namespace PtlController.Input;


public interface IBarcodeInputService
{
    Task<string?> ReadInputAsync(CancellationToken cancellationToken = default);
}
