using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Storage;

namespace PtlOrchestrator.Station.Tests.Support;

/// <summary>Database SQLite locale in una cartella temporanea, eliminata al termine del test.</summary>
internal sealed class TempDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PtlStationTests-" + Guid.NewGuid().ToString("N"));

    public TempDatabase()
    {
        Database = CreateDatabase();
        Database.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public LocalDatabase Database { get; }

    /// <summary>Una nuova istanza sullo stesso file: simula il riavvio dell'applicazione.</summary>
    public LocalDatabase CreateDatabase()
        => new(Options.Create(new LocalDataOptions { DatabasePath = Path.Combine(_directory, "station.db") }));

    public void Dispose()
    {
        // il pool tiene aperti i file: senza questo la cartella non si può eliminare
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
