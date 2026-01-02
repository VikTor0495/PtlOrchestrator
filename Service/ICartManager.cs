namespace PtlController.Service;

using PtlController.Domain;


public interface ICartManager
{
    CartAssignmentResult ProcessBarcode(string barcode);

    void ResetAll();

    void ShowStatus();

}
