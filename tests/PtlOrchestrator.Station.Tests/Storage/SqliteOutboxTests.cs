using Microsoft.Extensions.Time.Testing;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Storage;

public sealed class SqliteOutboxTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly TempDatabase _temp = new();
    private readonly SqliteOutbox _outbox;

    public SqliteOutboxTests()
    {
        _outbox = new SqliteOutbox(_temp.Database, new FakeTimeProvider(DateTimeOffset.UtcNow));
    }

    public void Dispose() => _temp.Dispose();

    private static OperationEvent NewEvent(string? barcode = null, OperationEventType type = OperationEventType.BarcodeAccepted)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, "ST-01", Guid.NewGuid(), Guid.NewGuid(), type, barcode);

    [Fact]
    public async Task Enqueue_ThenPeek_ReturnsTheEventWithAllItsFields()
    {
        var sent = new OperationEvent(
            Guid.NewGuid(), new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero), "ST-01",
            Guid.NewGuid(), Guid.NewGuid(), OperationEventType.WrongModulePressed,
            "8001234567890", "1", "0003", "Premuto modulo 0002, atteso 0003");

        await _outbox.EnqueueAsync(sent, Ct);
        var batch = await _outbox.PeekBatchAsync(10, Ct);

        Assert.Equal(sent, Assert.Single(batch));
    }

    [Fact]
    public async Task Peek_ReturnsEventsInInsertionOrder()
    {
        var events = Enumerable.Range(0, 5).Select(i => NewEvent(barcode: $"BC{i}")).ToList();
        foreach (var operationEvent in events)
            await _outbox.EnqueueAsync(operationEvent, Ct);

        var batch = await _outbox.PeekBatchAsync(10, Ct);

        Assert.Equal(events.Select(e => e.Id), batch.Select(e => e.Id));
    }

    [Fact]
    public async Task Peek_HonoursTheMaximumCount()
    {
        for (var i = 0; i < 5; i++)
            await _outbox.EnqueueAsync(NewEvent(), Ct);

        var batch = await _outbox.PeekBatchAsync(3, Ct);

        Assert.Equal(3, batch.Count);
    }

    [Fact]
    public async Task Peek_DoesNotRemoveTheEvents()
    {
        await _outbox.EnqueueAsync(NewEvent(), Ct);

        await _outbox.PeekBatchAsync(10, Ct);

        Assert.Equal(1, await _outbox.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task Enqueue_OfTheSameEventTwice_StoresItOnce()
    {
        var operationEvent = NewEvent();

        await _outbox.EnqueueAsync(operationEvent, Ct);
        await _outbox.EnqueueAsync(operationEvent, Ct);

        Assert.Equal(1, await _outbox.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task Remove_DeletesOnlyTheGivenEvents()
    {
        var first = NewEvent();
        var second = NewEvent();
        var third = NewEvent();
        foreach (var operationEvent in new[] { first, second, third })
            await _outbox.EnqueueAsync(operationEvent, Ct);

        await _outbox.RemoveAsync([first.Id, third.Id], Ct);

        var remaining = await _outbox.PeekBatchAsync(10, Ct);
        Assert.Equal([second.Id], remaining.Select(e => e.Id));
    }

    [Fact]
    public async Task Remove_WithNoIds_IsHarmless()
    {
        await _outbox.EnqueueAsync(NewEvent(), Ct);

        await _outbox.RemoveAsync([], Ct);

        Assert.Equal(1, await _outbox.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task RecordFailure_KeepsTheEventsInTheQueue()
    {
        var operationEvent = NewEvent();
        await _outbox.EnqueueAsync(operationEvent, Ct);

        await _outbox.RecordFailureAsync([operationEvent.Id], "server non raggiungibile", Ct);

        Assert.Equal([operationEvent.Id], (await _outbox.PeekBatchAsync(10, Ct)).Select(e => e.Id));
    }

    [Fact]
    public async Task Quarantine_RemovesEventsFromTheSendQueueButKeepsThem()
    {
        var poison = NewEvent();
        var healthy = NewEvent();
        await _outbox.EnqueueAsync(poison, Ct);
        await _outbox.EnqueueAsync(healthy, Ct);

        await _outbox.QuarantineAsync([poison.Id], "400: StationId mancante", Ct);

        Assert.Equal([healthy.Id], (await _outbox.PeekBatchAsync(10, Ct)).Select(e => e.Id));
        Assert.Equal(1, await _outbox.CountPendingAsync(Ct));
        Assert.Equal(1, await _outbox.CountQuarantinedAsync(Ct));
    }

    [Fact]
    public async Task Counts_AreZeroOnAnEmptyQueue()
    {
        Assert.Equal(0, await _outbox.CountPendingAsync(Ct));
        Assert.Equal(0, await _outbox.CountQuarantinedAsync(Ct));
    }

    [Fact]
    public async Task Events_SurviveAnApplicationRestart()
    {
        var operationEvent = NewEvent(barcode: "BC1");
        await _outbox.EnqueueAsync(operationEvent, Ct);

        var restartedDatabase = _temp.CreateDatabase();
        await restartedDatabase.InitializeAsync(Ct);
        var restartedOutbox = new SqliteOutbox(restartedDatabase, TimeProvider.System);

        var batch = await restartedOutbox.PeekBatchAsync(10, Ct);

        Assert.Equal([operationEvent.Id], batch.Select(e => e.Id));
    }

    [Fact]
    public async Task Initialize_CalledAgain_KeepsTheExistingData()
    {
        await _outbox.EnqueueAsync(NewEvent(), Ct);

        await _temp.Database.InitializeAsync(Ct);

        Assert.Equal(1, await _outbox.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task ConcurrentEnqueues_AreAllStored()
    {
        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => _outbox.EnqueueAsync(NewEvent(), Ct)));

        Assert.Equal(50, await _outbox.CountPendingAsync(Ct));
    }

    [Fact]
    public async Task OutboxEventSink_WritesIntoTheOutbox()
    {
        var sink = new OutboxEventSink(_outbox);
        var operationEvent = NewEvent();

        await sink.WriteAsync(operationEvent, Ct);

        Assert.Equal([operationEvent.Id], (await _outbox.PeekBatchAsync(10, Ct)).Select(e => e.Id));
    }
}
