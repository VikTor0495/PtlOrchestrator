using PtlOrchestrator.Domain;
using PtlOrchestrator.Tests.Support;

namespace PtlOrchestrator.Tests.Domain;

public class CartTests
{
    private const string BarcodeA = "AAA";
    private const string BarcodeB = "BBB";

    [Fact]
    public void GetBaskets_PreservesConfiguredOrder()
    {
        var cart = CartFactory.CreateCart("1", "0003", "0001", "0002");

        Assert.Equal(["0003", "0001", "0002"], cart.GetBaskets.Select(b => b.BasketId));
    }

    [Fact]
    public void TryAddItem_OnEmptyCart_AssignsFirstBasketAsNewItem()
    {
        var cart = CartFactory.CreateCart("1", "0001", "0002");

        var result = cart.TryAddItem(BarcodeA, maxQuantity: 3);

        Assert.True(result.Success);
        Assert.Equal(CartAssignmentType.NewItem, result.Type);
        Assert.Same(cart, result.Cart);
        Assert.Equal("0001", result.Basket!.BasketId);
        Assert.Equal(3, result.Basket.MaxQuantity);
        Assert.Equal(1, result.Basket.CurrentQuantity);
    }

    [Fact]
    public void TryAddItem_WithBarcodeAlreadyInBasket_ReusesBasketAsExistingItem()
    {
        var cart = CartFactory.CreateCart("1", "0001", "0002");
        cart.TryAddItem(BarcodeA, maxQuantity: 3);

        var result = cart.TryAddItem(BarcodeA, maxQuantity: 3);

        Assert.True(result.Success);
        Assert.Equal(CartAssignmentType.ExistingItem, result.Type);
        Assert.Equal("0001", result.Basket!.BasketId);
        Assert.Equal(2, result.Basket.CurrentQuantity);
    }

    [Fact]
    public void TryAddItem_WithDifferentBarcode_UsesNextEmptyBasket()
    {
        var cart = CartFactory.CreateCart("1", "0001", "0002");
        cart.TryAddItem(BarcodeA, maxQuantity: 3);

        var result = cart.TryAddItem(BarcodeB, maxQuantity: 2);

        Assert.Equal(CartAssignmentType.NewItem, result.Type);
        Assert.Equal("0002", result.Basket!.BasketId);
        Assert.Equal(2, result.Basket.MaxQuantity);
    }

    [Fact]
    public void TryAddItem_WhenBasketOfBarcodeIsFull_OpensNewBasketForSameBarcode()
    {
        var cart = CartFactory.CreateCart("1", "0001", "0002");
        cart.TryAddItem(BarcodeA, maxQuantity: 1);

        var result = cart.TryAddItem(BarcodeA, maxQuantity: 1);

        Assert.Equal(CartAssignmentType.NewItem, result.Type);
        Assert.Equal("0002", result.Basket!.BasketId);
    }

    [Fact]
    public void TryAddItem_WhenNoBasketIsAvailable_ReturnsFullWithReason()
    {
        var cart = CartFactory.CreateCart("7", "0001");
        cart.TryAddItem(BarcodeA, maxQuantity: 1);

        var result = cart.TryAddItem(BarcodeB, maxQuantity: 1);

        Assert.False(result.Success);
        Assert.Equal(CartAssignmentType.Full, result.Type);
        Assert.Null(result.Cart);
        Assert.Null(result.Basket);
        Assert.Contains("7", result.Reason);
        Assert.Contains(BarcodeB, result.Reason);
    }

    [Fact]
    public void TryAddItem_WithZeroMaxQuantity_ThrowsBecauseBasketCannotAcceptItems()
    {
        var cart = CartFactory.CreateCart("1", "0001");

        Assert.Throws<InvalidOperationException>(() => cart.TryAddItem(BarcodeA, maxQuantity: 0));
    }

    [Fact]
    public void Reset_EmptiesAllBaskets()
    {
        var cart = CartFactory.CreateCart("1", "0001", "0002");
        cart.TryAddItem(BarcodeA, maxQuantity: 3);
        cart.TryAddItem(BarcodeB, maxQuantity: 3);

        cart.Reset();

        Assert.All(cart.GetBaskets, basket => Assert.True(basket.IsEmpty));
    }

    [Fact]
    public void RemoveItem_RemovesFromTheRequestedBasketOnly()
    {
        var cart = CartFactory.CreateCart("1", "0001", "0002");
        cart.TryAddItem(BarcodeA, maxQuantity: 3);
        cart.TryAddItem(BarcodeB, maxQuantity: 3);

        cart.RemoveItem("0002", BarcodeB);

        Assert.Equal(1, cart.GetBaskets.Single(b => b.BasketId == "0001").CurrentQuantity);
        Assert.True(cart.GetBaskets.Single(b => b.BasketId == "0002").IsEmpty);
    }

    [Fact]
    public void RemoveItem_WithUnknownBasket_DoesNothing()
    {
        var cart = CartFactory.CreateCart("1", "0001");
        cart.TryAddItem(BarcodeA, maxQuantity: 3);

        cart.RemoveItem("9999", BarcodeA);

        Assert.Equal(1, cart.GetBaskets.Single().CurrentQuantity);
    }
}
