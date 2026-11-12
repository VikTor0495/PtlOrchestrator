using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Station.Presentation;

/// <summary>Fotografia immutabile di un carrello attivo, sicura da leggere dalla UI mentre il dominio continua a cambiare.</summary>
public sealed record CartSnapshot(string CartId, string Controller, IReadOnlyList<BasketSnapshot> Baskets)
{
    /// <summary>Un carrello vuoto può essere rimosso dal turno.</summary>
    public bool IsEmpty => Baskets.All(b => b.IsEmpty);

    public static IReadOnlyList<CartSnapshot> From(CartContainer container)
        => container.GetCarts()
            .Select(cart => new CartSnapshot(
                cart.CartId,
                cart.Endpoint?.ToString() ?? "predefinito",
                cart.GetBaskets.Select(b => new BasketSnapshot(b.BasketId, b.Barcode, b.CurrentQuantity, b.MaxQuantity)).ToList()))
            .ToList();
}

public sealed record BasketSnapshot(string BasketId, string? Barcode, int CurrentQuantity, int MaxQuantity)
{
    public bool IsEmpty => CurrentQuantity == 0;

    public bool IsFull => !IsEmpty && CurrentQuantity >= MaxQuantity;

    public string Display => IsEmpty ? "vuota" : $"{Barcode}  {CurrentQuantity}/{MaxQuantity}";
}
