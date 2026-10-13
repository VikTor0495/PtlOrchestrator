namespace PtlOrchestrator.Configuration;

public sealed class BarcodeLimitOptions
{
    public required string BasketId { get; set; }
    public required string FileName { get; set; }
    public required int BarcodeColumnIndex { get; set; }
    public required int LimitColumnIndex { get; set; }
}
