
namespace PtlController.Domain;

public sealed class CartContainer(IEnumerable<Cart> carts)
{
    private readonly List<Cart> _carts = [.. carts];

    public CartAssignmentResult AssignItem(string barcode)
    {
        foreach (var cart in _carts)
        {
            var basket = cart.TryAddItem(barcode);
            if (basket is not null)
            {
                return CartAssignmentResult.Accepted(cart.CartId, basket);
            }
        }

        return CartAssignmentResult.Rejected(
            $"Nessuna cesta disponibile per il prodotto {barcode}");
    }

    public void ResetAll()
        => _carts.ForEach(c => c.Reset());

}