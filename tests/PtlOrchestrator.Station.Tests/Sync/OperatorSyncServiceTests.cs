using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Sync;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Sync;

public class OperatorSyncServiceTests : IDisposable
{
    private sealed class FakeCentralClient : ICentralClient
    {
        public Func<DateTimeOffset?, CentralResult<OperatorsSnapshotResponse>> OnSnapshot { get; set; } = _ => throw new InvalidOperationException();

        public List<DateTimeOffset?> RequestedSince { get; } = [];

        public Task<CentralResult<IngestEventsResponse>> SendEventsAsync(
            IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CentralResult<OperatorsSnapshotResponse>> GetOperatorsSnapshotAsync(
            DateTimeOffset? since, CancellationToken cancellationToken)
        {
            RequestedSince.Add(since);
            return Task.FromResult(OnSnapshot(since));
        }
    }

    private static readonly DateTimeOffset T1 = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddHours(1);

    private readonly TempDatabase _database = new();
    private readonly SqliteOperatorCache _cache;
    private readonly FakeCentralClient _client = new();
    private readonly OperatorSyncService _service;

    public OperatorSyncServiceTests()
    {
        _cache = new SqliteOperatorCache(_database.Database);
        _service = new OperatorSyncService(
            _cache, _client, Options.Create(new CentralOptions()), TimeProvider.System,
            NullLogger<OperatorSyncService>.Instance);
    }

    public void Dispose() => _database.Dispose();

    private static OperatorDto Op(string name, string badge, bool active = true, Guid? id = null)
        => new(id ?? Guid.NewGuid(), name, badge, "hash", active, T1);

    private static CentralResult<OperatorsSnapshotResponse> Snapshot(DateTimeOffset serverTime, params OperatorDto[] operators)
        => CentralResult<OperatorsSnapshotResponse>.Ok(new OperatorsSnapshotResponse(operators, serverTime));

    [Fact]
    public async Task FirstSync_RequestsFullSnapshotAndStoresOperators()
    {
        var mario = Op("Mario", "B1");
        _client.OnSnapshot = _ => Snapshot(T1, mario);

        var updated = await _service.SyncOnceAsync(CancellationToken.None);

        Assert.Equal(1, updated);
        Assert.Null(_client.RequestedSince[0]);
        Assert.Equal(mario.Id, (await _cache.FindByBadgeAsync("B1", CancellationToken.None))!.Id);
        Assert.Equal(T1, await _cache.GetLastSyncAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NextSync_RequestsOnlyChangesSincePreviousServerTime()
    {
        _client.OnSnapshot = _ => Snapshot(T1, Op("Mario", "B1"));
        await _service.SyncOnceAsync(CancellationToken.None);

        _client.OnSnapshot = _ => Snapshot(T2);
        await _service.SyncOnceAsync(CancellationToken.None);

        Assert.Equal(T1, _client.RequestedSince[1]);
        Assert.Equal(T2, await _cache.GetLastSyncAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeactivatedOperator_IsUpdatedInCache()
    {
        var id = Guid.NewGuid();
        _client.OnSnapshot = _ => Snapshot(T1, Op("Mario", "B1", id: id));
        await _service.SyncOnceAsync(CancellationToken.None);

        _client.OnSnapshot = _ => Snapshot(T2, Op("Mario", "B1", active: false, id: id));
        await _service.SyncOnceAsync(CancellationToken.None);

        Assert.False((await _cache.FindByIdAsync(id, CancellationToken.None))!.IsActive);
        Assert.Empty(await _cache.GetActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ServerUnavailable_KeepsCacheAndSyncMarker()
    {
        _client.OnSnapshot = _ => Snapshot(T1, Op("Mario", "B1"));
        await _service.SyncOnceAsync(CancellationToken.None);

        _client.OnSnapshot = _ => CentralResult<OperatorsSnapshotResponse>.Transient("offline");
        var updated = await _service.SyncOnceAsync(CancellationToken.None);

        Assert.Null(updated);
        Assert.Equal(T1, await _cache.GetLastSyncAsync(CancellationToken.None));
        Assert.NotNull(await _cache.FindByBadgeAsync("B1", CancellationToken.None));
    }
}
