using PtlOrchestrator.Domain;
using PtlOrchestrator.File;

namespace PtlOrchestrator.Core.Tests.Support;

internal sealed class FakeCartReportWriter : ICartReportWriter
{
    public int WriteCalls { get; private set; }

    public Exception? Failure { get; set; }

    public void Write(IEnumerable<Cart> carts)
    {
        WriteCalls++;

        if (Failure is not null)
            throw Failure;
    }
}
