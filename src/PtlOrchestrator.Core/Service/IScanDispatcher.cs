using PtlOrchestrator.Domain.Operations;

namespace PtlOrchestrator.Service;

public interface IScanDispatcher
{
    /// <summary>
    /// Smista un codice letto dalla pistola: senza sessione operatore è un badge di login,
    /// con sessione attiva è un prodotto da assegnare a una cesta.
    /// </summary>
    Task<ScanOutcome> DispatchAsync(string scannedCode, CancellationToken cancellationToken);
}
