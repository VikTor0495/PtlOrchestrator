namespace PtlController.Configuration;

public sealed class CartOptions
{
    public int CartId { get; set; }
    public List<BasketOptions> Baskets { get; set; } = [];
}
