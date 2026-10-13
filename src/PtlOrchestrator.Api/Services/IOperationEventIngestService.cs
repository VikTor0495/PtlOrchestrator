using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Services;

public interface IOperationEventIngestService
{
    /// <summary>
    /// Memorizza un lotto di eventi in modo idempotente: gli eventi con un <c>Id</c> già noto
    /// sono ignorati e conteggiati come duplicati, così la postazione può ritentare senza rischi.
    /// </summary>
    Task<IngestEventsResponse> IngestAsync(IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken);
}
