using PtlController.Service;
using PtlController.Input;
using PtlController.Domain;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PtlController;

public class Worker(
    ILogger<Worker> logger,
    ICartManager cartManager,
    IBarcodeInputService barcodeInput,
    IHostApplicationLifetime appLifetime,
    LightstepConnectionService lightstepConnectionService) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ICartManager _cartManager = cartManager ?? throw new ArgumentNullException(nameof(cartManager));
    private readonly IBarcodeInputService _barcodeInput = barcodeInput ?? throw new ArgumentNullException(nameof(barcodeInput));
    private readonly IHostApplicationLifetime _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
    private readonly LightstepConnectionService _connectionService = lightstepConnectionService ?? throw new ArgumentNullException(nameof(lightstepConnectionService));

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        ShowStartupLogs();

        try
        {
            _logger.LogInformation("PTL Worker starting");

            await ConnectToControllerAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                 
                    var input = await _barcodeInput.ReadInputAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    if (await HandleSpecialCommandAsync(input, cancellationToken))
                        continue;

                    var result = _cartManager.ProcessBarcodeAsync(input, cancellationToken);

                    if (!result.Success)
                    {
                        // TODO: segnale errore operatore
                        continue;
                    }
                    var cartId = result.Cart!.CartId;
                  

                    // QUI: futuro PP5(module)
                    _logger.LogInformation(
                        "Accendere modulo PTL {Module} per carrello {CartId}",
                        module,
                        cartId);
                    
                    _logger.LogInformation(""); // Riga vuota per separare
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Errore durante elaborazione input barcode: {Message}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Errore critico nel Worker");
            throw;
        }
        finally
        {
            _logger.LogInformation("Worker terminato");
            _connectionService.Disconnect();
        }
    }


    private async Task<bool> HandleSpecialCommandAsync(string input, CancellationToken cancellationToken)
    {
        switch (input.ToLower().Trim())
        {
            case "exit":
            case "quit":
            case "q":
                _logger.LogWarning("Richiesta terminazione applicazione...");
                _appLifetime.StopApplication();
                return true;

            case "status":
                _cartManager.ShowStatus();
                return true;

            case "reset":
                _logger.LogWarning("Richiesta reset carrelli...");
                _logger.LogInformation("Confermi reset? (s/n):");
                
                var confirmation = await _barcodeInput.ReadInputAsync(cancellationToken);
                if (confirmation?.ToLower() is "s" or "si" or "y" or "yes")
                {
                    _cartManager.ResetAll();
                    _logger.LogInformation("Reset completato");
                }
                else
                {
                    _logger.LogInformation("Reset annullato");
                }
                return true;

            case "help":
            case "?":
                ShowStartupLogs();
                return true;

            default:
                return false; 
        }
    }

    private void ShowStartupLogs() 
    {
        _logger.LogInformation("╔═══════════════════════════════════════════════╗");
        _logger.LogInformation("║   BARCODE CART MANAGER - WORKER SERVICE       ║");
        _logger.LogInformation("╚═══════════════════════════════════════════════╝");
        _logger.LogInformation("");
        _logger.LogInformation("Sistema inizializzato correttamente");
        _logger.LogInformation("\nComandi disponibili:\n- Scansiona un barcode per assegnare al carrello\n- 'status' - Mostra stato carrelli\n- 'reset' - Reset di tutti i carrelli\n- 'exit' - Esci dal programma\n");
        _logger.LogInformation("Pronto per ricevere barcode...");
        _logger.LogInformation(new string('─', 50));
        _logger.LogInformation("");
        
    }

    private async Task ConnectToControllerAsync(CancellationToken cancellationToken)
    {
        await _connectionService.EnsureConnectedAsync(
            TimeSpan.FromSeconds(10),
            cancellationToken);
    }

}
