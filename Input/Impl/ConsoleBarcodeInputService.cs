using Microsoft.Extensions.Logging;

namespace PtlController.Input.Impl;

/// <summary>
/// Servizio di input da Console per lettura barcode
/// </summary>
public class ConsoleBarcodeInputService : IBarcodeInputService
{
    private readonly ILogger<ConsoleBarcodeInputService> _logger;

    public ConsoleBarcodeInputService(ILogger<ConsoleBarcodeInputService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<string?> ReadInputAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                // Mostra prompt
                Console.Write("Barcode > ");
                
                // Leggi input con supporto per cancellation
                string? input = null;
                var inputTask = Task.Run(() => Console.ReadLine());
                
                // Attendi input o cancellation
                while (!inputTask.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    Task.Delay(100, cancellationToken).Wait(cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Lettura input cancellata");
                    throw new OperationCanceledException(cancellationToken);
                }

                input = inputTask.Result;
                return input?.Trim();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante lettura input");
                return null;
            }
        }, cancellationToken);
    }
}
