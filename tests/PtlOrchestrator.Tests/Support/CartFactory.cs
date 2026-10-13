using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Tests.Support;

internal static class CartFactory
{
    public static Cart CreateCart(string cartId, params string[] basketIds)
        => new(cartId, basketIds.Select(id => new Basket(id, maxQuantity: 0)));

    public static CartContainer CreateContainer(params Cart[] carts)
        => new(carts);

    public static CartContainer CreateSingleCartContainer(params string[] basketIds)
        => CreateContainer(CreateCart("1", basketIds));
}
