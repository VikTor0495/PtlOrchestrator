using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Sync;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Sync;

public class EventSyncServiceTests : IDisposable
{
    private sealed class FakeCentralClient : ICentralClient
    {
        public Func<IReadOnlyList<OperationEvent>, CentralResult<IngestEventsResponse>> OnSend { get; set; } =
            events => CentralResult<IngestEventsResponse>.Ok(new IngestEventsResponse(events.Count, 0));

        public List<int> BatchSizes { get; } = [];

        public Task<CentralResult<IngestEventsResponse>> SendEventsAsync(
            IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken)
        {
            BatchSizes.Add(events.Count);
            return Task.FromResult(OnSend(events));
        }

        public Task<CentralResult<OperatorsSnapshotResponse>> GetOperatorsSnapshotAsync(
            DateTimeOffset? since, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private readonly TempDatabase _database = new();
    private readonly SqliteOutbox _outbox;
    private readonly FakeCentralClient _client = new();

    public EventSyncServiceTests()
    {
        _outbox = new SqliteOutbox(_database.Database, TimeProvider.System);
    }

    public void Dispose() => _database.Dispose();

    private EventSyncService CreateService(int batchSize = 2)
        => new(
            _outbox,
            _client,
            Options.Create(new CentralOptions { EventBatchSize = batchSize }),
            TimeProvider.System,
            NullLogger<EventSyncService>.Instance);

    private async Task<List<OperationEvent>> EnqueueAsync(int count)
    {
        var events = new List<OperationEvent>();
        for (var i = 0; i < count; i++)
        {
            var evt = new OperationEvent(
                Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(i), "ST1", null, null,
                OperationEventType.BarcodeAccepted, Barcode: $"B{i}");
            events.Add(evt);
            await _outbox.EnqueueAsync(evt, CancellationToken.None);
        }

        return events;
    }

    [Fact]
    public async Task SyncOnce_EmptyOutbox_SendsNothing()
    {
        var delivered = await CreateService().SyncOnceAsync(CancellationToken.None);

        Assert.Equal(0, delivered);
        Assert.Empty(_client.BatchSizes);
    }

    [Fact]
    public async Task SyncOnce_DrainsOutboxInBatches()
    {
        await EnqueueAsync(5);

        var delivered = await CreateService(batchSize: 2).SyncOnceAsync(CancellationToken.None);

        Assert.Equal(5, delivered);
        Assert.Equal([2, 2, 1], _client.BatchSizes);
        Assert.Equal(0, await _outbox.CountPendingAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SyncOnce_TransientFailure_KeepsEventsAndStops()
    {
        await EnqueueAsync(3);
        _client.OnSend = _ => CentralResult<IngestEventsResponse>.Transient("offline");

        var delivered = await CreateService(batchSize: 2).SyncOnceAsync(CancellationToken.None);

        Assert.Equal(0, delivered);
        Assert.Single(_client.BatchSizes);
        Assert.Equal(3, await _outbox.CountPendingAsync(CancellationToken.None));
        Assert.Equal(0, await _outbox.CountQuarantinedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SyncOnce_AfterOutage_DeliversEverythingInOrder()
    {
        var events = await EnqueueAsync(3);
        _client.OnSend = _ => CentralResult<IngestEventsResponse>.Transient("offline");
        var service = CreateService(batchSize: 10);
        await service.SyncOnceAsync(CancellationToken.None);

        var received = new List<Guid>();
        _client.OnSend = batch =>
        {
            received.AddRange(batch.Select(e => e.Id));
            return CentralResult<IngestEventsResponse>.Ok(new IngestEventsResponse(batch.Count, 0));
        };
        var delivered = await service.SyncOnceAsync(CancellationToken.None);

        Assert.Equal(3, delivered);
        Assert.Equal(events.Select(e => e.Id), received);
    }

    [Fact]
    public async Task SyncOnce_RejectedBatch_QuarantinesOnlyTheBadEvent()
    {
        var events = await EnqueueAsync(3);
        var bad = events[1];
        _client.OnSend = batch => batch.Any(e => e.Id == bad.Id)
            ? CentralResult<IngestEventsResponse>.Rejected("HTTP 400")
            : CentralResult<IngestEventsResponse>.Ok(new IngestEventsResponse(batch.Count, 0));

        var delivered = await CreateService(batchSize: 10).SyncOnceAsync(CancellationToken.None);

        Assert.Equal(2, delivered);
        Assert.Equal(0, await _outbox.CountPendingAsync(CancellationToken.None));
        Assert.Equal(1, await _outbox.CountQuarantinedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SyncOnce_ServerGoesDownWhileIsolating_StopsAndKeepsRemainingEvents()
    {
        var events = await EnqueueAsync(3);
        var calls = 0;
        _client.OnSend = batch =>
        {
            calls++;
            // 1ª chiamata: batch rifiutato; 2ª: primo evento ok; 3ª: il server cade
            return calls switch
            {
                1 => CentralResult<IngestEventsResponse>.Rejected("HTTP 400"),
                2 => CentralResult<IngestEventsResponse>.Ok(new IngestEventsResponse(1, 0)),
                _ => CentralResult<IngestEventsResponse>.Transient("offline")
            };
        };

        var delivered = await CreateService(batchSize: 10).SyncOnceAsync(CancellationToken.None);

        Assert.Equal(1, delivered);
        Assert.Equal(2, await _outbox.CountPendingAsync(CancellationToken.None));
        Assert.Equal(events[1].Id, (await _outbox.PeekBatchAsync(1, CancellationToken.None))[0].Id);
    }
}
