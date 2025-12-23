namespace PtlController.Domain;

public sealed class CartAssignmentResult
{
    private CartAssignmentResult(
        bool success,
        Cart? cart,
        Basket? basket,
        CartAssignmentType type,
        string? reason)
    {
        Success = success;
        Cart = cart;
        Basket = basket;
        Type = type;
        Reason = reason;
    }

    public bool Success { get; }
    public Cart? Cart { get; }
    public Basket? Basket { get; }
    public CartAssignmentType Type { get; }
    public string? Reason { get; }

       public static CartAssignmentResult NewItem(Cart cart, Basket basket)
        => new(true, cart, basket, CartAssignmentType.NewItem);

    public static CartAssignmentResult ExistingItem(Cart cart, Basket basket)
        => new(true, cart, basket, CartAssignmentType.ExistingItem);

    public static CartAssignmentResult Rejected(string reason)
        => new(false, null, null, CartAssignmentType.Rejected, reason);
}
