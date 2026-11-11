namespace PtlOrchestrator.Domain;

/// <summary>Esito del check-in di un carrello, con il messaggio da mostrare all'operatore.</summary>
public sealed record CartCheckInOutcome(CartCheckInResult Result, string Message)
{
    public bool Success => Result == CartCheckInResult.Done;
}

/// <summary>Esito del rilascio di un carrello (carrello vuoto liberato dall'operatore).</summary>
public sealed record CartReleaseOutcome(bool Success, string Message);
