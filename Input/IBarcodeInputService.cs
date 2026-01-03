namespace PtlOrchestrator.Input;


public interface IBarcodeInputService
{
    Task<string?> ReadInputAsync(CancellationToken cancellationToken = default);
}
