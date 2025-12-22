using PtlController.Service;
using PtlController.Input;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PtlController;

/// <summary>
/// Worker principale che gestisce il loop di lettura barcode
/// </summary>
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("╔═══════════════════════════════════════════════╗");
        _logger.LogInformation("║   BARCODE CART MANAGER - WORKER SERVICE       ║");
        _logger.LogInformation("╚═══════════════════════════════════════════════╝");
        _logger.LogInformation("");

        // Verifica configurazione
        if (!_cartManager.IsConfigurationValid())
        {
            _logger.LogCritical("Configurazione carrelli non valida! Arresto...");
            _appLifetime.StopApplication();
            return;
        }

        _logger.LogInformation("Sistema inizializzato correttamente");
        LogsAvailableCommands();
        _logger.LogInformation("Pronto per ricevere barcode...");
        _logger.LogInformation(new string('─', 50));
        _logger.LogInformation("");

        try
        {
            _logger.LogInformation("PTL Worker starting");

            _connectionService.Connect();
            _connectionService.SendTestCommand();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                 
                    var input = await _barcodeInput.ReadInputAsync(stoppingToken);

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    if (await HandleSpecialCommandAsync(input, stoppingToken))
                        continue;

                    await _cartManager.ProcessBarcodeAsync(input, stoppingToken);
                    _logger.LogInformation(""); // Riga vuota per separare
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante elaborazione input");
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

    /// <summary>
    /// Gestisce i comandi speciali (status, reset, exit, ecc.)
    /// </summary>
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
                LogsAvailableCommands();
                return true;

            default:
                return false; // Non è un comando speciale
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutdown in corso...");
        return base.StopAsync(cancellationToken);
    }

    private void LogsAvailableCommands() 
    {
        _logger.LogInformation("\nComandi disponibili:\n- Scansiona un barcode per assegnare al carrello\n- 'status' - Mostra stato carrelli\n- 'reset' - Reset di tutti i carrelli\n- 'exit' - Esci dal programma\n");
    }
}
