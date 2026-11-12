
using System.Text;

namespace PtlOrchestrator.Domain;

public enum CartCheckInResult
{
    Done,
    AlreadyActive,
    Unknown,
    /// <summary>Il server ha negato l'occupazione o il controller non risponde.</summary>
    Rejected,
    /// <summary>Ci sono già <see cref="CartContainer.MaxActiveCarts"/> carrelli attivi nel turno.</summary>
    LimitReached
}

/// <summary>
/// Carrelli configurati e carrelli attivi nel turno. Un carrello è configurato all'avvio, ma diventa
/// attivo solo con il check-in dell'operatore; senza carrelli attivi non si assegnano prodotti.
/// </summary>
public sealed class CartContainer
{
    private readonly List<Cart> _carts;
    private readonly HashSet<string> _active;

    /// <summary>Massimo numero di carrelli che una postazione può avere attivi contemporaneamente.</summary>
    public const int MaxActiveCarts = 4;

    public int ActiveCount => _active.Count;

    /// <param name="carts">Carrelli configurati.</param>
    /// <param name="activeCartIds">Carrelli attivi all'avvio; se nullo, tutti i configurati restano attivi.</param>
    public CartContainer(IEnumerable<Cart> carts, IEnumerable<string>? activeCartIds = null)
    {
        _carts = [.. carts];
        _active = activeCartIds is null
            ? _carts.Select(c => c.CartId).ToHashSet()
            : [.. activeCartIds];
    }

    public CartAssignmentResult AssignItem(string barcode, int maxQuantity)
    {
        if (_active.Count == 0)
            return CartAssignmentResult.Rejected(
                "Nessun carrello attivo: fai il check-in di un carrello prima di scansionare prodotti");

        foreach (var cart in _carts.Where(c => _active.Contains(c.CartId)))
        {
            var result = cart.TryAddItem(barcode, maxQuantity);
            if (result.Success)
                return result;
        }

        return CartAssignmentResult.Rejected(
            $"Nessun basket disponibile per il prodotto {barcode}");
    }

    /// <summary>Attiva un carrello configurato. Il codice del carrello è il suo CartId.</summary>
    public CartCheckInResult CheckIn(string cartId)
    {
        if (!_carts.Any(c => c.CartId == cartId))
            return CartCheckInResult.Unknown;

        if (!_active.Contains(cartId) && _active.Count >= MaxActiveCarts)
            return CartCheckInResult.LimitReached;

        return _active.Add(cartId)
            ? CartCheckInResult.Done
            : CartCheckInResult.AlreadyActive;
    }

    /// <summary>True se il codice letto corrisponde a un carrello configurato (attivo o no).</summary>
    public bool IsKnownCart(string cartId) => _carts.Any(c => c.CartId == cartId);

    public bool IsActive(string cartId) => _active.Contains(cartId);

    /// <summary>
    /// Attiva un carrello ricevuto dal server (con controller e cestelli censiti a DB).
    /// Se il carrello è già attivo non cambia nulla.
    /// </summary>
    public CartCheckInResult Activate(Cart cart)
    {
        if (_active.Contains(cart.CartId))
            return CartCheckInResult.AlreadyActive;

        if (_active.Count >= MaxActiveCarts)
            return CartCheckInResult.LimitReached;

        _carts.RemoveAll(c => c.CartId == cart.CartId);
        _carts.Add(cart);
        _active.Add(cart.CartId);
        return CartCheckInResult.Done;
    }

    /// <summary>Rimuove un carrello attivo dal turno. Non controlla che sia vuoto: lo fa il chiamante.</summary>
    public Cart? Release(string cartId)
    {
        var cart = _carts.FirstOrDefault(c => c.CartId == cartId && _active.Contains(c.CartId));
        if (cart is null)
            return null;

        _active.Remove(cartId);
        _carts.Remove(cart);
        return cart;
    }

    public Cart? GetActive(string cartId)
        => _carts.FirstOrDefault(c => c.CartId == cartId && _active.Contains(c.CartId));

    /// <summary>True se almeno un cestello dei carrelli attivi contiene prodotti.</summary>
    public bool HasItems()
        => GetCarts().Any(c => c.GetBaskets.Any(b => !b.IsEmpty));

    /// <summary>True se il carrello attivo non contiene prodotti.</summary>
    public bool IsEmpty(string cartId)
        => GetActive(cartId)?.GetBaskets.All(b => b.IsEmpty) ?? true;

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
                        $"[ {basket.Barcode} ] " +
                        $"{basket.CurrentQuantity}/{basket.MaxQuantity} ");
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

    /// <summary>Carrelli attivi nel turno, nell'ordine di configurazione.</summary>
    public IEnumerable<Cart> GetCarts() => _carts.Where(c => _active.Contains(c.CartId));
}
