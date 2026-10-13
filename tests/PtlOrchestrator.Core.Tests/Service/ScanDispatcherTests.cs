using Microsoft.Extensions.Logging.Abstractions;
using PtlOrchestrator.Core.Tests.Support;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.Service;

public class ScanDispatcherTests
{
    private readonly FakeOperatorDirectory _directory = new();
    private readonly OperatorSessionContext _sessionContext = new();
    private readonly FakeCartManager _cartManager = new();
    private readonly AppProcessingState _processingState = new(NullLogger<AppProcessingState>.Instance);
    private readonly ScanDispatcher _dispatcher;

    public ScanDispatcherTests()
    {
        var loginService = new OperatorLoginService(
            _directory,
            _sessionContext,
            new FakeOperationRecorder(),
            _processingState,
            TimeProvider.System,
            NullLogger<OperatorLoginService>.Instance);

        _dispatcher = new ScanDispatcher(
            _sessionContext,
            loginService,
            _cartManager,
            _processingState,
            NullLogger<ScanDispatcher>.Instance);
    }

    private void StartSession()
        => _sessionContext.Begin(new OperatorSession(Guid.NewGuid(), Guid.NewGuid(), "Mario", DateTimeOffset.UtcNow));

    private static Cart AnyCart() => new("1", [new Basket("0001", 0)]);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Dispatch_WithBlankCode_IsIgnored(string code)
    {
        var outcome = await _dispatcher.DispatchAsync(code, CancellationToken.None);

        Assert.Equal(ScanOutcomeKind.Ignored, outcome.Kind);
    }

    [Fact]
    public async Task Dispatch_WithoutSession_TreatsTheCodeAsBadgeAndLogsIn()
    {
        _directory.Add(name: "Mario", badgeCode: "BADGE-1");

        var outcome = await _dispatcher.DispatchAsync("BADGE-1", CancellationToken.None);

        Assert.Equal(ScanOutcomeKind.OperatorLoggedIn, outcome.Kind);
        Assert.Equal("Mario", outcome.Session!.OperatorName);
        Assert.Empty(_cartManager.ProcessedBarcodes);
    }

    [Fact]
    public async Task Dispatch_WithoutSession_AndUnknownBadge_RejectsTheLogin()
    {
        var outcome = await _dispatcher.DispatchAsync("8001234567890", CancellationToken.None);

        Assert.Equal(ScanOutcomeKind.LoginRejected, outcome.Kind);
        Assert.Equal(LoginFailure.UnknownOperator, outcome.LoginFailure);
        Assert.Empty(_cartManager.ProcessedBarcodes);
    }

    [Fact]
    public async Task Dispatch_WithSession_TreatsTheCodeAsProduct()
    {
        StartSession();
        var cart = AnyCart();
        _cartManager.OnProcessBarcode = _ =>
            Task.FromResult(CartAssignmentResult.NewItem(cart, cart.GetBaskets.Single()));

        var outcome = await _dispatcher.DispatchAsync("8001234567890", CancellationToken.None);

        Assert.Equal(ScanOutcomeKind.ProductAccepted, outcome.Kind);
        Assert.Equal(["8001234567890"], _cartManager.ProcessedBarcodes);
    }

    [Fact]
    public async Task Dispatch_WithSession_TrimsTheCodeBeforeProcessing()
    {
        StartSession();

        await _dispatcher.DispatchAsync("  8001234567890 ", CancellationToken.None);

        Assert.Equal(["8001234567890"], _cartManager.ProcessedBarcodes);
    }

    [Fact]
    public async Task Dispatch_WhenProductIsRejected_CarriesTheReason()
    {
        StartSession();
        _cartManager.OnProcessBarcode = _ =>
            Task.FromResult(CartAssignmentResult.Rejected("Carrello saturo"));

        var outcome = await _dispatcher.DispatchAsync("8001234567890", CancellationToken.None);

        Assert.Equal(ScanOutcomeKind.ProductRejected, outcome.Kind);
        Assert.Equal("Carrello saturo", outcome.Message);
    }

    [Fact]
    public async Task Dispatch_WhenAnotherOperationIsRunning_ReportsSystemBusyWithoutProcessing()
    {
        StartSession();
        _processingState.TryEnter();

        var outcome = await _dispatcher.DispatchAsync("8001234567890", CancellationToken.None);

        Assert.Equal(ScanOutcomeKind.SystemBusy, outcome.Kind);
        Assert.Empty(_cartManager.ProcessedBarcodes);
        Assert.True(_processingState.IsBusy);
    }

    [Fact]
    public async Task Dispatch_ReleasesTheBusyStateAfterProcessing()
    {
        StartSession();

        await _dispatcher.DispatchAsync("8001234567890", CancellationToken.None);

        Assert.False(_processingState.IsBusy);
    }

    [Fact]
    public async Task Dispatch_WhileProcessing_MarksTheSystemAsBusy()
    {
        StartSession();
        var wasBusyDuringProcessing = false;
        _cartManager.OnProcessBarcode = _ =>
        {
            wasBusyDuringProcessing = _processingState.IsBusy;
            return Task.FromResult(CartAssignmentResult.Rejected("x"));
        };

        await _dispatcher.DispatchAsync("8001234567890", CancellationToken.None);

        Assert.True(wasBusyDuringProcessing);
    }

    [Fact]
    public async Task Dispatch_WhenProcessingThrows_ReturnsFailureAndReleasesTheBusyState()
    {
        StartSession();
        _cartManager.OnProcessBarcode = _ => throw new InvalidOperationException("boom");

        var outcome = await _dispatcher.DispatchAsync("8001234567890", CancellationToken.None);

        Assert.Equal(ScanOutcomeKind.ProcessingFailed, outcome.Kind);
        Assert.Equal("boom", outcome.Message);
        Assert.False(_processingState.IsBusy);
    }

    [Fact]
    public async Task Dispatch_WhenCancelled_PropagatesCancellationAndReleasesTheBusyState()
    {
        StartSession();
        _cartManager.OnProcessBarcode = _ => throw new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _dispatcher.DispatchAsync("8001234567890", CancellationToken.None));

        Assert.False(_processingState.IsBusy);
    }
}
