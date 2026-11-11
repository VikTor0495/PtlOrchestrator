using Microsoft.Data.Sqlite;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Tests.Support;

namespace PtlOrchestrator.Station.Tests.Storage;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly TempDatabase _database = new();
    private readonly DateTimeOffset _at = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task SavedValues_AreReadBackAfterARestart()
    {
        var store = new SettingsStore(_database.Database);
        await store.SaveAsync(new Dictionary<string, string> { ["Central:BaseUrl"] = "http://server:5000" }, _at, CancellationToken.None);

        var reopened = new SettingsStore(_database.CreateDatabase());
        var values = await reopened.LoadAsync(CancellationToken.None);

        Assert.Equal("http://server:5000", values["Central:BaseUrl"]);
    }

    [Fact]
    public async Task EveryChange_IsKeptInTheHistoryWithOldAndNewValue()
    {
        var store = new SettingsStore(_database.Database);
        await store.SaveAsync(new Dictionary<string, string> { ["ProcessTimeout:Minutes"] = "5" }, _at, CancellationToken.None);
        await store.SaveAsync(new Dictionary<string, string> { ["ProcessTimeout:Minutes"] = "8" }, _at.AddDays(1), CancellationToken.None);

        var history = ReadHistory();

        Assert.Equal([(null, "5"), ("5", "8")], history.Select(h => (h.Old, h.New)));
    }

    [Fact]
    public async Task UnchangedValue_IsNotWrittenAgain()
    {
        var store = new SettingsStore(_database.Database);
        await store.SaveAsync(new Dictionary<string, string> { ["ProcessTimeout:Minutes"] = "5" }, _at, CancellationToken.None);

        await store.SaveAsync(new Dictionary<string, string> { ["ProcessTimeout:Minutes"] = "5" }, _at.AddDays(1), CancellationToken.None);

        Assert.Single(ReadHistory());
    }

    [Fact]
    public async Task KeysThatAreNotEditable_AreIgnored()
    {
        var store = new SettingsStore(_database.Database);

        await store.SaveAsync(new Dictionary<string, string> { ["LocalData:DatabasePath"] = "altro.db" }, _at, CancellationToken.None);

        var values = await store.LoadAsync(CancellationToken.None);
        Assert.Empty(values);
    }

    [Fact]
    public void ReadAll_OfAMissingDatabase_IsEmpty()
    {
        var values = SettingsStore.ReadAll(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nope.db"));

        Assert.Empty(values);
    }

    [Fact]
    public async Task ReadAll_ReturnsTheSavedValuesForTheStartupConfiguration()
    {
        var store = new SettingsStore(_database.Database);
        await store.SaveAsync(new Dictionary<string, string> { ["Station:StationId"] = "ST-CASSA" }, _at, CancellationToken.None);
        SqliteConnection.ClearAllPools();

        var values = SettingsStore.ReadAll(_database.DatabasePath);

        Assert.Equal("ST-CASSA", values["Station:StationId"]);
    }

    private string _databaseFile() => _database.DatabasePath;

    private List<(string? Old, string New)> ReadHistory()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databaseFile(),
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT old_value, new_value FROM app_settings_history ORDER BY seq";

        var rows = new List<(string? Old, string New)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            rows.Add((reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetString(1)));

        return rows;
    }
}
