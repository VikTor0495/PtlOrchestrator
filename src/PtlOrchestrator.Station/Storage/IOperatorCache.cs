using PtlOrchestrator.Contracts.Operators;

namespace PtlOrchestrator.Station.Storage;

/// <summary>Copia locale dell'anagrafica operatori sincronizzata dal server.</summary>
public interface IOperatorCache
{
    Task UpsertAsync(IReadOnlyCollection<OperatorDto> operators, CancellationToken cancellationToken);

    /// <summary>Istante del server dell'ultima sincronizzazione riuscita, se c'è stata.</summary>
    Task<DateTimeOffset?> GetLastSyncAsync(CancellationToken cancellationToken);

    Task SetLastSyncAsync(DateTimeOffset serverTime, CancellationToken cancellationToken);
}
