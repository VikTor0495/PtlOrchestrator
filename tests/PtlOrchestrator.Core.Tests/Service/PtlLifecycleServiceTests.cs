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
        public bool IsConnected { get; set; }
        public int FailuresBeforeSuccess { get; set; }

        public Task EnsureConnectedAsync(CancellationToken ct)
        {
            steps.Log.Add("connect");
            if (FailuresBeforeSuccess > 0)
            {
                FailuresBeforeSuccess--;
                throw new IOException("controller non raggiungibile");
            }

            IsConnected = true;
            return Task.CompletedTask;
        }

        public EthernetController GetController() => throw new NotSupportedException();

        public void Disconnect()
        {
            steps.Log.Add("disconnect");
            IsConnected = false;
        }
    }

    private sealed class FakeLimits(Steps steps) : IBasketLimitService
    {
        public string? LoadedPath { get; private set; }

        public void Load(string csvPath, CancellationToken cancellationToken)
        {
            LoadedPath = csvPath;
            steps.Log.Add("limits");
        }

        public int GetMaxFor(string barcode) => 0;
        public bool HasMaxLimit(string barcode) => false;
    }

    private sealed class FakeCarts(Steps steps) : ICartManager
    {
        public bool ThrowOnReport { get; set; }

        public Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken) => throw new NotSupportedException();

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
    public async Task Start_ConnectsLoadsLimitsAndResetsInOrder_ThenBecomesReady()
    {
        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _service.IsReady);

        Assert.Equal(["connect", "limits", "reset"], _steps.Log);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "limiti.csv"), _limits.LoadedPath);

        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WhileInitializing_SystemIsBusy_AndIsReleasedWhenReady()
    {
        _connection.FailuresBeforeSuccess = 1;

        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _steps.Log.Count == 1);

        Assert.True(_processing.IsBusy);
        Assert.False(_service.IsReady);

        await AdvanceUntilAsync(() => _service.IsReady);

        Assert.False(_processing.IsBusy);

        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FailedInitialization_IsRetriedAfterTheDelay()
    {
        _connection.FailuresBeforeSuccess = 2;

        await _service.StartAsync(CancellationToken.None);
        await AdvanceUntilAsync(() => _service.IsReady);

        Assert.Equal(3, _steps.Log.Count(s => s == "connect"));

        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stop_WritesReportResetsCartsAndDisconnects()
    {
        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _service.IsReady);
        _steps.Log.Clear();

        await _service.StopAsync(CancellationToken.None);

        Assert.Equal(["report", "reset", "disconnect"], _steps.Log);
        Assert.False(_service.IsReady);
    }

    [Fact]
    public async Task Stop_ReportFailure_StillResetsAndDisconnects()
    {
        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _service.IsReady);
        _carts.ThrowOnReport = true;
        _steps.Log.Clear();

        await _service.StopAsync(CancellationToken.None);

        Assert.Equal(["report", "reset", "disconnect"], _steps.Log);
    }

    [Fact]
    public async Task Stop_BeforePtlWasEverConnected_DoesNotTryToResetOrHang()
    {
        _connection.FailuresBeforeSuccess = int.MaxValue;

        await _service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _steps.Log.Count == 1);

        await _service.StopAsync(CancellationToken.None);

        Assert.DoesNotContain("reset", _steps.Log);
        Assert.False(_processing.IsBusy);
    }
}
