namespace PtlOrchestrator.Service.Impl;

/// <summary>
/// Implementazione per le installazioni senza server carrelli (il Console legacy): nessun carrello
/// è sottoposto a check-in, quindi il catalogo è vuoto e ogni richiesta è rifiutata.
/// </summary>
public sealed class NoCartAssignmentClient : ICartAssignmentClient
{
    public bool IsKnownCart(string cartCode) => false;

    public Task RefreshCatalogAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<CartClaimResult> ClaimAsync(string cartCode, string stationId, Guid? sessionId, CancellationToken cancellationToken)
        => Task.FromResult(new CartClaimResult(null, "Nessun server carrelli configurato"));

    public Task ReleaseAsync(string cartCode, string stationId, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
