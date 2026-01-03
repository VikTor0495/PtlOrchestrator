
namespace PtlOrchestrator.Domain;

public sealed class CartContainer(IEnumerable<Cart> carts)
{
    private readonly List<Cart> _carts = [.. carts];

    public CartAssignmentResult AssignItem(string barcode)
    {
        foreach (var cart in _carts)
        {
            var result = cart.TryAddItem(barcode);
            if (result.Success)
                return result;
        }

        return CartAssignmentResult.Rejected(
            $"Nessun basket disponibile per il prodotto {barcode}");
    }

    public void ResetAll()
        => _carts.ForEach(c => c.Reset());

    public void ShowStatus()
    {
        Console.WriteLine("");
        Console.WriteLine("═══════════════ STATO CARRELLI ═══════════════");

        foreach (var cart in _carts)
        {
            Console.WriteLine($"Carrello {cart.CartId}:");

            foreach (var basket in cart.Baskets)
            {
                if (basket.IsEmpty)
                {
                    Console.WriteLine(
                        $"  - Basket {basket.BasketId}: VUOTO (max {basket.MaxQuantity})");
                }
                else
                {
                    Console.WriteLine(
                        $"  - Basket {basket.BasketId}: " +
                        $"{basket.CurrentQuantity}/{basket.MaxQuantity} " +
                        $"[{basket.Barcode}]");
                }
            }

            Console.WriteLine("");
        }

        Console.WriteLine("══════════════════════════════════════════════");
        Console.WriteLine("");
    }

    public void Rollback(CartAssignmentResult assignment)
    {
        if (!assignment.Success || assignment.Cart == null || assignment.Basket == null || string.IsNullOrEmpty(assignment.Basket.Barcode))
            return;

        var cart = _carts.FirstOrDefault(c => c.CartId == assignment.Cart.CartId);
        if (cart == null)
            return;

        cart.RemoveItem(
            assignment.Basket.BasketId,
            assignment.Basket.Barcode);
    }

}