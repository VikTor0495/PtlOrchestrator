using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Core.Tests.Support;

internal sealed class FakeOperationRecorder : IOperationRecorder
{
    public List<RecordedOperation> Recorded { get; } = [];

    public IEnumerable<OperationEventType> Types => Recorded.Select(r => r.Type);

    public Task RecordAsync(
        OperationEventType type,
        string? barcode = null,
        string? cartId = null,
        string? basketId = null,
        string? details = null)
    {
        Recorded.Add(new RecordedOperation(type, barcode, cartId, basketId, details));
        return Task.CompletedTask;
    }
}

internal sealed record RecordedOperation(
    OperationEventType Type,
    string? Barcode,
    string? CartId,
    string? BasketId,
    string? Details);
