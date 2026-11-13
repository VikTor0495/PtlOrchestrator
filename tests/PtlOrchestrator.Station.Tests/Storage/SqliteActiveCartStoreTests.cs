using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Storage;

public sealed class SqliteActiveCartStoreTests : IDisposable
{
    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task SavedCarts_AreFoundAgainAfterARestart()
    {
        await new SqliteActiveCartStore(_database.Database).SaveAsync(["2", "1"], CancellationToken.None);

        var reopened = new SqliteActiveCartStore(_database.CreateDatabase());
        var carts = await reopened.LoadAsync(CancellationToken.None);

        Assert.Equal(["1", "2"], carts);
    }

    [Fact]
    public async Task Saving_ReplacesThePreviousList()
    {
        var store = new SqliteActiveCartStore(_database.Database);
        await store.SaveAsync(["1", "2", "3"], CancellationToken.None);

        await store.SaveAsync(["2"], CancellationToken.None);

        Assert.Equal(["2"], await store.LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SavingAnEmptyList_ClearsEverything()
    {
        var store = new SqliteActiveCartStore(_database.Database);
        await store.SaveAsync(["1"], CancellationToken.None);

        await store.SaveAsync([], CancellationToken.None);

        Assert.Empty(await store.LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DuplicatedCodes_AreStoredOnce()
    {
        var store = new SqliteActiveCartStore(_database.Database);

        await store.SaveAsync(["1", "1"], CancellationToken.None);

        Assert.Equal(["1"], await store.LoadAsync(CancellationToken.None));
    }
}
