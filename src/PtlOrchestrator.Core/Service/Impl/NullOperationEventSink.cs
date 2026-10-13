using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Service.Impl;

public sealed class NullOperationEventSink : IOperationEventSink
{
    public Task WriteAsync(OperationEvent operationEvent, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
