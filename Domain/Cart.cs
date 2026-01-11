namespace PtlOrchestrator.Domain;
public sealed class Cart(int cartId, IEnumerable<Basket> baskets)
{
    public int CartId { get; } = cartId;
    private readonly List<Basket> _baskets = [.. baskets];

    public IReadOnlyCollection<Basket> GetBaskets => _baskets;

    public CartAssignmentResult TryAddItem(string barcode, int maxQuantity)
    {
        var existing = _baskets
            .FirstOrDefault(b => b.Barcode == barcode && !b.IsFull);

        if (existing is not null)
        {
            existing.AddItem(barcode);
            return CartAssignmentResult.ExistingItem(this, existing);
        }

        var empty = _baskets.FirstOrDefault(b => b.IsEmpty);
        if (empty is not null)
        {
            empty.UpdateMaxQuantity(maxQuantity);
            empty.AddItem(barcode);
            return CartAssignmentResult.NewItem(this, empty);
        }

        return CartAssignmentResult.Full(
            $"Carrello {CartId} saturo per prodotto {barcode}");
    }

    public void Reset()
        => _baskets.ForEach(b => b.Reset());

    public void RemoveItem(string basketId, string barcode)
    {
        var basket = _baskets.FirstOrDefault(b => b.BasketId == basketId);
        basket?.RemoveItem(barcode);
    }

}

