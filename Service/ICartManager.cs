namespace PtlOrchestrator.Service;

using PtlOrchestrator.Domain;


public interface ICartManager
{
    CartAssignmentResult ProcessBarcode(string barcode);

    void ResetAll();

    void ShowStatus();

}
