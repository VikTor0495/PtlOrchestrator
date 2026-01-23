namespace PtlOrchestrator.Manager;

using PtlOrchestrator.Domain;


public interface ICartManager
{
    Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken);

    Task ResetAll(CancellationToken cancellationToken);

    void ShowStatus();
    
    void WriteCsvReport();
}
