using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Service;

/// <summary>
/// Destinazione degli eventi di operazione. La postazione la implementa con una coda locale
/// durevole; il sink non deve mai dipendere dalla disponibilità della rete.
/// </summary>
public interface IOperationEventSink
{
    Task WriteAsync(OperationEvent operationEvent, CancellationToken cancellationToken);
}
