
using System.Text;

namespace PtlOrchestrator.Domain;

public sealed class CartContainer(IEnumerable<Cart> carts)
{
    private readonly List<Cart> _carts = [.. carts];

    public CartAssignmentResult AssignItem(string barcode, int maxQuantity)
    {
        foreach (var cart in _carts)
        {
            var result = cart.TryAddItem(barcode, maxQuantity);
            if (result.Success)
                return result;
        }

        return CartAssignmentResult.Rejected(
            $"Nessun basket disponibile per il prodotto {barcode}");
    }

    public void ResetAll()
        => _carts.ForEach(c => c.Reset());

    public string GetStatusString()
    {
        var sb = new StringBuilder();

        sb.AppendLine("");
        sb.AppendLine("═══════════════ STATO CARRELLI ═══════════════");

        foreach (var cart in _carts)
        {
            sb.AppendLine($"Carrello {cart.CartId}:");

            foreach (var basket in cart.GetBaskets)
            {
                if (basket.IsEmpty)
                {
                    sb.AppendLine(
                        $"  - Basket {basket.BasketId}: VUOTO (max non settato)");
                }
                else
                {
                    sb.AppendLine(
                        $"  - Basket {basket.BasketId}: " +
                        $"{basket.CurrentQuantity}/{basket.MaxQuantity} " +
                        $"[{basket.Barcode}]");
                }
            }

            sb.AppendLine("");
        }

        sb.AppendLine("══════════════════════════════════════════════");
        sb.AppendLine("");
        return sb.ToString();
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

    public IEnumerable<Cart> GetCarts() => _carts;
}