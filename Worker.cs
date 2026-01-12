using PtlOrchestrator.Service;
using PtlOrchestrator.Input;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace PtlOrchestrator;

public sealed class Worker : BackgroundService
{
    private readonly ICartManager _cartManager;
    private readonly IBarcodeInputService _barcodeInput;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILightstepConnectionService _connectionService;
    private readonly IBasketLimitService _basketLimitService;
    private readonly IOptionsMonitor<BarcodeLimitOptions> _optionsMonitor;
    private readonly IAppProcessingState _appProcessingState;
    private readonly ILogger<Worker> _logger;
    private int firstBarcode = 0;

    private string _currentCsvPath = string.Empty;

    private readonly Channel<string> _barcodeChannel =
        Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite
            // oppure Wait se vuoi bloccare l'input
        });

    public Worker(
        ICartManager cartManager,
        IBarcodeInputService barcodeInput,
        IHostApplicationLifetime appLifetime,
        ILightstepConnectionService lightstepConnectionService,
        IBasketLimitService basketLimitService,
        IOptionsMonitor<BarcodeLimitOptions> optionsMonitor,
        IAppProcessingState appProcessingState,
        ILogger<Worker> logger)
    {
        _cartManager = cartManager;
        _barcodeInput = barcodeInput;
        _appLifetime = appLifetime;
        _connectionService = lightstepConnectionService;
        _basketLimitService = basketLimitService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _appProcessingState = appProcessingState;

        // 🔁 Hook reload a caldo
        _optionsMonitor.OnChange(OnBarcodeLimitOptionsChanged);
    }

    private void ReadInputLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {

                if (firstBarcode == 0)
                {
                    firstBarcode = 1;
                    _logger.LogInformation("Barcode >");
                }

                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    _logger.LogInformation("Barcode >");
                    continue;
                }

                if (_appProcessingState.IsBusy)
                {
                    _logger.LogWarning(
                        "Input '{Input}' ignorato: sistema occupato",
                        input.Trim());
                    continue;
                }

                // comandi fuori dal channel
                if (HandleSpecialCommandAsync(input, ct))
                {
                    _logger.LogInformation("Barcode >");
                    continue;
                }


                if (!_barcodeChannel.Writer.TryWrite(input.Trim()))
                {
                    _logger.LogWarning("Input scartato: sistema occupato");
                }
            }
        }
        finally
        {
            _barcodeChannel.Writer.TryComplete();
        }
    }

    private async Task ProcessLoop(CancellationToken ct)
    {
        await foreach (var barcode in _barcodeChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                if (!_appProcessingState.TryEnter())
                    continue;

                var result = _cartManager.ProcessBarcode(barcode, ct);

                if (!result.Success)
                {
                    _logger.LogWarning("RIFIUTATO: {Reason}", result.Reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore processamento barcode");
            }
            finally
            {
                _appProcessingState.Exit();
                _logger.LogInformation("Barcode > ");
            }
        }
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

            /* while (!cancellationToken.IsCancellationRequested)
            {
                try
                {

                    var input = await _barcodeInput.ReadInputAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(input) || await HandleSpecialCommandAsync(input, cancellationToken))
                        continue;

                    var result = _cartManager.ProcessBarcode(input, cancellationToken);

                    if (!result.Success)
                    {
                        _logger.LogWarning(
                            "RIFIUTATO barcode {Barcode}: {Reason}",
                            input,
                            result.Reason);
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
            } */


            var inputTask = Task.Run(() => ReadInputLoop(cancellationToken), cancellationToken);
            var processTask = Task.Run(() => ProcessLoop(cancellationToken), cancellationToken);

            await Task.WhenAll(inputTask, processTask);
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
            _barcodeChannel.Writer.TryComplete();
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

    private bool HandleSpecialCommandAsync(string input, CancellationToken cancellationToken)
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
                HandleResetApp(cancellationToken);
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

    private bool HandleResetApp(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Confermi reset? (s/n):");

        //var confirmation = await _barcodeInput.ReadInputAsync(cancellationToken);
        var confirmation = Console.ReadLine();

        if (confirmation?.ToLowerInvariant() is "s" or "si" or "y" or "yes")
        {
            _cartManager.WriteCsvReport();
            _cartManager.ResetAll();
            _logger.LogInformation("Reset completato");
            return true;
        }
        else
            _logger.LogInformation("Reset annullato");
        return false;
    }
}
