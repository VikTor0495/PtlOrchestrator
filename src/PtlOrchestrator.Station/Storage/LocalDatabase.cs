using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Station.Configuration;

namespace PtlOrchestrator.Station.Storage;

/// <summary>
/// Database SQLite locale della postazione: coda degli eventi da inviare e cache degli operatori.
/// Ogni operazione apre una connessione breve; SQLite gestisce la concorrenza con il journal WAL.
/// </summary>
public sealed class LocalDatabase
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS outbox_events (
            seq        INTEGER PRIMARY KEY AUTOINCREMENT,
            id         TEXT    NOT NULL UNIQUE,
            payload    TEXT    NOT NULL,
            created_at TEXT    NOT NULL,
            attempts   INTEGER NOT NULL DEFAULT 0,
            last_error TEXT,
            dead       INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS ix_outbox_events_dead_seq ON outbox_events (dead, seq);

        CREATE TABLE IF NOT EXISTS cached_operators (
            id         TEXT PRIMARY KEY,
            name       TEXT NOT NULL,
            badge_code TEXT,
            pin_hash   TEXT,
            is_active  INTEGER NOT NULL,
            updated_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_cached_operators_badge ON cached_operators (badge_code);

        CREATE TABLE IF NOT EXISTS sync_state (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """;

    private readonly string _connectionString;
    private readonly string _databasePath;

    public LocalDatabase(IOptions<LocalDataOptions> options)
    {
        _databasePath = Path.GetFullPath(options.Value.DatabasePath, AppContext.BaseDirectory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // WAL: lettori e scrittori non si bloccano; FULL: un evento confermato sopravvive a un'interruzione di corrente
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
