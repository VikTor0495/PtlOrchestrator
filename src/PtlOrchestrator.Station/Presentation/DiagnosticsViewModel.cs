using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Service;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Sync;

namespace PtlOrchestrator.Station.Presentation;

/// <summary>Strumenti per chi gestisce la postazione: sincronizzazione forzata, report e azzeramento dei carrelli.</summary>
public sealed partial class DiagnosticsViewModel(
    EventSyncService eventSync,
    OperatorSyncService operatorSync,
    IOutbox outbox,
    IOperatorCache operatorCache,
    ICartManager cartManager,
    ILightstepConnectionService lightstep,
    IAppProcessingState processingState,
    IUiDispatcher dispatcher,
    ILogger<DiagnosticsViewModel> logger) : ObservableObject
{
    private readonly EventSyncService _eventSync = eventSync;
    private readonly OperatorSyncService _operatorSync = operatorSync;
    private readonly IOutbox _outbox = outbox;
    private readonly IOperatorCache _operatorCache = operatorCache;
    private readonly ICartManager _cartManager = cartManager;
    private readonly ILightstepConnectionService _lightstep = lightstep;
    private readonly IAppProcessingState _processingState = processingState;
    private readonly IUiDispatcher _dispatcher = dispatcher;
    private readonly ILogger<DiagnosticsViewModel> _logger = logger;

    [ObservableProperty]
    private int _pendingEvents;

    [ObservableProperty]
    private int _quarantinedEvents;

    [ObservableProperty]
    private string _lastOperatorSync = "mai";

    [ObservableProperty]
    private bool _isPtlConnected;

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncNowCommand), nameof(WriteReportCommand))]
    private bool _isWorking;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var pending = await _outbox.CountPendingAsync(cancellationToken);
        var quarantined = await _outbox.CountQuarantinedAsync(cancellationToken);
        var lastSync = await _operatorCache.GetLastSyncAsync(cancellationToken);
        var ptlConnected = _lightstep.IsConnected;

        _dispatcher.Post(() =>
        {
            PendingEvents = pending;
            QuarantinedEvents = quarantined;
            IsPtlConnected = ptlConnected;
            LastOperatorSync = lastSync is null ? "mai" : lastSync.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SyncNowAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            var delivered = await _eventSync.SyncOnceAsync(cancellationToken);
            var operators = await _operatorSync.SyncOnceAsync(cancellationToken);
            await RefreshAsync(cancellationToken);

            var events = $"Eventi inviati: {delivered}" + (PendingEvents > 0 ? $" (ancora {PendingEvents} in coda)" : string.Empty);
            var operatorsText = operators is null ? "server non raggiungibile" : $"operatori aggiornati: {operators}";

            SetResult($"{events}; {operatorsText}");
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task WriteReportAsync()
    {
        await RunAsync(() =>
        {
            _cartManager.WriteCsvReport();
            SetResult("Report CSV scritto");
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Scrive il report, riconnette il PTL e riporta a zero carrelli e lampade (come il vecchio comando "reset").
    /// Non parte mentre è in corso un'operazione. Lo scarto delle scansioni durante l'azzeramento è garantito dallo stato "occupato".
    /// </summary>
    public async Task ResetCartsAsync(CancellationToken cancellationToken)
    {
        if (!_processingState.TryEnter())
        {
            SetResult("Operazione in corso: riprova tra qualche istante");
            return;
        }

        IsWorking = true;

        try
        {
            _cartManager.WriteCsvReport();

            _lightstep.Disconnect();
            await _lightstep.EnsureConnectedAsync(cancellationToken);

            await _cartManager.ResetAll(cancellationToken);

            SetResult("Carrelli azzerati, report scritto");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azzeramento dei carrelli non riuscito");
            SetResult($"Azzeramento non riuscito: {ex.Message}");
        }
        finally
        {
            _processingState.Exit();
            IsWorking = false;
        }
    }

    private bool CanRun() => !IsWorking;

    private async Task RunAsync(Func<Task> work)
    {
        IsWorking = true;

        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operazione di diagnostica non riuscita");
            SetResult($"Operazione non riuscita: {ex.Message}");
        }
        finally
        {
            IsWorking = false;
        }
    }

    private void SetResult(string text) => _dispatcher.Post(() => Result = text);
}
