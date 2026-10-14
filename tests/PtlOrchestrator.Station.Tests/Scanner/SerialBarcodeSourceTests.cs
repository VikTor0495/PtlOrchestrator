using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Scanner;

namespace PtlOrchestrator.Station.Tests.Scanner;

public class SerialBarcodeSourceTests
{
    /// <summary>Connessione che consegna i blocchi indicati e poi si "scollega" (0 byte).</summary>
    private sealed class ScriptedConnection(IEnumerable<byte[]> chunks) : ISerialConnection
    {
        private readonly Queue<byte[]> _chunks = new(chunks);

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_chunks.Count == 0)
                return ValueTask.FromResult(0);

            var chunk = _chunks.Dequeue();
            chunk.CopyTo(buffer);
            return ValueTask.FromResult(chunk.Length);
        }

        public void Dispose() { }
    }

    private sealed class BlockingConnection : ISerialConnection
    {
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public void Dispose() { }
    }

    private sealed class ScriptedFactory(params Func<ISerialConnection>[] attempts) : ISerialConnectionFactory
    {
        private readonly Queue<Func<ISerialConnection>> _attempts = new(attempts);

        public int Opened { get; private set; }

        public ISerialConnection Open(SerialScannerOptions options)
        {
            Opened++;
            return _attempts.Count > 0 ? _attempts.Dequeue()() : new BlockingConnection();
        }
    }

    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    private static SerialBarcodeSource CreateSource(ISerialConnectionFactory factory, int maxLength = 128)
        => new(
            factory,
            Options.Create(new SerialScannerOptions { PortName = "COM9", ReconnectSeconds = 1, MaxCodeLength = maxLength }),
            TimeProvider.System,
            NullLogger<SerialBarcodeSource>.Instance);

    private static async Task<List<string>> ReadAsync(SerialBarcodeSource source, int count)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var codes = new List<string>();

        await foreach (var code in source.ReadAsync(cts.Token))
        {
            codes.Add(code);
            if (codes.Count == count)
                break;
        }

        return codes;
    }

    [Fact]
    public async Task ReadAsync_SplitsCodesOnCarriageReturnAndLineFeed()
    {
        var factory = new ScriptedFactory(() => new ScriptedConnection([Bytes("AAA\r\nBBB\rCCC\n")]));

        var codes = await ReadAsync(CreateSource(factory), 3);

        Assert.Equal(["AAA", "BBB", "CCC"], codes);
    }

    [Fact]
    public async Task ReadAsync_ReassemblesCodeSplitAcrossReads()
    {
        var factory = new ScriptedFactory(() => new ScriptedConnection([Bytes("12"), Bytes("34"), Bytes("56\r")]));

        var codes = await ReadAsync(CreateSource(factory), 1);

        Assert.Equal(["123456"], codes);
    }

    [Fact]
    public async Task ReadAsync_IgnoresEmptyLines()
    {
        var factory = new ScriptedFactory(() => new ScriptedConnection([Bytes("\r\n\r\nXYZ\r\n")]));

        var codes = await ReadAsync(CreateSource(factory), 1);

        Assert.Equal(["XYZ"], codes);
    }

    [Fact]
    public async Task ReadAsync_DiscardsCodesLongerThanMax_AndRecoversOnNextCode()
    {
        var factory = new ScriptedFactory(() => new ScriptedConnection([Bytes("TOOLONGCODE\rOK\r")]));

        var codes = await ReadAsync(CreateSource(factory, maxLength: 5), 1);

        Assert.Equal(["OK"], codes);
    }

    [Fact]
    public async Task ReadAsync_PortUnavailable_RetriesUntilItOpens()
    {
        var factory = new ScriptedFactory(
            () => throw new IOException("COM9 assente"),
            () => new ScriptedConnection([Bytes("AFTER\r")]));

        var codes = await ReadAsync(CreateSource(factory), 1);

        Assert.Equal(["AFTER"], codes);
        Assert.Equal(2, factory.Opened);
    }

    [Fact]
    public async Task ReadAsync_Disconnection_ReconnectsAndKeepsReading()
    {
        var factory = new ScriptedFactory(
            () => new ScriptedConnection([Bytes("ONE\r")]),
            () => new ScriptedConnection([Bytes("TWO\r")]));

        var codes = await ReadAsync(CreateSource(factory), 2);

        Assert.Equal(["ONE", "TWO"], codes);
        Assert.Equal(2, factory.Opened);
    }

    [Fact]
    public async Task ReadAsync_Cancellation_EndsEnumerationWithoutError()
    {
        var factory = new ScriptedFactory(() => new BlockingConnection());
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var codes = new List<string>();
        await foreach (var code in CreateSource(factory).ReadAsync(cts.Token))
            codes.Add(code);

        Assert.Empty(codes);
    }
}
