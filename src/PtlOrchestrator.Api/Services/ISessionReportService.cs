using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Services;

public sealed record SessionQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? OperatorId,
    int Take);

public interface ISessionReportService
{
    Task<IReadOnlyList<WorkSessionSummary>> ListAsync(SessionQuery query, CancellationToken cancellationToken);

    /// <summary>Eventi di un turno in ordine cronologico, o <c>null</c> se il turno non esiste.</summary>
    Task<IReadOnlyList<OperationEvent>?> GetEventsAsync(Guid sessionId, CancellationToken cancellationToken);
}
