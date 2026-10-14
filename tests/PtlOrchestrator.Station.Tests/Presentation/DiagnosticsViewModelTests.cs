using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Service.Impl;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Presentation;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Sync;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Presentation;

public class DiagnosticsViewModelTests
{
    private sealed class StubCentralClient : ICentralClient
    {
        public bool Online { get; set; } = true;
        public int OperatorsInSnapshot { get; set; }

        public Task<CentralResult<IngestEventsResponse>> SendEventsAsync(IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken)
            => Task.FromResult(Online
                ? CentralResult<IngestEventsResponse>.Ok(new IngestEventsResponse(events.Count, 0))
                : CentralResult<IngestEventsResponse>.Transient("offline"));

        public Task<CentralResult<OperatorsSnapshotResponse>> GetOperatorsSnapshotAsync(DateTimeOffset? since, CancellationToken cancellationToken)
        {
            var operators = Enumerable.Range(0, OperatorsInSnapshot)
                .Select(i => new OperatorDto(Guid.NewGuid(), $"Op{i}", $"B{i}", null, true, DateTimeOffset.UtcNow))
                .ToList();

            return Task.FromResult(Online
                ? CentralResult<OperatorsSnapshotResponse>.Ok(new OperatorsSnapshotResponse(operators, DateTimeOffset.UtcNow))
                : CentralResult<OperatorsSnapshotResponse>.Transient("offline"));
        }
    }

    private sealed class MemoryOutbox : IOutbox
    {
        public List<OperationEvent> Events { get; } = [];
        public int Quarantined { get; set; }

