using PtlOrchestrator.Contracts.Carts;

namespace PtlOrchestrator.Service;

/// <summary>Esito della richiesta di occupazione di un carrello al server centrale.</summary>
public sealed record CartClaimResult(CartCheckInResponse? Cart, string? Error)
{
    public bool Success => Error is null && Cart is not null;
}

/// <summary>
/// Accesso al catalogo dei carrelli e alla loro occupazione sul server centrale.
/// L'implementazione della postazione chiama l'Api; in assenza di server il check-in è rifiutato.
/// </summary>
public interface ICartAssignmentClient
{
    /// <summary>True se il codice letto corrisponde a un carrello del catalogo (dall'ultimo aggiornamento).</summary>
    bool IsKnownCart(string cartCode);

    /// <summary>Aggiorna il catalogo dei codici carrello.</summary>
    Task RefreshCatalogAsync(CancellationToken cancellationToken);

    /// <summary>Chiede al server di occupare il carrello per questa postazione.</summary>
    Task<CartClaimResult> ClaimAsync(string cartCode, string stationId, Guid? sessionId, CancellationToken cancellationToken);

    /// <summary>Libera il carrello sul server. Lancia eccezione se il server non risponde o il carrello è di un'altra postazione.</summary>
    Task ReleaseAsync(string cartCode, string stationId, CancellationToken cancellationToken);
}
