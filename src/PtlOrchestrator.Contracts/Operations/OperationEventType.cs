using System.Text.Json.Serialization;

namespace PtlOrchestrator.Contracts.Operations;

/// <summary>
/// Tipi di fatto registrati nel log delle operazioni. Sul filo viaggiano come stringa,
/// così aggiungere un valore non invalida i dati già memorizzati.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationEventType
{
    SessionStarted,
    SessionEnded,
    BarcodeAccepted,
    BarcodeRejected,
    BasketConfirmed,
    WrongModulePressed,
    OperationTimedOut,
    PtlFault,
    CartsReset
}
