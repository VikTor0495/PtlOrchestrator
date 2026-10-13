using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;

namespace PtlOrchestrator.Core.Tests.Support;

internal sealed class FakeCartManager : ICartManager
{
    public List<string> ProcessedBarcodes { get; } = [];

    public Func<string, Task<CartAssignmentResult>>? OnProcessBarcode { get; set; }

    public Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken)
    {
        ProcessedBarcodes.Add(barcode);

        return OnProcessBarcode?.Invoke(barcode)
            ?? Task.FromResult(CartAssignmentResult.Rejected("non configurato"));
    }

    public Task ResetAll(CancellationToken cancellationToken) => Task.CompletedTask;

    public void ShowStatus()
    {
    }

    public void WriteCsvReport()
    {
    }
}
