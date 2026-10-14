using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Station.Storage;

/// <summary>
/// Coda durevole degli eventi in attesa di essere inviati al server centrale.
/// Gli eventi escono in ordine di inserimento.
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync(OperationEvent operationEvent, CancellationToken cancellationToken);

    /// <summary>I prossimi eventi da inviare (esclusi quelli messi in quarantena), dal più vecchio.</summary>
    Task<IReadOnlyList<OperationEvent>> PeekBatchAsync(int maxCount, CancellationToken cancellationToken);

    /// <summary>Rimuove dalla coda gli eventi consegnati.</summary>
    Task RemoveAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken);

    /// <summary>Registra un tentativo fallito (temporaneo) senza togliere gli eventi dalla coda.</summary>
    Task RecordFailureAsync(IReadOnlyCollection<Guid> eventIds, string error, CancellationToken cancellationToken);

    /// <summary>Esclude dall'invio gli eventi rifiutati in modo definitivo dal server, conservandoli per l'analisi.</summary>
    Task QuarantineAsync(IReadOnlyCollection<Guid> eventIds, string reason, CancellationToken cancellationToken);

    Task<int> CountPendingAsync(CancellationToken cancellationToken);

    Task<int> CountQuarantinedAsync(CancellationToken cancellationToken);
}
