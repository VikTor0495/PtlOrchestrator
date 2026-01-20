namespace PtlOrchestrator.Configuration;

public sealed class CartOptions
{
    public required string CartId { get; set; }
    public List<BasketOptions> Baskets { get; set; } = [];
}
