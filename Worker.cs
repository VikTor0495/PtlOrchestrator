using PtlOrchestrator.Service;
using PtlOrchestrator.Input;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PtlOrchestrator;

public sealed class Worker : BackgroundService
{
    private readonly ICartManager _cartManager;
    private readonly IBarcodeInputService _barcodeInput;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILightstepConnectionService _connectionService;
    private readonly IBasketLimitService _basketLimitService;
    private readonly IOptionsMonitor<BarcodeLimitOptions> _optionsMonitor;
    private readonly ILogger<Worker> _logger;

    private string _currentCsvPath = string.Empty;

    public Worker(
        ICartManager cartManager,
        IBarcodeInputService barcodeInput,
        IHostApplicationLifetime appLifetime,
        ILightstepConnectionService lightstepConnectionService,
        IBasketLimitService basketLimitService,
        IOptionsMonitor<BarcodeLimitOptions> optionsMonitor,
        ILogger<Worker> logger)
    {
        _cartManager = cartManager;
        _barcodeInput = barcodeInput;
        _appLifetime = appLifetime;
        _connectionService = lightstepConnectionService;
        _basketLimitService = basketLimitService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        // 🔁 Hook reload a caldo
        _optionsMonitor.OnChange(OnBarcodeLimitOptionsChanged);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PTL Worker starting");

            await _connectionService.EnsureConnectedAsync(cancellationToken);

            LoadBasketLimits(_optionsMonitor.CurrentValue, cancellationToken);

            _cartManager.ResetAll();
            ShowStartupLogs();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var input = await _barcodeInput.ReadInputAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    if (await HandleSpecialCommandAsync(input, cancellationToken))
                        continue;

                    var result = _cartManager.ProcessBarcode(input);

                    if (!result.Success)
                    {
                        _logger.LogWarning(
                            "RIFIUTATO barcode {Barcode}: {Reason}",
                            input,
                            result.Reason);                       
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante elaborazione barcode");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker interrotto su richiesta di shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Errore critico nel Worker");
        }
        finally
        {
            _cartManager.WriteCsvReport();
            _logger.LogInformation("Worker terminato");
            _connectionService.Disconnect();
        }
    }


    private void OnBarcodeLimitOptionsChanged(BarcodeLimitOptions options)
    {
        _logger.LogInformation(
            "LimitBarcode modificate. Ricarico file limiti...");

        try
        {
            LoadBasketLimits(options, CancellationToken.None);
           // _cartManager.ResetAll(); DA FARE?
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante reload limiti barcode");
        }
    }

    private void LoadBasketLimits(
        BarcodeLimitOptions options,
        CancellationToken cancellationToken)
    {
        _currentCsvPath = Path.Combine(
            AppContext.BaseDirectory,
            options.FileName);

        _logger.LogInformation(
            "Caricamento limiti basket da {CsvPath}",
            _currentCsvPath);

        _basketLimitService.Load(_currentCsvPath, cancellationToken);
    }

    private async Task<bool> HandleSpecialCommandAsync(string input, CancellationToken cancellationToken)
    {
        switch (input.ToLowerInvariant().Trim())
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

    private void ShowStartupLogs()
    {
        var message = string.Join(Environment.NewLine,
        [
            "",
            "╔═══════════════════════════════════════════════╗",
            "║   BARCODE CART MANAGER - WORKER SERVICE       ║",
            "╚═══════════════════════════════════════════════╝",
            "",
            "Sistema inizializzato correttamente",
            "",
            "Comandi disponibili:",
            "",
            "- 'status'      - Mostra stato carrelli",
            "- 'reset'       - Reset di tutti i carrelli (Genera report)",
            "- 'exit/quit'   - Esci dal programma (Genera report)",
            "",
            "Pronto per ricevere input!",
            "",
            new string('─', 50)
        ]);

        _logger.LogInformation("{StartupMessage}", message);
    }

    private async Task HandleResetConsoleMessageAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Confermi reset? (s/n):");

        var confirmation = await _barcodeInput.ReadInputAsync(cancellationToken);

        if (confirmation?.ToLowerInvariant() is "s" or "si" or "y" or "yes")
            _logger.LogInformation("Reset completato");
        else
            _logger.LogInformation("Reset annullato");
    }
}
