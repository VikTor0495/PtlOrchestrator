using PtlOrchestrator.Domain;
using PtlOrchestrator.Core.Tests.Support;

namespace PtlOrchestrator.Core.Tests.Domain;

public class CartAssignmentResultTests
{
    private readonly Cart _cart = CartFactory.CreateCart("1", "0001");

    [Fact]
    public void ExistingItem_IsSuccessfulAndCarriesCartAndBasket()
    {
        var basket = _cart.GetBaskets.Single();

        var result = CartAssignmentResult.ExistingItem(_cart, basket);

        Assert.True(result.Success);
        Assert.Equal(CartAssignmentType.ExistingItem, result.Type);
        Assert.Same(_cart, result.Cart);
        Assert.Same(basket, result.Basket);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void NewItem_IsSuccessfulAndCarriesCartAndBasket()
    {
        var basket = _cart.GetBaskets.Single();

        var result = CartAssignmentResult.NewItem(_cart, basket);

        Assert.True(result.Success);
        Assert.Equal(CartAssignmentType.NewItem, result.Type);
        Assert.Same(_cart, result.Cart);
        Assert.Same(basket, result.Basket);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Full_IsFailureWithReasonAndNoCart()
    {
        var result = CartAssignmentResult.Full("saturo");

        Assert.False(result.Success);
        Assert.Equal(CartAssignmentType.Full, result.Type);
        Assert.Equal("saturo", result.Reason);
        Assert.Null(result.Cart);
        Assert.Null(result.Basket);
    }

    [Fact]
    public void Rejected_IsFailureWithReasonAndNoCart()
    {
        var result = CartAssignmentResult.Rejected("rifiutato");

        Assert.False(result.Success);
        Assert.Equal(CartAssignmentType.Rejected, result.Type);
        Assert.Equal("rifiutato", result.Reason);
        Assert.Null(result.Cart);
        Assert.Null(result.Basket);
    }
}
