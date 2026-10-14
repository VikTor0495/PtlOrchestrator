using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;
using PtlOrchestrator.Station.Presentation;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Presentation;

public sealed class MainViewModelTests : IDisposable
{
    private sealed class FakeScanActivity : IScanActivity
    {
        public event EventHandler<ScanProcessedEventArgs>? ScanProcessed;

        public int SubscriberCount => ScanProcessed?.GetInvocationList().Length ?? 0;

        public void Raise(string code, ScanOutcome outcome) => ScanProcessed?.Invoke(this, new ScanProcessedEventArgs(code, outcome));
    }

    private sealed class FakeLoginService(IOperatorSessionContext sessionContext) : IOperatorLoginService
    {
        public LoginResult NextLogin { get; set; } = LoginResult.Failed(LoginFailure.UnknownOperator);
        public LogoutResult NextLogout { get; set; } = LogoutResult.Done;
        public (Guid OperatorId, string Pin)? LastPinLogin { get; private set; }

        public Task<LoginResult> LoginWithBadgeAsync(string badgeCode, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<LoginResult> LoginWithPinAsync(Guid operatorId, string pin, CancellationToken cancellationToken)
        {
            LastPinLogin = (operatorId, pin);
            if (NextLogin.Success)
                sessionContext.Begin(NextLogin.Session!);

            return Task.FromResult(NextLogin);
        }

        public Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken)
        {
            if (NextLogout == LogoutResult.Done)
                sessionContext.End();

            return Task.FromResult(NextLogout);
        }
    }

    private sealed class FakeDirectory : IOperatorDirectory
    {
        public List<OperatorDto> Operators { get; } = [];

        public Task<OperatorDto?> FindByBadgeAsync(string badgeCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OperatorDto?> FindByIdAsync(Guid operatorId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<OperatorDto>> GetActiveAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<OperatorDto>>(Operators.ToList());
    }

    private readonly FakeScanActivity _scans = new();
    private readonly OperatorSessionContext _session = new();
    private readonly FakeLoginService _login;
    private readonly FakeDirectory _directory = new();
    private readonly FakeOutbox _outbox = new();
    private readonly FakeLightstep _lightstep = new();
    private readonly CartContainer _carts = new([new Cart("1", [new Basket("0001", 0), new Basket("0002", 0)])]);
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _login = new FakeLoginService(_session);
        _viewModel = new MainViewModel(
            _scans, _session, _login, _directory, _outbox, _lightstep, _carts,
            Options.Create(new StationOptions { StationId = "ST-TEST" }),
            new ImmediateDispatcher(), TimeProvider.System, NullLogger<MainViewModel>.Instance);
    }

    public void Dispose() => _viewModel.Dispose();

    private static OperatorDto Operator(string name = "Mario") => new(Guid.NewGuid(), name, "B1", "hash", true, DateTimeOffset.UtcNow);

    private static OperatorSession Session(string name = "Mario") => new(Guid.NewGuid(), Guid.NewGuid(), name, DateTimeOffset.UtcNow);

    [Fact]
    public void InitialState_IsLoggedOutWithEmptyBaskets()
    {
        Assert.Equal("ST-TEST", _viewModel.StationId);
        Assert.False(_viewModel.IsLoggedIn);
        Assert.True(_viewModel.IsLoggedOut);
        Assert.Null(_viewModel.OperatorName);
        var cart = Assert.Single(_viewModel.Carts);
        Assert.All(cart.Baskets, b => Assert.True(b.IsEmpty));
        Assert.False(_viewModel.LogoutCommand.CanExecute(null));
    }

    [Fact]
    public void BadgeLoginScan_ShowsOperatorAndGreeting()
    {
        var session = Session("Giulia");
        _session.Begin(session);

        _scans.Raise("BADGE", ScanOutcome.OperatorLoggedIn(session));

        Assert.True(_viewModel.IsLoggedIn);
        Assert.Equal("Giulia", _viewModel.OperatorName);
        Assert.Equal("BADGE", _viewModel.LastScanCode);
        Assert.Equal(MessageSeverity.Success, _viewModel.LastMessage.Severity);
        Assert.True(_viewModel.LogoutCommand.CanExecute(null));
    }

    [Fact]
    public void ProductScan_UpdatesCartSnapshotAndMessage()
    {
        _session.Begin(Session());
        var assignment = _carts.AssignItem("8000123", 3);

        _scans.Raise("8000123", ScanOutcome.ProductAccepted(assignment));

        var basket = _viewModel.Carts[0].Baskets[0];
        Assert.Equal("8000123", basket.Barcode);
        Assert.Equal(1, basket.CurrentQuantity);
        Assert.Equal(3, basket.MaxQuantity);
        Assert.Contains("0001", _viewModel.LastMessage.Text);
    }

    [Fact]
    public void IgnoredScan_KeepsPreviousMessage()
    {
        var before = _viewModel.LastMessage;

        _scans.Raise("  ", ScanOutcome.Ignored());

        Assert.Same(before, _viewModel.LastMessage);
    }

