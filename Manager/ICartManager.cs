namespace PtlOrchestrator.Manager;

using PtlOrchestrator.Domain;


public interface ICartManager
{
    CartAssignmentResult ProcessBarcode(string barcode, CancellationToken cancellationToken);

    void ResetAll(CancellationToken cancellationToken);

    void ShowStatus();
    
    void WriteCsvReport();
}
