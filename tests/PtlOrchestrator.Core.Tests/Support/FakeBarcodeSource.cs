using System.Runtime.CompilerServices;
using System.Threading.Channels;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Core.Tests.Support;

internal sealed class FakeBarcodeSource : IBarcodeSource
{
    private readonly Channel<string> _scans = Channel.CreateUnbounded<string>();

    public void Push(string code) => _scans.Writer.TryWrite(code);

    public void Complete() => _scans.Writer.TryComplete();

    public async IAsyncEnumerable<string> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var code in _scans.Reader.ReadAllAsync(cancellationToken))
            yield return code;
    }
}
