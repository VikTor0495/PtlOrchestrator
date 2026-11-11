namespace PtlOrchestrator.Api.Persistence.Entities;

/// <summary>Carrello censito: il codice è il numero seriale che vede l'operatore.</summary>
public sealed class CartEntity
{
    public string Code { get; set; } = string.Empty;
    public string ControllerIp { get; set; } = string.Empty;
    public int ControllerPort { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CartBasketEntity> Baskets { get; set; } = [];
}

public sealed class CartBasketEntity
{
    public string CartCode { get; set; } = string.Empty;
    public string BasketId { get; set; } = string.Empty;
    public CartEntity? Cart { get; set; }
}

/// <summary>
/// Assegnazione di un carrello a una postazione. Un carrello è occupato finché esiste una riga
/// con <see cref="ReleasedAt"/> nullo (indice univoco parziale): le righe chiuse restano come storico.
/// </summary>
public sealed class CartAssignmentEntity
{
    public Guid Id { get; set; }
    public string CartCode { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}
