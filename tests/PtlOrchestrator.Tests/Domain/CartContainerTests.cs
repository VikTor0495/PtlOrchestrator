using PtlOrchestrator.Domain;
using PtlOrchestrator.Tests.Support;

namespace PtlOrchestrator.Tests.Domain;

public class CartContainerTests
{
    private const string BarcodeA = "AAA";
    private const string BarcodeB = "BBB";

    [Fact]
    public void AssignItem_UsesFirstCartWithSpace()
    {
        var container = CartFactory.CreateContainer(
            CartFactory.CreateCart("1", "0001"),
            CartFactory.CreateCart("2", "0002"));

        var result = container.AssignItem(BarcodeA, maxQuantity: 2);

        Assert.True(result.Success);
        Assert.Equal("1", result.Cart!.CartId);
        Assert.Equal("0001", result.Basket!.BasketId);
    }

    [Fact]
    public void AssignItem_WhenFirstCartIsSaturated_SpillsOverToNextCart()
    {
        var container = CartFactory.CreateContainer(
            CartFactory.CreateCart("1", "0001"),
            CartFactory.CreateCart("2", "0002"));
        container.AssignItem(BarcodeA, maxQuantity: 1);

        var result = container.AssignItem(BarcodeB, maxQuantity: 1);

        Assert.True(result.Success);
        Assert.Equal("2", result.Cart!.CartId);
        Assert.Equal("0002", result.Basket!.BasketId);
    }

    [Fact]
    public void AssignItem_WhenAllCartsAreSaturated_ReturnsRejectedWithBarcodeInReason()
    {
        var container = CartFactory.CreateSingleCartContainer("0001");
        container.AssignItem(BarcodeA, maxQuantity: 1);

        var result = container.AssignItem(BarcodeB, maxQuantity: 1);

        Assert.False(result.Success);
        Assert.Equal(CartAssignmentType.Rejected, result.Type);
        Assert.Contains(BarcodeB, result.Reason);
    }

    [Fact]
    public void ResetAll_EmptiesEveryBasketOfEveryCart()
    {
        var container = CartFactory.CreateContainer(
            CartFactory.CreateCart("1", "0001"),
            CartFactory.CreateCart("2", "0002"));
        container.AssignItem(BarcodeA, maxQuantity: 1);
        container.AssignItem(BarcodeB, maxQuantity: 1);

        container.ResetAll();

        var baskets = container.GetCarts().SelectMany(c => c.GetBaskets);
        Assert.All(baskets, basket => Assert.True(basket.IsEmpty));
    }

    [Fact]
    public void Rollback_OfNewItem_EmptiesTheBasket()
    {
        var container = CartFactory.CreateSingleCartContainer("0001");
        var assignment = container.AssignItem(BarcodeA, maxQuantity: 3);

        container.Rollback(assignment);

        var basket = container.GetCarts().Single().GetBaskets.Single();
        Assert.True(basket.IsEmpty);
        Assert.Equal(0, basket.MaxQuantity);
    }

    [Fact]
    public void Rollback_OfExistingItem_RemovesOnlyTheLastItem()
    {
        var container = CartFactory.CreateSingleCartContainer("0001");
        container.AssignItem(BarcodeA, maxQuantity: 3);
        var assignment = container.AssignItem(BarcodeA, maxQuantity: 3);

        container.Rollback(assignment);

        var basket = container.GetCarts().Single().GetBaskets.Single();
        Assert.Equal(1, basket.CurrentQuantity);
        Assert.Equal(BarcodeA, basket.Barcode);
    }

    [Fact]
    public void Rollback_OfFailedAssignment_DoesNothing()
    {
        var container = CartFactory.CreateSingleCartContainer("0001");
        container.AssignItem(BarcodeA, maxQuantity: 1);
        var failed = container.AssignItem(BarcodeB, maxQuantity: 1);

        container.Rollback(failed);

        Assert.Equal(1, container.GetCarts().Single().GetBaskets.Single().CurrentQuantity);
    }

    [Fact]
    public void Rollback_OfAssignmentFromUnknownCart_DoesNothing()
    {
        var container = CartFactory.CreateSingleCartContainer("0001");
        container.AssignItem(BarcodeA, maxQuantity: 3);

        var foreignCart = CartFactory.CreateCart("99", "0099");
        var foreignAssignment = foreignCart.TryAddItem(BarcodeA, maxQuantity: 3);

        container.Rollback(foreignAssignment);

        Assert.Equal(1, container.GetCarts().Single().GetBaskets.Single().CurrentQuantity);
    }

    [Fact]
    public void GetStatusString_ShowsEmptyAndOccupiedBaskets()
    {
        var container = CartFactory.CreateSingleCartContainer("0001", "0002");
        container.AssignItem(BarcodeA, maxQuantity: 3);

        var status = container.GetStatusString();

        Assert.Contains("Carrello 1:", status);
        Assert.Contains($"Basket 0001: [ {BarcodeA} ] 1/3", status);
        Assert.Contains("Basket 0002: VUOTO", status);
    }

    [Fact]
    public void GetCarts_ReturnsConfiguredCarts()
    {
        var container = CartFactory.CreateContainer(
            CartFactory.CreateCart("1", "0001"),
            CartFactory.CreateCart("2", "0002"));

        Assert.Equal(["1", "2"], container.GetCarts().Select(c => c.CartId));
    }
}
