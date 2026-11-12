using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Service;
using PtlOrchestrator.Station.Storage;

namespace PtlOrchestrator.Station.Presentation;

/// <summary>
/// Stato mostrato nella finestra principale: operatore in turno, input manuale dei codici,
/// carrelli attivi, ultima scansione e salute del sistema (PTL, coda verso il server).
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);

    private readonly IOperatorSessionContext _sessionContext;
    private readonly IOperatorLoginService _loginService;
    private readonly IScanDispatcher _scanDispatcher;
    private readonly ICartManager _cartManager;
    private readonly IOutbox _outbox;
    private readonly ILightstepConnectionService _lightstep;
    private readonly CartContainer _cartContainer;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IScanActivity _scanActivity;
    private readonly CancellationTokenSource _monitorCts = new();

    private Task? _monitorTask;
    private bool _disposed;

    public MainViewModel(
        IScanActivity scanActivity,
        IOperatorSessionContext sessionContext,
        IOperatorLoginService loginService,
        IScanDispatcher scanDispatcher,
        ICartManager cartManager,
        IOutbox outbox,
        ILightstepConnectionService lightstep,
        CartContainer cartContainer,
        IOptions<StationOptions> stationOptions,
        IUiDispatcher dispatcher,
        TimeProvider timeProvider,
        ILogger<MainViewModel> logger)
    {
        _scanActivity = scanActivity;
        _sessionContext = sessionContext;
        _loginService = loginService;
        _scanDispatcher = scanDispatcher;
        _cartManager = cartManager;
        _outbox = outbox;
        _lightstep = lightstep;
        _cartContainer = cartContainer;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _logger = logger;

        StationId = stationOptions.Value.StationId;
        Carts = CartSnapshot.From(cartContainer);
        ApplySession(sessionContext.Current);

        _scanActivity.ScanProcessed += OnScanProcessed;
    }

    public string StationId { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCarts), nameof(NoCarts), nameof(CartColumns))]
    private IReadOnlyList<CartSnapshot> _carts;

    public bool HasCarts => Carts.Count > 0;

    public bool NoCarts => !HasCarts;

    /// <summary>Colonne della griglia carrelli: 1, 2 o 3 carrelli in fila, 4 in una griglia 2x2.</summary>
    public int CartColumns => Carts.Count switch
    {
        0 or 1 => 1,
        2 => 2,
        3 => 3,
        _ => 2
    };

    /// <summary>Richiesta di un popup per l'operatore (per ora: limite di carrelli superato).</summary>
    public event EventHandler<string>? PopupRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoggedOut))]
    [NotifyCanExecuteChangedFor(nameof(LogoutCommand), nameof(CloseWorkCommand))]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string? _operatorName;

    [ObservableProperty]
    private DateTimeOffset? _sessionStartedAt;

    [ObservableProperty]
    private string? _lastScanCode;

    [ObservableProperty]
    private OperatorMessage _lastMessage = new(MessageSeverity.Info, "Inserisci il codice operatore o scansiona il badge");

    [ObservableProperty]
    private int _pendingEvents;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuarantinedEvents))]
    private int _quarantinedEvents;

    [ObservableProperty]
    private bool _isPtlConnected;

    /// <summary>Codice digitato a mano, con lo stesso effetto di una scansione: operatore, carrello o prodotto.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCodeCommand))]
    private string _codeInput = string.Empty;

    public bool IsLoggedOut => !IsLoggedIn;

    public bool HasQuarantinedEvents => QuarantinedEvents > 0;

    /// <summary>Avvia l'aggiornamento periodico di stato (coda, PTL).</summary>
    public void StartMonitoring()
        => _monitorTask ??= Task.Run(() => MonitorAsync(_monitorCts.Token));

    /// <summary>Rilegge dai servizi lo stato che cambia anche senza scansioni.</summary>
    public async Task RefreshStatusAsync(CancellationToken cancellationToken)
    {
        var pending = await _outbox.CountPendingAsync(cancellationToken);
        var quarantined = await _outbox.CountQuarantinedAsync(cancellationToken);
        var ptlConnected = _lightstep.IsConnected;

        _dispatcher.Post(() =>
        {
            PendingEvents = pending;
            QuarantinedEvents = quarantined;
            IsPtlConnected = ptlConnected;
        });
    }

    [RelayCommand(CanExecute = nameof(CanSubmitCode))]
    private async Task SubmitCodeAsync(CancellationToken cancellationToken)
    {
        var code = CodeInput.Trim();
        CodeInput = string.Empty;

        await ProcessCodeAsync(code, cancellationToken);
    }

    /// <summary>Check-in di un carrello dal dialogo: stesso percorso di una scansione.</summary>
    public Task CheckInCartAsync(string cartCode, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(cartCode)
            ? Task.CompletedTask
            : ProcessCodeAsync(cartCode.Trim(), cancellationToken);

    private async Task ProcessCodeAsync(string code, CancellationToken cancellationToken)
    {
        // fuori dal thread della finestra: la DLL del PTL blocca il thread che la chiama e può rimandare callback al suo contesto
        var outcome = await Task.Run(() => _scanDispatcher.DispatchAsync(code, cancellationToken), cancellationToken);

        _dispatcher.Post(() =>
        {
            LastScanCode = code;
            Carts = CartSnapshot.From(_cartContainer);
            ApplySession(_sessionContext.Current);

            LastMessage = ScanMessageFormatter.Format(code, outcome) ?? LastMessage;
            RaisePopupIfNeeded(outcome);
        });
    }

    private void RaisePopupIfNeeded(ScanOutcome outcome)
    {
        if (outcome.Kind == ScanOutcomeKind.CartLimitReached)
            PopupRequested?.Invoke(this, outcome.Message ?? "Limite di carrelli raggiunto");
    }

    private bool CanSubmitCode() => CodeInput.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(IsLoggedIn))]
    private async Task LogoutAsync(CancellationToken cancellationToken)
    {
        // non si esce con prodotti nei carrelli: il lavoro va chiuso prima (report e azzeramento)
        if (_cartManager.HasItems())
        {
            _dispatcher.Post(() => LastMessage = new(
                MessageSeverity.Warning,
                "Ci sono prodotti nei carrelli: premi Reset prima di uscire"));
            return;
        }

        // i carrelli del turno tornano disponibili per le altre postazioni
        foreach (var cartId in _cartManager.ActiveCartIds())
        {
            var release = await Task.Run(() => _cartManager.ReleaseCartAsync(cartId, cancellationToken), cancellationToken);
            if (!release.Success)
            {
                _dispatcher.Post(() => LastMessage = new(MessageSeverity.Error, release.Message));
                return;
            }
        }

        var result = await _loginService.LogoutAsync(cancellationToken);

        _dispatcher.Post(() =>
        {
            if (result == LogoutResult.Done)
                ApplySession(null);

            Carts = CartSnapshot.From(_cartContainer);
            LastMessage = new(
                result == LogoutResult.Done ? MessageSeverity.Info : MessageSeverity.Warning,
                ScanMessageFormatter.FormatLogoutResult(result));
        });
    }

    [RelayCommand(CanExecute = nameof(IsLoggedIn))]
    private async Task CloseWorkAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(() => _cartManager.CloseWorkAsync(cancellationToken), cancellationToken);

            _dispatcher.Post(() =>
            {
                Carts = CartSnapshot.From(_cartContainer);
                LastMessage = new(MessageSeverity.Success, "Reset eseguito: report generato e carrelli azzerati");
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Chiusura dello sventagliamento non riuscita");
            _dispatcher.Post(() => LastMessage = new(MessageSeverity.Error, $"Reset non riuscito: {ex.Message}"));
        }
    }

    /// <summary>Rimuove dal turno un carrello vuoto: torna disponibile per le altre postazioni.</summary>
    [RelayCommand]
    private async Task RemoveCartAsync(string? cartId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(cartId))
            return;

        var outcome = await Task.Run(() => _cartManager.ReleaseCartAsync(cartId, cancellationToken), cancellationToken);

        _dispatcher.Post(() =>
        {
            Carts = CartSnapshot.From(_cartContainer);
            LastMessage = new(outcome.Success ? MessageSeverity.Success : MessageSeverity.Warning, outcome.Message);
        });
    }

    private void OnScanProcessed(object? sender, ScanProcessedEventArgs e)
    {
        // chiamato dal thread che elabora le scansioni, subito prima della scansione successiva:
        // lo stato di dominio qui è coerente, quindi se ne scatta la fotografia prima di passare alla UI
        var message = ScanMessageFormatter.Format(e.ScannedCode, e.Outcome);
        var carts = CartSnapshot.From(_cartContainer);
        var session = _sessionContext.Current;

        _dispatcher.Post(() =>
        {
            LastScanCode = e.ScannedCode;
            Carts = carts;
            ApplySession(session);

            if (message is not null)
                LastMessage = message;

            RaisePopupIfNeeded(e.Outcome);
        });
    }

    private void ApplySession(OperatorSession? session)
    {
        IsLoggedIn = session is not null;
        OperatorName = session?.OperatorName;
        SessionStartedAt = session?.StartedAt;
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshStatusAsync(cancellationToken);
                await Task.Delay(MonitorInterval, _timeProvider, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Aggiornamento dello stato non riuscito");

                try
                {
                    await Task.Delay(MonitorInterval, _timeProvider, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _scanActivity.ScanProcessed -= OnScanProcessed;
        _monitorCts.Cancel();
        _monitorCts.Dispose();
    }
}