        public Task EnqueueAsync(OperationEvent operationEvent, CancellationToken cancellationToken)
        {
            Events.Add(operationEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OperationEvent>> PeekBatchAsync(int maxCount, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<OperationEvent>>(Events.Take(maxCount).ToList());

        public Task RemoveAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken)
        {
            Events.RemoveAll(e => eventIds.Contains(e.Id));
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(IReadOnlyCollection<Guid> eventIds, string error, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task QuarantineAsync(IReadOnlyCollection<Guid> eventIds, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> CountPendingAsync(CancellationToken cancellationToken) => Task.FromResult(Events.Count);
        public Task<int> CountQuarantinedAsync(CancellationToken cancellationToken) => Task.FromResult(Quarantined);
    }

    private sealed class MemoryOperatorCache : IOperatorCache
    {
        public DateTimeOffset? LastSync { get; set; }

        public Task UpsertAsync(IReadOnlyCollection<OperatorDto> operators, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<DateTimeOffset?> GetLastSyncAsync(CancellationToken cancellationToken) => Task.FromResult(LastSync);

        public Task SetLastSyncAsync(DateTimeOffset serverTime, CancellationToken cancellationToken)
        {
            LastSync = serverTime;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCartManager(List<string> calls, FakeLightstep lightstep) : ICartManager
    {
        public bool FailOnReset { get; set; }

        public Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ResetAll(CancellationToken cancellationToken)
        {
            calls.Add("reset");
            return FailOnReset ? throw new IOException("PTL non risponde") : Task.CompletedTask;
        }

        public void ShowStatus() { }

        public void WriteCsvReport() => calls.Add("report");
    }

    private readonly StubCentralClient _central = new();
    private readonly MemoryOutbox _outbox = new();
    private readonly MemoryOperatorCache _cache = new();
    private readonly FakeLightstep _lightstep = new();
    private readonly AppProcessingState _processing = new(NullLogger<AppProcessingState>.Instance);
    private readonly List<string> _calls = [];
    private readonly RecordingCartManager _carts;
    private readonly DiagnosticsViewModel _viewModel;

    public DiagnosticsViewModelTests()
    {
        _carts = new RecordingCartManager(_calls, _lightstep);
        var options = Options.Create(new CentralOptions());
        var eventSync = new EventSyncService(_outbox, _central, options, TimeProvider.System, NullLogger<EventSyncService>.Instance);
        var operatorSync = new OperatorSyncService(_cache, _central, options, TimeProvider.System, NullLogger<OperatorSyncService>.Instance);

        _viewModel = new DiagnosticsViewModel(
            eventSync, operatorSync, _outbox, _cache, _carts, _lightstep, _processing,
            new ImmediateDispatcher(), NullLogger<DiagnosticsViewModel>.Instance);
    }

    private void AddPendingEvents(int count)
    {
        for (var i = 0; i < count; i++)
            _outbox.Events.Add(new OperationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "ST", null, null, OperationEventType.BarcodeAccepted));
    }

    [Fact]
    public async Task Refresh_ShowsQueueAndSyncState()
    {
        AddPendingEvents(3);
        _outbox.Quarantined = 2;
        _lightstep.IsConnected = true;
        _cache.LastSync = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        await _viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(3, _viewModel.PendingEvents);
        Assert.Equal(2, _viewModel.QuarantinedEvents);
        Assert.True(_viewModel.IsPtlConnected);
        Assert.NotEqual("mai", _viewModel.LastOperatorSync);
    }

    [Fact]
    public async Task Refresh_NeverSynced_SaysSo()
    {
        await _viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("mai", _viewModel.LastOperatorSync);
    }

    [Fact]
    public async Task SyncNow_Online_DeliversEventsAndUpdatesOperators()
    {
        AddPendingEvents(3);
        _central.OperatorsInSnapshot = 2;

        await _viewModel.SyncNowCommand.ExecuteAsync(null);

        Assert.Equal(0, _viewModel.PendingEvents);
        Assert.Contains("inviati: 3", _viewModel.Result);
        Assert.Contains("operatori aggiornati: 2", _viewModel.Result);
        Assert.False(_viewModel.IsWorking);
    }

    [Fact]
    public async Task SyncNow_Offline_ReportsServerUnreachableAndKeepsEvents()
    {
        AddPendingEvents(3);
        _central.Online = false;

        await _viewModel.SyncNowCommand.ExecuteAsync(null);

        Assert.Equal(3, _viewModel.PendingEvents);
        Assert.Contains("non raggiungibile", _viewModel.Result);
        Assert.Contains("ancora 3", _viewModel.Result);
    }

    [Fact]
    public async Task WriteReport_WritesAndConfirms()
    {
        await _viewModel.WriteReportCommand.ExecuteAsync(null);

        Assert.Equal(["report"], _calls);
        Assert.Contains("Report", _viewModel.Result);
    }

    [Fact]
    public async Task ResetCarts_WritesReportReconnectsAndResets_ThenReleasesBusyState()
    {
        _lightstep.IsConnected = true;

        await _viewModel.ResetCartsAsync(CancellationToken.None);

        Assert.Equal(["report", "reset"], _calls);
        Assert.Equal(["disconnect", "connect"], _lightstep.Calls);
        Assert.False(_processing.IsBusy);
        Assert.Contains("azzerati", _viewModel.Result);
    }

    [Fact]
    public async Task ResetCarts_WhileAnOperationIsRunning_DoesNothing()
    {
        _processing.TryEnter();

        await _viewModel.ResetCartsAsync(CancellationToken.None);

        Assert.Empty(_calls);
        Assert.Empty(_lightstep.Calls);
        Assert.True(_processing.IsBusy);
        Assert.Contains("in corso", _viewModel.Result);
    }

    [Fact]
    public async Task ResetCarts_Failure_IsReportedAndBusyStateReleased()
    {
        _carts.FailOnReset = true;

        await _viewModel.ResetCartsAsync(CancellationToken.None);

        Assert.False(_processing.IsBusy);
        Assert.False(_viewModel.IsWorking);
        Assert.Contains("non riuscito", _viewModel.Result);
        Assert.Contains("PTL non risponde", _viewModel.Result);
    }
}
