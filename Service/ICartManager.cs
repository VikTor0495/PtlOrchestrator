namespace PtlController.Service;


public interface ICartManager
{
    Task ProcessBarcodeAsync(string barcode, CancellationToken ct);
    void ResetAll();

}
