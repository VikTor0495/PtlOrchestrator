using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Service;
using PtlOrchestrator.Station.Configuration;

namespace PtlOrchestrator.Station.Scanner;

/// <summary>
/// Legge i codici da una pistola collegata a una porta seriale. Ogni codice termina con CR e/o LF.
/// Se la porta non si apre o si scollega, riprova ogni <see cref="SerialScannerOptions.ReconnectSeconds"/>
/// senza mai terminare finché non viene annullata la lettura.
/// </summary>
public sealed class SerialBarcodeSource(
    ISerialConnectionFactory connectionFactory,
    IOptions<SerialScannerOptions> options,
    TimeProvider timeProvider,
    ILogger<SerialBarcodeSource> logger) : IBarcodeSource
{
    private readonly ISerialConnectionFactory _connectionFactory = connectionFactory;
    private readonly SerialScannerOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<SerialBarcodeSource> _logger = logger;

    public async IAsyncEnumerable<string> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reconnectDelay = TimeSpan.FromSeconds(Math.Max(1, _options.ReconnectSeconds));
        var buffer = new byte[256];

        while (!cancellationToken.IsCancellationRequested)
        {
            var connection = TryOpen();

            if (connection is null)
            {
                if (!await TryDelayAsync(reconnectDelay, cancellationToken))
                    yield break;

                continue;
            }

            using (connection)
            {
                var framer = new LineFramer(_options.MaxCodeLength);

                while (true)
                {
                    var read = await TryReadAsync(connection, buffer, cancellationToken);
                    if (read <= 0)
                        break;

                    foreach (var code in framer.Append(buffer.AsSpan(0, read)))
                        yield return code;
                }
            }

            if (cancellationToken.IsCancellationRequested)
                yield break;

            _logger.LogWarning("Scanner {Port} scollegato, nuovo tentativo tra {Seconds}s", _options.PortName, reconnectDelay.TotalSeconds);

            if (!await TryDelayAsync(reconnectDelay, cancellationToken))
                yield break;
        }
    }

    private ISerialConnection? TryOpen()
    {
        try
        {
            var connection = _connectionFactory.Open(_options);
            _logger.LogInformation("Scanner collegato su {Port}", _options.PortName);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Scanner {Port} non disponibile: {Reason}", _options.PortName, ex.Message);
            return null;
        }
    }

    private async Task<int> TryReadAsync(ISerialConnection connection, byte[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Errore di lettura dallo scanner: {Reason}", ex.Message);
            return 0;
        }
    }

    private async Task<bool> TryDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Ricompone i codici dai byte ricevuti a pezzi; scarta i codici oltre la lunghezza massima.</summary>
    internal sealed class LineFramer(int maxLength)
    {
        private readonly StringBuilder _current = new();
        private bool _overflow;

        public IEnumerable<string> Append(ReadOnlySpan<byte> bytes)
        {
            var completed = new List<string>();

            foreach (var b in bytes)
            {
                if (b is (byte)'\r' or (byte)'\n')
                {
                    if (!_overflow && _current.Length > 0)
                        completed.Add(_current.ToString().Trim());

                    _current.Clear();
                    _overflow = false;
                }
                else if (_current.Length < maxLength)
                {
                    _current.Append((char)b);
                }
                else
                {
                    _overflow = true;
                }
            }

            return completed.Where(code => code.Length > 0);
        }
    }
}
