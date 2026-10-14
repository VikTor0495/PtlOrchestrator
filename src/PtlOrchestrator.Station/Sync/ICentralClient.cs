using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;

namespace PtlOrchestrator.Station.Sync;

/// <summary>Comunicazione della postazione con il server centrale. Non solleva eccezioni per errori di rete.</summary>
public interface ICentralClient
{
    Task<CentralResult<IngestEventsResponse>> SendEventsAsync(
        IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken);

    /// <summary>Operatori modificati dopo <paramref name="since"/>; senza valore restituisce l'anagrafica completa.</summary>
    Task<CentralResult<OperatorsSnapshotResponse>> GetOperatorsSnapshotAsync(
        DateTimeOffset? since, CancellationToken cancellationToken);
}
