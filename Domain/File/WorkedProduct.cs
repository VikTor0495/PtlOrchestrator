namespace PtlOrchestrator.Domain.File;

public sealed record WorkedProduct(
    DateTime Timestamp,
    string CartId,
    string BasketId,
    string Barcode,
    int Quantity
);
