using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Service;

public interface IOperationRecorder
{
    /// <summary>
    /// Registra un fatto stampandolo con postazione, operatore, sessione e orario correnti.
    /// Non lancia mai: un guasto di registrazione non deve interrompere il picking.
    /// </summary>
    Task RecordAsync(
        OperationEventType type,
        string? barcode = null,
        string? cartId = null,
        string? basketId = null,
        string? details = null);
}
