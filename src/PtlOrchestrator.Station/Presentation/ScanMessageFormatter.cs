using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;

namespace PtlOrchestrator.Station.Presentation;

public enum MessageSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>Messaggio breve e comprensibile per l'operatore.</summary>
public sealed record OperatorMessage(MessageSeverity Severity, string Text);

public static class ScanMessageFormatter
{
    /// <summary>Restituisce il messaggio per l'esito di una scansione, o null se non c'è nulla da mostrare.</summary>
    public static OperatorMessage? Format(string scannedCode, ScanOutcome outcome)
        => outcome.Kind switch
        {
            ScanOutcomeKind.Ignored => null,
            ScanOutcomeKind.OperatorLoggedIn => new(MessageSeverity.Success, $"Benvenuto {outcome.Session!.OperatorName}"),
            ScanOutcomeKind.LoginRejected => new(MessageSeverity.Error, FormatLoginFailure(outcome.LoginFailure!.Value)),
            ScanOutcomeKind.SystemBusy => new(MessageSeverity.Warning, "Sistema occupato: attendi e riscansiona"),
            ScanOutcomeKind.ProductAccepted => new(MessageSeverity.Success, FormatAccepted(scannedCode, outcome.Assignment!)),
            ScanOutcomeKind.ProductRejected => new(MessageSeverity.Error, outcome.Message ?? $"Prodotto {scannedCode} rifiutato"),
            ScanOutcomeKind.ProcessingFailed => new(MessageSeverity.Error, $"Errore durante l'operazione: {outcome.Message}"),
            _ => null
        };

    public static string FormatLoginFailure(LoginFailure failure)
        => failure switch
        {
            LoginFailure.UnknownOperator => "Operatore non riconosciuto",
            LoginFailure.OperatorInactive => "Operatore non attivo",
            LoginFailure.InvalidPin => "PIN errato",
            LoginFailure.SessionAlreadyActive => "C'è già un turno attivo: esci prima di cambiare operatore",
            _ => "Accesso non riuscito"
        };

    public static string FormatLogoutResult(LogoutResult result)
        => result switch
        {
            LogoutResult.Done => "Turno terminato",
            LogoutResult.NoActiveSession => "Nessun turno attivo",
            LogoutResult.SystemBusy => "Operazione in corso: attendi prima di uscire",
            _ => "Uscita non riuscita"
        };

    private static string FormatAccepted(string scannedCode, CartAssignmentResult assignment)
    {
        var basket = assignment.Basket!;

        return $"{scannedCode} → carrello {assignment.Cart!.CartId}, cesta {basket.BasketId} ({basket.CurrentQuantity}/{basket.MaxQuantity})";
    }
}
