using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Core.Tests.Support;

internal sealed class CollectingOperationEventSink : IOperationEventSink
{
    public List<OperationEvent> Events { get; } = [];

    public Exception? Failure { get; set; }

    public Task WriteAsync(OperationEvent operationEvent, CancellationToken cancellationToken)
    {
        if (Failure is not null)
            throw Failure;

        Events.Add(operationEvent);
        return Task.CompletedTask;
    }
}
