using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Service;
using PtlOrchestrator.Station.Storage;

namespace PtlOrchestrator.Station.Presentation;

/// <summary>
/// Stato mostrato nella finestra principale: operatore in turno, ultima scansione, carrelli
/// e salute del sistema (PTL, coda verso il server).
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);

    private readonly IOperatorSessionContext _sessionContext;
    private readonly IOperatorLoginService _loginService;
    private readonly IOperatorDirectory _operatorDirectory;
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
        IOperatorDirectory operatorDirectory,
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
        _operatorDirectory = operatorDirectory;
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

    public ObservableCollection<OperatorDto> AvailableOperators { get; } = [];

    [ObservableProperty]
    private IReadOnlyList<CartSnapshot> _carts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoggedOut))]
    [NotifyCanExecuteChangedFor(nameof(LogoutCommand), nameof(LoginWithPinCommand))]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string? _operatorName;

    [ObservableProperty]
    private DateTimeOffset? _sessionStartedAt;

    [ObservableProperty]
    private string? _lastScanCode;

    [ObservableProperty]
    private OperatorMessage _lastMessage = new(MessageSeverity.Info, "Scansiona il badge per iniziare");

    [ObservableProperty]
    private int _pendingEvents;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuarantinedEvents))]
    private int _quarantinedEvents;

    [ObservableProperty]
    private bool _isPtlConnected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginWithPinCommand))]
    private OperatorDto? _selectedOperator;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginWithPinCommand))]
    private string _pin = string.Empty;

    public bool IsLoggedOut => !IsLoggedIn;

    public bool HasQuarantinedEvents => QuarantinedEvents > 0;

    /// <summary>Avvia l'aggiornamento periodico di stato (coda, PTL, elenco operatori).</summary>
    public void StartMonitoring()
        => _monitorTask ??= Task.Run(() => MonitorAsync(_monitorCts.Token));

    /// <summary>Rilegge dai servizi lo stato che cambia anche senza scansioni.</summary>
    public async Task RefreshStatusAsync(CancellationToken cancellationToken)
    {
        var pending = await _outbox.CountPendingAsync(cancellationToken);
        var quarantined = await _outbox.CountQuarantinedAsync(cancellationToken);
        var operators = await _operatorDirectory.GetActiveAsync(cancellationToken);
        var ptlConnected = _lightstep.IsConnected;

        _dispatcher.Post(() =>
        {
            PendingEvents = pending;
            QuarantinedEvents = quarantined;
            IsPtlConnected = ptlConnected;
            ReplaceOperators(operators);
        });
    }

    [RelayCommand(CanExecute = nameof(CanLoginWithPin))]
    private async Task LoginWithPinAsync(CancellationToken cancellationToken)
    {
        var operatorDto = SelectedOperator!;

        var result = await _loginService.LoginWithPinAsync(operatorDto.Id, Pin, cancellationToken);

        _dispatcher.Post(() =>
        {
            Pin = string.Empty;

            if (result.Success)
            {
                ApplySession(result.Session);
                LastMessage = new(MessageSeverity.Success, $"Benvenuto {result.Session!.OperatorName}");
            }
            else
            {
                LastMessage = new(MessageSeverity.Error, ScanMessageFormatter.FormatLoginFailure(result.Failure!.Value));
            }
        });
    }

    private bool CanLoginWithPin() => !IsLoggedIn && SelectedOperator is not null && Pin.Length > 0;

    [RelayCommand(CanExecute = nameof(IsLoggedIn))]
    private async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var result = await _loginService.LogoutAsync(cancellationToken);

        _dispatcher.Post(() =>
        {
            if (result == LogoutResult.Done)
                ApplySession(null);

            LastMessage = new(
                result == LogoutResult.Done ? MessageSeverity.Info : MessageSeverity.Warning,
                ScanMessageFormatter.FormatLogoutResult(result));
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
        });
    }

    private void ApplySession(OperatorSession? session)
    {
        IsLoggedIn = session is not null;
        OperatorName = session?.OperatorName;
        SessionStartedAt = session?.StartedAt;
    }

    private void ReplaceOperators(IReadOnlyList<OperatorDto> operators)
    {
        var selectedId = SelectedOperator?.Id;

        if (AvailableOperators.Select(o => o.Id).SequenceEqual(operators.Select(o => o.Id)))
            return;

        AvailableOperators.Clear();
        foreach (var operatorDto in operators)
            AvailableOperators.Add(operatorDto);

        SelectedOperator = AvailableOperators.FirstOrDefault(o => o.Id == selectedId);
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
