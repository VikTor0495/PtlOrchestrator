namespace PtlOrchestrator.Domain;

public sealed record BarcodeLimit(
    string Barcode,
    int MaxQuantity
);
