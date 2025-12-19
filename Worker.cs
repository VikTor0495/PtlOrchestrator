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
    IHostApplicationLifetime appLifetime) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ICartManager _cartManager = cartManager ?? throw new ArgumentNullException(nameof(cartManager));
    private readonly IBarcodeInputService _barcodeInput = barcodeInput ?? throw new ArgumentNullException(nameof(barcodeInput));
    private readonly IHostApplicationLifetime _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("╔═══════════════════════════════════════════════╗");
        _logger.LogInformation("║   BARCODE CART MANAGER - WORKER SERVICE      ║");
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
        _logger.LogInformation("");
        _logger.LogInformation("Comandi disponibili:");
        _logger.LogInformation("  - Scansiona un barcode per assegnare al carrello");
        _logger.LogInformation("  - 'status' - Mostra stato carrelli");
        _logger.LogInformation("  - 'reset' - Reset di tutti i carrelli");
        _logger.LogInformation("  - 'exit' o 'quit' - Esci dal programma");
        _logger.LogInformation("");
        _logger.LogInformation("Pronto per ricevere barcode...");
        _logger.LogInformation(new string('─', 50));
        _logger.LogInformation("");

        try
        {
            // Loop principale di lettura input
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Leggi input (barcode o comando)
                    var input = await _barcodeInput.ReadInputAsync(stoppingToken);

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    // Gestione comandi speciali
                    if (await HandleSpecialCommandAsync(input, stoppingToken))
                        continue;

                    // Processa il barcode
                    await _cartManager.ProcessBarcodeAsync(input, stoppingToken);
                    _logger.LogInformation(""); // Riga vuota per separare
                }
                catch (OperationCanceledException)
                {
                    // Shutdown richiesto
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
                _logger.LogInformation("");
                _logger.LogInformation("Comandi disponibili:");
                _logger.LogInformation("  - Scansiona un barcode per assegnare al carrello");
                _logger.LogInformation("  - 'status' - Mostra stato carrelli");
                _logger.LogInformation("  - 'reset' - Reset di tutti i carrelli");
                _logger.LogInformation("  - 'exit' - Esci dal programma");
                _logger.LogInformation("");
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
}
