using PtlOrchestrator.Service;
using PtlOrchestrator.Input;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PtlOrchestrator;

public class Worker(
    ICartManager cartManager,
    IBarcodeInputService barcodeInput,
    IHostApplicationLifetime appLifetime,
    ILightstepConnectionService lightstepConnectionService,
    ILogger<Worker> logger) : BackgroundService
{

    private readonly ICartManager _cartManager = cartManager ?? throw new ArgumentNullException(nameof(cartManager));
    private readonly IBarcodeInputService _barcodeInput = barcodeInput ?? throw new ArgumentNullException(nameof(barcodeInput));
    private readonly IHostApplicationLifetime _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
    private readonly ILightstepConnectionService _connectionService = lightstepConnectionService ?? throw new ArgumentNullException(nameof(lightstepConnectionService));
    private readonly ILogger<Worker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PTL Worker starting");

            await _connectionService.EnsureConnectedAsync(cancellationToken);

            _cartManager.ResetAll();

            ShowStartupLogs();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {

                    var input = await _barcodeInput.ReadInputAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(input) || await HandleSpecialCommandAsync(input, cancellationToken))
                        continue;

                    var result = _cartManager.ProcessBarcode(input);

                    if (!result.Success)
                        continue;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante elaborazione input barcode: {Message}", ex.Message);
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
                _logger.LogInformation("Richiesta terminazione applicazione...");
                _appLifetime.StopApplication();
                return true;

            case "status":
                _logger.LogInformation("Richiesta stato carrelli...");
                _cartManager.ShowStatus();
                return true;
            case "reset":
                await HandleResetConsoleMessageAsync(cancellationToken);
                _cartManager.WriteCsvReport();
                _cartManager.ResetAll();
                return true;

            case "help":
            case "?":
                ShowStartupLogs();
                return true;

            default:
                return false;
        }
    }

    private static void ShowStartupLogs()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════╗");
        Console.WriteLine("║   BARCODE CART MANAGER - WORKER SERVICE       ║");
        Console.WriteLine("╚═══════════════════════════════════════════════╝");
        Console.WriteLine("");
        Console.WriteLine("Sistema inizializzato correttamente\n");
        Console.WriteLine("Comandi disponibili:\n");
        Console.WriteLine("- 'status' - Mostra stato carrelli\n");
        Console.WriteLine("- 'reset' - Reset di tutti i carrelli (Genera report)\n");
        Console.WriteLine("- 'exit/quit' - Esci dal programma (Genera report)\n");
        Console.WriteLine("Pronto per ricevere input!\n");
        Console.WriteLine(new string('─', 50));
        Console.WriteLine("");

    }

    private async Task HandleResetConsoleMessageAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Confermi reset? (s/n):");
        var confirmation = await _barcodeInput.ReadInputAsync(cancellationToken);
        if (confirmation?.ToLower() is "s" or "si" or "y" or "yes")
        {
            _logger.LogInformation("Reset completato");
        }
        else
        {
            _logger.LogInformation("Reset annullato");
        }
    }

}
