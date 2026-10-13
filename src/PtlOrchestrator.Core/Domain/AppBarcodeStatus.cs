using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Domain;

public sealed class AppBarcodeStatus(string barcode, AppState state)
{
    
    public string Barcode { get; } = barcode;

    public AppState State { get; set; } = state;
}