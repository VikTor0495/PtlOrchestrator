using PtlOrchestrator.Service;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Configuration;

using Microsoft.Extensions.Options;
using System.Threading.Channels;


namespace PtlOrchestrator;

public sealed class Worker(
    ICartManager cartManager,
    IHostApplicationLifetime appLifetime,
    ILightstepConnectionService lightstepConnectionService,
    IBasketLimitService basketLimitService,
    IOptions<BarcodeLimitOptions> optionsMonitor,
    IAppProcessingState appProcessingState,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly ICartManager _cartManager = cartManager;
    private readonly IHostApplicationLifetime _appLifetime = appLifetime;
    private readonly ILightstepConnectionService _connectionService = lightstepConnectionService;
    private readonly IBasketLimitService _basketLimitService = basketLimitService;
    private readonly IOptions<BarcodeLimitOptions> _optionsMonitor = optionsMonitor;
    private readonly IAppProcessingState _appProcessingState = appProcessingState;
    private readonly ILogger<Worker> _logger = logger;
    private int firstBarcode = 0;

    private string _currentCsvPath = string.Empty;

    private readonly Channel<string> _barcodeChannel =
        Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });

    private async Task ReadInputLoop(CancellationToken ct)
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
                if (await HandleSpecialCommandAsync(input, ct))
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

                var result = await _cartManager.ProcessBarcode(barcode, ct);

                if (!result.Success)
                {
                    _logger.LogWarning("RIFIUTATO: {Reason}", result.Reason);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Processamento barcode '{Barcode}' cancellato", barcode);
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

            LoadBasketLimits(_optionsMonitor.Value, cancellationToken);

            await _cartManager.ResetAll(cancellationToken);

            ShowStartupLogs();

            var inputTask = Task.Run(() => ReadInputLoop(cancellationToken), cancellationToken);
            var processTask = Task.Run(() => ProcessLoop(cancellationToken), cancellationToken);

            await Task.WhenAll(inputTask, processTask);
        }
        catch (OperationCanceledException)
        {
            await _cartManager.ResetAll(cancellationToken);
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
            await _cartManager.ResetAll(cancellationToken);
            _logger.LogInformation("Worker terminato");
            _connectionService.Disconnect();
        }
    }


    private void LoadBasketLimits(
        BarcodeLimitOptions options,
        CancellationToken cancellationToken)
    {
        _currentCsvPath = Path.Combine(
            AppContext.BaseDirectory,
            options.FileName);

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
                _cartManager.WriteCsvReport();
                _connectionService.Disconnect();
                _appLifetime.StopApplication();
                return true;

            case "status":
                _logger.LogInformation("Richiesta stato carrelli...");
                _cartManager.ShowStatus();
                return true;

            case "reset":
                await HandleResetApp(cancellationToken);
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
            new string('-', 50),
            "",
            "╔═══════════════════════════════════════════════╗",
            "║           PUT-TO-LIGHT ORCHESTRATOR           ║",
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
            new string('─', 50)
        ]);

        _logger.LogInformation("{StartupMessage}", message);
    }

    private async Task<bool> HandleResetApp(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Confermi reset? (s/N):");

        var confirmation = Console.ReadLine();

        if (confirmation?.ToLowerInvariant() is "s" or "si" or "y" or "yes")
        {
            _cartManager.WriteCsvReport();

            _connectionService.Disconnect();
            await _connectionService.EnsureConnectedAsync(cancellationToken);

            await _cartManager.ResetAll(cancellationToken);
            _logger.LogInformation("Reset completato");
            return true;
        }
        else
            _logger.LogInformation("Reset annullato");
        return false;
    }
}