    [Fact]
    public void RejectedScan_ShowsErrorWithoutChangingSession()
    {
        _scans.Raise("???", ScanOutcome.LoginRejected(LoginFailure.UnknownOperator));

        Assert.False(_viewModel.IsLoggedIn);
        Assert.Equal(MessageSeverity.Error, _viewModel.LastMessage.Severity);
    }

    [Fact]
    public async Task RefreshStatus_PublishesQueueCountsPtlStateAndOperators()
    {
        _outbox.Pending = 4;
        _outbox.Quarantined = 1;
        _lightstep.IsConnected = true;
        _directory.Operators.Add(Operator("Anna"));
        _directory.Operators.Add(Operator("Bruno"));

        await _viewModel.RefreshStatusAsync(CancellationToken.None);

        Assert.Equal(4, _viewModel.PendingEvents);
        Assert.Equal(1, _viewModel.QuarantinedEvents);
        Assert.True(_viewModel.HasQuarantinedEvents);
        Assert.True(_viewModel.IsPtlConnected);
        Assert.Equal(["Anna", "Bruno"], _viewModel.AvailableOperators.Select(o => o.Name));
    }

    [Fact]
    public async Task RefreshStatus_KeepsSelectedOperatorWhenListChanges()
    {
        var anna = Operator("Anna");
        _directory.Operators.Add(anna);
        await _viewModel.RefreshStatusAsync(CancellationToken.None);
        _viewModel.SelectedOperator = _viewModel.AvailableOperators[0];

        _directory.Operators.Add(Operator("Bruno"));
        await _viewModel.RefreshStatusAsync(CancellationToken.None);

        Assert.Equal(anna.Id, _viewModel.SelectedOperator?.Id);
    }

    [Fact]
    public async Task PinLogin_RequiresOperatorAndPin()
    {
        _directory.Operators.Add(Operator());
        await _viewModel.RefreshStatusAsync(CancellationToken.None);

        Assert.False(_viewModel.LoginWithPinCommand.CanExecute(null));

        _viewModel.SelectedOperator = _viewModel.AvailableOperators[0];
        Assert.False(_viewModel.LoginWithPinCommand.CanExecute(null));

        _viewModel.Pin = "1234";
        Assert.True(_viewModel.LoginWithPinCommand.CanExecute(null));
    }

    [Fact]
    public async Task PinLogin_Success_StartsTurnAndClearsPin()
    {
        var anna = Operator("Anna");
        _directory.Operators.Add(anna);
        await _viewModel.RefreshStatusAsync(CancellationToken.None);
        _viewModel.SelectedOperator = _viewModel.AvailableOperators[0];
        _viewModel.Pin = "1234";
        _login.NextLogin = LoginResult.Succeeded(Session("Anna"));

        await _viewModel.LoginWithPinCommand.ExecuteAsync(null);

        Assert.Equal((anna.Id, "1234"), _login.LastPinLogin);
        Assert.True(_viewModel.IsLoggedIn);
        Assert.Equal("Anna", _viewModel.OperatorName);
        Assert.Equal(string.Empty, _viewModel.Pin);
    }

    [Fact]
    public async Task PinLogin_WrongPin_ShowsErrorAndClearsPin()
    {
        _directory.Operators.Add(Operator());
        await _viewModel.RefreshStatusAsync(CancellationToken.None);
        _viewModel.SelectedOperator = _viewModel.AvailableOperators[0];
        _viewModel.Pin = "0000";
        _login.NextLogin = LoginResult.Failed(LoginFailure.InvalidPin);

        await _viewModel.LoginWithPinCommand.ExecuteAsync(null);

        Assert.False(_viewModel.IsLoggedIn);
        Assert.Equal(string.Empty, _viewModel.Pin);
        Assert.Equal(MessageSeverity.Error, _viewModel.LastMessage.Severity);
        Assert.Contains("PIN", _viewModel.LastMessage.Text);
    }

    [Fact]
    public async Task Logout_EndsTurn()
    {
        _session.Begin(Session());
        _scans.Raise("X", ScanOutcome.Ignored());
        Assert.True(_viewModel.IsLoggedIn);

        await _viewModel.LogoutCommand.ExecuteAsync(null);

        Assert.False(_viewModel.IsLoggedIn);
        Assert.Null(_viewModel.OperatorName);
    }

    [Fact]
    public async Task Logout_WhileBusy_KeepsTurnAndWarns()
    {
        _session.Begin(Session());
        _scans.Raise("X", ScanOutcome.Ignored());
        _login.NextLogout = LogoutResult.SystemBusy;

        await _viewModel.LogoutCommand.ExecuteAsync(null);

        Assert.True(_viewModel.IsLoggedIn);
        Assert.Equal(MessageSeverity.Warning, _viewModel.LastMessage.Severity);
    }

    [Fact]
    public void Dispose_UnsubscribesFromScans()
    {
        Assert.Equal(1, _scans.SubscriberCount);

        _viewModel.Dispose();

        Assert.Equal(0, _scans.SubscriberCount);
    }
}
