using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Manager;

namespace PtlOrchestrator.Service.Impl;

/// <summary>
/// Prepara il PTL all'avvio (connessione, limiti barcode, azzeramento dei carrelli) e lo
/// riconsegna in ordine allo spegnimento (report CSV, azzeramento, disconnessione).
/// Finché non è pronto il sistema risulta occupato, quindi le scansioni vengono scartate.
/// </summary>
public sealed class PtlLifecycleService(
    ILightstepConnectionService connectionService,
    IBasketLimitService basketLimitService,
    ICartManager cartManager,
    IAppProcessingState appProcessingState,
    IOptions<BarcodeLimitOptions> limitOptions,
    TimeProvider timeProvider,
    ILogger<PtlLifecycleService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly ILightstepConnectionService _connectionService = connectionService;
    private readonly IBasketLimitService _basketLimitService = basketLimitService;
    private readonly ICartManager _cartManager = cartManager;
    private readonly IAppProcessingState _appProcessingState = appProcessingState;
    private readonly BarcodeLimitOptions _limitOptions = limitOptions.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<PtlLifecycleService> _logger = logger;

    private volatile bool _isReady;

    /// <summary>Vero quando il PTL è connesso, i limiti sono caricati e i carrelli sono azzerati.</summary>
    public bool IsReady => _isReady;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var markedBusy = _appProcessingState.TryEnter();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await InitializeAsync(stoppingToken);
                    _isReady = true;
                    _logger.LogInformation("PTL pronto");
                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Inizializzazione del PTL non riuscita, nuovo tentativo tra {Seconds}s", RetryDelay.TotalSeconds);
                    await Task.Delay(RetryDelay, _timeProvider, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // arresto durante l'attesa del nuovo tentativo
        }
        finally
        {
            if (markedBusy)
                _appProcessingState.Exit();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        TryRun("scrittura del report CSV", _cartManager.WriteCsvReport);

        if (_connectionService.IsConnected)
        {
            try
            {
                await _cartManager.ResetAll(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azzeramento dei carrelli allo spegnimento non riuscito");
            }
        }

        TryRun("disconnessione dal PTL", _connectionService.Disconnect);
        _isReady = false;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _connectionService.EnsureConnectedAsync(cancellationToken);

        _basketLimitService.Load(Path.Combine(AppContext.BaseDirectory, _limitOptions.FileName), cancellationToken);

        await _cartManager.ResetAll(cancellationToken);
    }

    private void TryRun(string description, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante {Operation}", description);
        }
    }
}
