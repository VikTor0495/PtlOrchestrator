using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Storage;

public sealed class SqliteOperatorCacheTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset T0 = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    private readonly TempDatabase _temp = new();
    private readonly SqliteOperatorCache _cache;

    public SqliteOperatorCacheTests()
    {
        _cache = new SqliteOperatorCache(_temp.Database);
    }

    public void Dispose() => _temp.Dispose();

    private static OperatorDto NewOperator(
        string name = "Mario Rossi",
        string? badge = "BADGE-1",
        string? pinHash = "v1.1.AAAA.AAAA",
        bool isActive = true,
        DateTimeOffset? updatedAt = null,
        Guid? id = null)
        => new(id ?? Guid.NewGuid(), name, badge, pinHash, isActive, updatedAt ?? T0);

    [Fact]
    public async Task Upsert_ThenFindById_ReturnsTheOperatorWithAllItsFields()
    {
        var stored = NewOperator();

        await _cache.UpsertAsync([stored], Ct);

        Assert.Equal(stored, await _cache.FindByIdAsync(stored.Id, Ct));
    }

    [Fact]
    public async Task FindByBadge_ReturnsTheMatchingOperator()
    {
        var mario = NewOperator(name: "Mario", badge: "BADGE-1");
        var luisa = NewOperator(name: "Luisa", badge: "BADGE-2");
        await _cache.UpsertAsync([mario, luisa], Ct);

        var found = await _cache.FindByBadgeAsync("BADGE-2", Ct);

        Assert.Equal(luisa.Id, found!.Id);
    }

    [Fact]
    public async Task FindByBadge_ForUnknownBadge_ReturnsNull()
    {
        await _cache.UpsertAsync([NewOperator()], Ct);

        Assert.Null(await _cache.FindByBadgeAsync("SCONOSCIUTO", Ct));
    }

    [Fact]
    public async Task FindById_ForUnknownOperator_ReturnsNull()
    {
        Assert.Null(await _cache.FindByIdAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Operator_WithoutBadgeOrPin_IsStoredWithNulls()
    {
        var stored = NewOperator(badge: null, pinHash: null);

        await _cache.UpsertAsync([stored], Ct);

        var found = await _cache.FindByIdAsync(stored.Id, Ct);
        Assert.Null(found!.BadgeCode);
        Assert.Null(found.PinHash);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveOperatorsAndSortsByName()
    {
        await _cache.UpsertAsync(
        [
            NewOperator(name: "Zeno", badge: "B-3"),
            NewOperator(name: "Anna", badge: "B-1", isActive: false),
            NewOperator(name: "Marco", badge: "B-2"),
            NewOperator(name: "Bruno", badge: "B-4")
        ], Ct);

        var active = await _cache.GetActiveAsync(Ct);

        Assert.Equal(["Bruno", "Marco", "Zeno"], active.Select(o => o.Name));
    }

    [Fact]
    public async Task Upsert_OfNewerData_UpdatesTheOperator()
    {
        var original = NewOperator(name: "Mario", updatedAt: T0);
        await _cache.UpsertAsync([original], Ct);

        await _cache.UpsertAsync([original with { Name = "Mario Bianchi", UpdatedAt = T0.AddMinutes(5) }], Ct);

        var found = await _cache.FindByIdAsync(original.Id, Ct);
        Assert.Equal("Mario Bianchi", found!.Name);
        Assert.Equal(T0.AddMinutes(5), found.UpdatedAt);
    }

    [Fact]
    public async Task Upsert_OfOlderData_DoesNotRegressTheCache()
    {
        var original = NewOperator(name: "Mario", updatedAt: T0.AddMinutes(10));
        await _cache.UpsertAsync([original], Ct);

        await _cache.UpsertAsync([original with { Name = "Vecchio nome", UpdatedAt = T0 }], Ct);

        Assert.Equal("Mario", (await _cache.FindByIdAsync(original.Id, Ct))!.Name);
    }

    [Fact]
    public async Task Upsert_DeactivatingAnOperator_RemovesItFromTheActiveList()
    {
        var mario = NewOperator(updatedAt: T0);
        await _cache.UpsertAsync([mario], Ct);

        await _cache.UpsertAsync([mario with { IsActive = false, UpdatedAt = T0.AddMinutes(1) }], Ct);

        Assert.Empty(await _cache.GetActiveAsync(Ct));
        Assert.False((await _cache.FindByIdAsync(mario.Id, Ct))!.IsActive);
    }

    [Fact]
    public async Task Upsert_WithNoOperators_IsHarmless()
    {
        await _cache.UpsertAsync([], Ct);

        Assert.Empty(await _cache.GetActiveAsync(Ct));
    }

    [Fact]
    public async Task LastSync_IsNullUntilItIsSet()
    {
        Assert.Null(await _cache.GetLastSyncAsync(Ct));
    }

    [Fact]
    public async Task LastSync_RoundTripsAndCanBeOverwritten()
    {
        await _cache.SetLastSyncAsync(T0, Ct);
        await _cache.SetLastSyncAsync(T0.AddHours(1), Ct);

        Assert.Equal(T0.AddHours(1), await _cache.GetLastSyncAsync(Ct));
    }

    [Fact]
    public async Task Operators_SurviveAnApplicationRestart()
    {
        var mario = NewOperator();
        await _cache.UpsertAsync([mario], Ct);

        var restartedDatabase = _temp.CreateDatabase();
        await restartedDatabase.InitializeAsync(Ct);
        var restartedCache = new SqliteOperatorCache(restartedDatabase);

        Assert.Equal(mario, await restartedCache.FindByBadgeAsync("BADGE-1", Ct));
    }

    [Fact]
    public async Task Badge_WithSqlSpecialCharacters_IsTreatedAsPlainText()
    {
        await _cache.UpsertAsync([NewOperator(badge: "X'; DROP TABLE cached_operators;--")], Ct);

        var found = await _cache.FindByBadgeAsync("X'; DROP TABLE cached_operators;--", Ct);

        Assert.NotNull(found);
        Assert.Single(await _cache.GetActiveAsync(Ct));
    }
}
