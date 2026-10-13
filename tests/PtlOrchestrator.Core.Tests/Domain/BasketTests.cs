using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Core.Tests.Domain;

public class BasketTests
{
    private const string Barcode = "8001234567890";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithBlankBasketId_Throws(string? basketId)
    {
        Assert.Throws<ArgumentException>(() => new Basket(basketId!, 5));
    }

    [Fact]
    public void Constructor_WithNegativeMaxQuantity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Basket("0001", -1));
    }

    [Fact]
    public void NewBasket_IsEmptyWithoutBarcode()
    {
        var basket = new Basket("0001", 5);

        Assert.Equal("0001", basket.BasketId);
        Assert.True(basket.IsEmpty);
        Assert.Null(basket.Barcode);
        Assert.Equal(0, basket.CurrentQuantity);
        Assert.Equal(5, basket.MaxQuantity);
    }

    [Fact]
    public void AddItem_OnEmptyBasket_AssignsBarcodeAndIncrementsQuantity()
    {
        var basket = new Basket("0001", 5);

        basket.AddItem(Barcode);

        Assert.Equal(Barcode, basket.Barcode);
        Assert.Equal(1, basket.CurrentQuantity);
        Assert.False(basket.IsEmpty);
    }

    [Fact]
    public void AddItem_WithSameBarcode_IncrementsQuantity()
    {
        var basket = new Basket("0001", 5);

        basket.AddItem(Barcode);
        basket.AddItem(Barcode);

        Assert.Equal(2, basket.CurrentQuantity);
    }

    [Fact]
    public void AddItem_WithDifferentBarcode_Throws()
    {
        var basket = new Basket("0001", 5);
        basket.AddItem(Barcode);

        Assert.Throws<InvalidOperationException>(() => basket.AddItem("OTHER"));
        Assert.Equal(1, basket.CurrentQuantity);
    }

    [Fact]
    public void AddItem_WhenFull_Throws()
    {
        var basket = new Basket("0001", 1);
        basket.AddItem(Barcode);

        Assert.True(basket.IsFull);
        Assert.Throws<InvalidOperationException>(() => basket.AddItem(Barcode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AddItem_WithBlankBarcode_Throws(string barcode)
    {
        var basket = new Basket("0001", 5);

        Assert.Throws<InvalidOperationException>(() => basket.AddItem(barcode));
    }

    [Fact]
    public void BasketWithZeroMaxQuantity_IsBothEmptyAndFull_AndRejectsItems()
    {
        var basket = new Basket("0001", 0);

        Assert.True(basket.IsEmpty);
        Assert.True(basket.IsFull);
        Assert.Throws<InvalidOperationException>(() => basket.AddItem(Barcode));
    }

    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    public void IsFull_ReflectsQuantityAgainstMax(int itemsToAdd, int max, bool expectedFull)
    {
        var basket = new Basket("0001", max);

        for (var i = 0; i < itemsToAdd; i++)
            basket.AddItem(Barcode);

        Assert.Equal(expectedFull, basket.IsFull);
    }

    [Fact]
    public void UpdateMaxQuantity_WithValidValue_ChangesMax()
    {
        var basket = new Basket("0001", 5);

        basket.UpdateMaxQuantity(8);

        Assert.Equal(8, basket.MaxQuantity);
    }

    [Fact]
    public void UpdateMaxQuantity_WithNegativeValue_Throws()
    {
        var basket = new Basket("0001", 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => basket.UpdateMaxQuantity(-1));
    }

    [Fact]
    public void UpdateMaxQuantity_BelowCurrentQuantity_Throws()
    {
        var basket = new Basket("0001", 5);
        basket.AddItem(Barcode);
        basket.AddItem(Barcode);

        Assert.Throws<InvalidOperationException>(() => basket.UpdateMaxQuantity(1));
        Assert.Equal(5, basket.MaxQuantity);
    }

    [Fact]
    public void RemoveItem_DecrementsQuantity()
    {
        var basket = new Basket("0001", 5);
        basket.AddItem(Barcode);
        basket.AddItem(Barcode);

        basket.RemoveItem(Barcode);

        Assert.Equal(1, basket.CurrentQuantity);
        Assert.Equal(Barcode, basket.Barcode);
        Assert.Equal(5, basket.MaxQuantity);
    }

    [Fact]
    public void RemoveItem_ToZero_ClearsBarcodeAndMaxQuantity()
    {
        var basket = new Basket("0001", 5);
        basket.AddItem(Barcode);

        basket.RemoveItem(Barcode);

        Assert.True(basket.IsEmpty);
        Assert.Null(basket.Barcode);
        Assert.Equal(0, basket.MaxQuantity);
    }

    [Fact]
    public void RemoveItem_WithDifferentBarcode_DoesNothing()
    {
        var basket = new Basket("0001", 5);
        basket.AddItem(Barcode);

        basket.RemoveItem("OTHER");

        Assert.Equal(1, basket.CurrentQuantity);
        Assert.Equal(Barcode, basket.Barcode);
    }

    [Fact]
    public void RemoveItem_OnEmptyBasket_DoesNothing()
    {
        var basket = new Basket("0001", 5);

        basket.RemoveItem(Barcode);

        Assert.Equal(0, basket.CurrentQuantity);
        Assert.Equal(5, basket.MaxQuantity);
    }

    [Fact]
    public void Reset_ClearsBarcodeQuantityAndMax()
    {
        var basket = new Basket("0001", 5);
        basket.AddItem(Barcode);
        basket.AddItem(Barcode);

        basket.Reset();

        Assert.True(basket.IsEmpty);
        Assert.Null(basket.Barcode);
        Assert.Equal(0, basket.MaxQuantity);
    }
}
