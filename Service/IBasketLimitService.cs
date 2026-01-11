namespace PtlOrchestrator.Service;

public interface IBasketLimitService
{
    void Load(string csvPath, CancellationToken cancellationToken);
    int GetMaxFor(string barcode);
    bool HasMaxLimit(string barcode);
}