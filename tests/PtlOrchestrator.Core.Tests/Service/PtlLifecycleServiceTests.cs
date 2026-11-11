using AioiSystems.Lightstep;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.Service;

public sealed class PtlLifecycleServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class Steps
    {
        public List<string> Log { get; } = [];
    }

    private sealed class FakeConnection(Steps steps) : ILightstepConnectionService
    {
        public bool IsConnected => false;

        public bool IsConnectedTo(PtlEndpoint endpoint) => false;

        public Task EnsureConnectedAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task EnsureConnectedAsync(PtlEndpoint endpoint, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> TryConnectAsync(PtlEndpoint endpoint, CancellationToken ct) => throw new NotSupportedException();

        public EthernetController GetController(PtlEndpoint? endpoint = null) => throw new NotSupportedException();

        public void Disconnect()
        {
            steps.Log.Add("disconnect");
        }

        public void Disconnect(PtlEndpoint endpoint) => Disconnect();
    }

    private sealed class FakeLimits(Steps steps) : IBasketLimitService
    {
        public int FailuresBeforeSuccess { get; set; }

        public string? LoadedPath { get; private set; }

        public void Load(string csvPath, CancellationToken cancellationToken)
        {
            if (FailuresBeforeSuccess > 0)
            {
                FailuresBeforeSuccess--;
                throw new IOException("file limiti non leggibile");
            }

            LoadedPath = csvPath;
            steps.Log.Add("limits");
        }

        public int GetMaxFor(string barcode) => 0;
        public bool HasMaxLimit(string barcode) => false;
    }

    private sealed class FakeCarts(Steps steps) : ICartManager
    {
        public bool ThrowOnReport { get; set; }

        public List<string> ActiveCarts { get; } = [];

        public Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public bool IsKnownCart(string code) => false;

        public Task<CartCheckInOutcome> CheckInAsync(string cartId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CartReleaseOutcome> ReleaseCartAsync(string cartId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public IReadOnlyList<string> ActiveCartIds() => ActiveCarts;

        public bool HasItems() => false;

        public Task CloseWorkAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ResetAll(CancellationToken cancellationToken)
        {
            steps.Log.Add("reset");
            return Task.CompletedTask;
        }

        public void ShowStatus() { }

        public void WriteCsvReport()
        {
            steps.Log.Add("report");
            if (ThrowOnReport)
                throw new IOException("disco pieno");
        }
    }

    private readonly Steps _steps = new();
    private readonly FakeConnection _connection;
    private readonly FakeLimits _limits;
    private readonly FakeCarts _carts;
    private readonly AppProcessingState _processing = new(NullLogger<AppProcessingState>.Instance);
    private readonly FakeTimeProvider _time = new();
    private readonly PtlLifecycleService _service;

    public PtlLifecycleServiceTests()
    {
        _connection = new FakeConnection(_steps);
        _limits = new FakeLimits(_steps);
        _carts = new FakeCarts(_steps);
        _service = new PtlLifecycleService(
            _connection, _limits, _carts, _processing,
            Options.Create(new BarcodeLimitOptions { BasketId = "x", FileName = "limiti.csv", BarcodeColumnIndex = 0, LimitColumnIndex = 1 }),
            _time, NullLogger<PtlLifecycleService>.Instance);
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(Timeout);
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }

    // il ritardo tra i tentativi si registra solo dopo il fallimento: si avanza il tempo finché il servizio non procede
    private async Task AdvanceUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(Timeout);
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            _time.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(10, cts.Token);
        }
    }

    [Fact]
    public async Task Start_LoadsLimitsAndResets_WithoutConnectingAnyController()
    {
        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _service.IsReady);

        Assert.Equal(["limits", "reset"], _steps.Log);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "limiti.csv"), _limits.LoadedPath);

        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhileInitializing_SystemIsBusy_AndIsReleasedWhenReady()
    {
        _limits.FailuresBeforeSuccess = 1;

        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _processing.IsBusy);

        Assert.False(_service.IsReady);

        await AdvanceUntilAsync(() => _service.IsReady);

        Assert.False(_processing.IsBusy);

        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FailedInitialization_IsRetriedAfterTheDelay()
    {
        _limits.FailuresBeforeSuccess = 2;

        await _service.StartAsync(CancellationToken.None);
        await AdvanceUntilAsync(() => _service.IsReady);

        Assert.Equal(1, _steps.Log.Count(s => s == "limits"));

        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stop_WithActiveCarts_WritesReportResetsAndDisconnects()
    {
        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _service.IsReady);
        _carts.ActiveCarts.Add("1");
        _steps.Log.Clear();

        await _service.StopAsync(CancellationToken.None);

        Assert.Equal(["report", "reset", "disconnect"], _steps.Log);
        Assert.False(_service.IsReady);
    }

    [Fact]
    public async Task Stop_WithoutActiveCarts_WritesReportAndDisconnectsWithoutReset()
    {
        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _service.IsReady);
        _steps.Log.Clear();

        await _service.StopAsync(CancellationToken.None);

        Assert.Equal(["report", "disconnect"], _steps.Log);
    }

    [Fact]
    public async Task Stop_ReportFailure_StillDisconnects()
    {
        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _service.IsReady);
        _carts.ThrowOnReport = true;
        _steps.Log.Clear();

        await _service.StopAsync(CancellationToken.None);

        Assert.Contains("disconnect", _steps.Log);
    }
}
