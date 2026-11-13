using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Manager;
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

        public Task<LoginResult> LoginWithBadgeAsync(string badgeCode, CancellationToken cancellationToken)
        {
            if (NextLogin.Success)
                sessionContext.Begin(NextLogin.Session!);

            return Task.FromResult(NextLogin);
        }

        public Task<LoginResult> LoginWithPinAsync(Guid operatorId, string pin, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken)
        {
            if (NextLogout == LogoutResult.Done)
                sessionContext.End();

            return Task.FromResult(NextLogout);
        }
    }

    private sealed class FakeScanDispatcher : IScanDispatcher
    {
        public List<string> Submitted { get; } = [];
        public ScanOutcome NextOutcome { get; set; } = ScanOutcome.Ignored();

        public Task<ScanOutcome> DispatchAsync(string scannedCode, CancellationToken cancellationToken)
        {
            Submitted.Add(scannedCode);
            return Task.FromResult(NextOutcome);
        }
    }

    private sealed class FakeCartManager : ICartManager
    {
        public bool Items { get; set; }
        public List<string> Active { get; } = [];
        public List<string> Released { get; } = [];
        public bool ReleaseFails { get; set; }
        public int CloseCount { get; private set; }

        public Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public bool IsKnownCart(string code) => false;

        public Task<CartCheckInOutcome> CheckInAsync(string cartCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CartReleaseOutcome> ReleaseCartAsync(string cartCode, CancellationToken cancellationToken)
        {
            if (ReleaseFails)
                return Task.FromResult(new CartReleaseOutcome(false, $"Rilascio di {cartCode} non riuscito"));

            Released.Add(cartCode);
            Active.Remove(cartCode);
            return Task.FromResult(new CartReleaseOutcome(true, $"Carrello {cartCode} rimosso"));
        }

        public IReadOnlyList<string> ActiveCartIds() => [.. Active];

        public bool HasItems() => Items;

        public Task CloseWorkAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return Task.CompletedTask;
        }

        public Task ResetAll(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ShowStatus() { }

        public void WriteCsvReport() { }
    }

    private readonly FakeScanActivity _scans = new();
    private readonly OperatorSessionContext _session = new();
    private readonly FakeLoginService _login;
    private readonly FakeScanDispatcher _dispatcher = new();
    private readonly FakeCartManager _cartManager = new();
    private readonly FakeOutbox _outbox = new();
    private readonly FakeLightstep _lightstep = new();
    private readonly CartContainer _carts = new([new Cart("1", [new Basket("0001", 0), new Basket("0002", 0)])]);
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _login = new FakeLoginService(_session);
        _viewModel = new MainViewModel(
            _scans, _session, _login, _dispatcher, _cartManager, _outbox, _lightstep, _carts,
            Options.Create(new StationOptions { StationId = "ST-TEST" }),
            new ImmediateDispatcher(), TimeProvider.System, NullLogger<MainViewModel>.Instance);
    }

    public void Dispose() => _viewModel.Dispose();

    private static OperatorSession Session(string name = "Mario") => new(Guid.NewGuid(), Guid.NewGuid(), name, DateTimeOffset.UtcNow);

    [Fact]
    public void InitialState_IsLoggedOutWithEmptyBaskets()
    {
        Assert.Equal("ST-TEST", _viewModel.StationId);
        Assert.False(_viewModel.IsLoggedIn);
        Assert.True(_viewModel.IsLoggedOut);
        Assert.Null(_viewModel.OperatorName);
        Assert.False(_viewModel.LogoutCommand.CanExecute(null));
        Assert.False(_viewModel.CloseWorkCommand.CanExecute(null));
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
    public async Task RefreshStatus_PublishesQueueCountsAndPtlState()
    {
        _outbox.Pending = 4;
        _outbox.Quarantined = 1;
        _lightstep.IsConnected = true;

        await _viewModel.RefreshStatusAsync(CancellationToken.None);

        Assert.Equal(4, _viewModel.PendingEvents);
        Assert.Equal(1, _viewModel.QuarantinedEvents);
        Assert.True(_viewModel.HasQuarantinedEvents);
        Assert.True(_viewModel.IsPtlConnected);
    }

    [Fact]
    public void SubmitCode_RequiresNonBlankInput()
    {
        Assert.False(_viewModel.SubmitCodeCommand.CanExecute(null));

        _viewModel.CodeInput = "   ";
        Assert.False(_viewModel.SubmitCodeCommand.CanExecute(null));

        _viewModel.CodeInput = "OP001";
        Assert.True(_viewModel.SubmitCodeCommand.CanExecute(null));
    }

    [Fact]
    public async Task SubmitCode_SendsTheTrimmedCodeAsIfScanned_AndClearsTheInput()
    {
        _viewModel.CodeInput = "  OP001 ";
        _dispatcher.NextOutcome = ScanOutcome.OperatorLoggedIn(Session("Anna"));
        _login.NextLogin = LoginResult.Succeeded(Session("Anna"));

        await _viewModel.SubmitCodeCommand.ExecuteAsync(null);

        Assert.Equal(["OP001"], _dispatcher.Submitted);
        Assert.Equal(string.Empty, _viewModel.CodeInput);
        Assert.Equal("OP001", _viewModel.LastScanCode);
        Assert.Equal(MessageSeverity.Success, _viewModel.LastMessage.Severity);
    }

    [Fact]
    public async Task SubmitCode_CartCheckInRejected_ShowsItsMessageAsWarning()
    {
        _session.Begin(Session());
        _viewModel.CodeInput = "2";
        _dispatcher.NextOutcome = ScanOutcome.CartCheckInRejected("Carrello 2 già in uso dalla postazione ST-02");

        await _viewModel.SubmitCodeCommand.ExecuteAsync(null);

        Assert.Equal(MessageSeverity.Warning, _viewModel.LastMessage.Severity);
        Assert.Contains("ST-02", _viewModel.LastMessage.Text);
    }

    [Fact]
    public async Task Logout_WithProductsInCarts_GeneratesTheReportFirstThenDetachesTheCarts()
    {
        _session.Begin(Session());
        _scans.Raise("X", ScanOutcome.Ignored());
        _cartManager.Items = true;
        _cartManager.Active.Add("1");

        await _viewModel.LogoutCommand.ExecuteAsync(null);

        Assert.Equal(1, _cartManager.CloseCount);
        Assert.Equal(["1"], _cartManager.Released);
        Assert.False(_viewModel.IsLoggedIn);
    }

    [Fact]
    public async Task Logout_WithEmptyCarts_DoesNotGenerateAReportButStillDetachesThem()
    {
        _session.Begin(Session());
        _scans.Raise("X", ScanOutcome.Ignored());
        _cartManager.Active.AddRange(["1", "2"]);

        await _viewModel.LogoutCommand.ExecuteAsync(null);

        Assert.Equal(0, _cartManager.CloseCount);
        Assert.Equal(["1", "2"], _cartManager.Released);
        Assert.False(_viewModel.IsLoggedIn);
    }

    [Fact]
    public async Task Logout_WhenACartCannotBeDetached_KeepsTheTurn()
    {
        _session.Begin(Session());
        _scans.Raise("X", ScanOutcome.Ignored());
        _cartManager.Active.Add("1");
        _cartManager.ReleaseFails = true;

        await _viewModel.LogoutCommand.ExecuteAsync(null);

        Assert.True(_viewModel.IsLoggedIn);
        Assert.Equal(MessageSeverity.Error, _viewModel.LastMessage.Severity);
    }

    [Fact]
    public async Task ScanningWhileAProductIsBeingPlaced_AsksToPlaceTheFirstOneAndNamesIt()
    {
        _session.Begin(Session());
        PopupRequest? popup = null;
        _viewModel.PopupRequested += (_, request) => popup = request;
        _viewModel.CodeInput = "2222";
        _dispatcher.NextOutcome = ScanOutcome.ProductInProgress("1111");

        await _viewModel.SubmitCodeCommand.ExecuteAsync(null);

        Assert.NotNull(popup);
        Assert.Contains("1111", popup!.Message);
        Assert.True(popup.ClosesWhenProductIsPlaced);
        Assert.Equal(MessageSeverity.Warning, _viewModel.LastMessage.Severity);
    }

    [Fact]
    public void ScanningWhileAProductIsBeingPlaced_FromTheGun_RaisesThePopupToo()
    {
        PopupRequest? popup = null;
        _viewModel.PopupRequested += (_, request) => popup = request;

        _scans.Raise("2222", ScanOutcome.ProductInProgress("1111"));

        Assert.Contains("1111", popup!.Message);
    }

    [Fact]
    public void WhenTheProductIsPlaced_ThePopupIsDismissed()
    {
        var dismissed = 0;
        _viewModel.PopupDismissRequested += (_, _) => dismissed++;
        var assignment = _carts.AssignItem("8000123", 3);

        _scans.Raise("8000123", ScanOutcome.ProductAccepted(assignment));

        Assert.Equal(1, dismissed);
    }

    [Fact]
    public async Task CloseWork_KeepsTheCartsAttached()
    {
        _session.Begin(Session());
        _scans.Raise("X", ScanOutcome.Ignored());
        _cartManager.Active.Add("1");

        await _viewModel.CloseWorkCommand.ExecuteAsync(null);

        Assert.Empty(_cartManager.Released);
        Assert.Equal(["1"], _cartManager.Active);
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
    public async Task CloseWork_GeneratesReportAndShowsSuccess()
    {
        _session.Begin(Session());
        _scans.Raise("X", ScanOutcome.Ignored());

        await _viewModel.CloseWorkCommand.ExecuteAsync(null);

        Assert.Equal(1, _cartManager.CloseCount);
        Assert.Equal(MessageSeverity.Success, _viewModel.LastMessage.Severity);
        Assert.True(_viewModel.IsLoggedIn);
    }

    [Fact]
    public async Task RemoveCart_ReleasesItAndShowsTheOutcome()
    {
        _cartManager.Active.Add("1");

        await _viewModel.RemoveCartCommand.ExecuteAsync("1");

        Assert.Equal(["1"], _cartManager.Released);
        Assert.Equal(MessageSeverity.Success, _viewModel.LastMessage.Severity);
    }

    [Fact]
    public async Task CheckInBeyondTheCartLimit_RaisesAPopupAndShowsAWarning()
    {
        _session.Begin(Session());
        string? popup = null;
        _viewModel.PopupRequested += (_, request) => popup = request.Message;
        _viewModel.CodeInput = "5";
        _dispatcher.NextOutcome = ScanOutcome.CartLimitReached("Massimo 4 carrelli per volta");

        await _viewModel.SubmitCodeCommand.ExecuteAsync(null);

        Assert.Contains("Massimo 4", popup);
        Assert.Equal(MessageSeverity.Warning, _viewModel.LastMessage.Severity);
    }

    [Fact]
    public void CartLimit_FromTheScanner_RaisesAPopupToo()
    {
        string? popup = null;
        _viewModel.PopupRequested += (_, request) => popup = request.Message;

        _scans.Raise("5", ScanOutcome.CartLimitReached("Massimo 4 carrelli per volta"));

        Assert.NotNull(popup);
    }

    [Fact]
    public async Task CheckInCart_SendsTheTrimmedNumberThroughTheSamePathAsAScan()
    {
        await _viewModel.CheckInCartAsync("  3 ", CancellationToken.None);
        await _viewModel.CheckInCartAsync("   ", CancellationToken.None);

        Assert.Equal(["3"], _dispatcher.Submitted);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, 2, true)]
    [InlineData(3, 3, true)]
    [InlineData(4, 2, true)]
    public void CartLayout_AdaptsToTheNumberOfCarts(int count, int expectedColumns, bool hasCarts)
    {
        var carts = Enumerable.Range(1, count).Select(n => new Cart(n.ToString(), [new Basket($"000{n}", 0)]));
        var container = new CartContainer(carts);
        using var viewModel = new MainViewModel(
            _scans, _session, _login, _dispatcher, _cartManager, _outbox, _lightstep, container,
            Options.Create(new StationOptions { StationId = "ST-TEST" }),
            new ImmediateDispatcher(), TimeProvider.System, NullLogger<MainViewModel>.Instance);

        Assert.Equal(expectedColumns, viewModel.CartColumns);
        Assert.Equal(hasCarts, viewModel.HasCarts);
        Assert.Equal(!hasCarts, viewModel.NoCarts);
    }

    [Fact]
    public void Dispose_UnsubscribesFromScans()
    {
        Assert.Equal(1, _scans.SubscriberCount);

        _viewModel.Dispose();

        Assert.Equal(0, _scans.SubscriberCount);
    }
}
