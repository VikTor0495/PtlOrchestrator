namespace PtlController.Domain;

public sealed class CartAssignmentResult
{
    private CartAssignmentResult(
        bool success,
        CartAssignmentType type,
        Cart? cart,
        Basket? basket,
        string? reason)
    {
        Success = success;
        Type = type;
        Cart = cart;
        Basket = basket;
        Reason = reason;
    }

    public bool Success { get; }
    public CartAssignmentType Type { get; }
    public Cart? Cart { get; }
    public Basket? Basket { get; }
    public string? Reason { get; }

    public static CartAssignmentResult ExistingItem(Cart cart, Basket basket)
        => new(true, CartAssignmentType.ExistingItem, cart, basket, null);

    public static CartAssignmentResult NewItem(Cart cart, Basket basket)
        => new(true, CartAssignmentType.NewItem, cart, basket, null);

    public static CartAssignmentResult Full(string reason)
        => new(false, CartAssignmentType.Full, null, null, reason);

    public static CartAssignmentResult Rejected(string reason)
        => new(false, CartAssignmentType.Rejected, null, null, reason);
}
