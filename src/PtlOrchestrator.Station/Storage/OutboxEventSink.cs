using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Station.Storage;

/// <summary>Scrive gli eventi nella coda locale durevole: l'invio al server avviene in un secondo momento.</summary>
public sealed class OutboxEventSink(IOutbox outbox) : IOperationEventSink
{
    private readonly IOutbox _outbox = outbox;

    public Task WriteAsync(OperationEvent operationEvent, CancellationToken cancellationToken)
        => _outbox.EnqueueAsync(operationEvent, cancellationToken);
}
