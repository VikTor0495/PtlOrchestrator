using Microsoft.Data.Sqlite;

namespace PtlOrchestrator.Station.Storage;

/// <summary>
/// Impostazioni della postazione modificabili dalla GUI. Sono salvate nel database locale e
/// sovrascrivono <c>appsettings.json</c>; ogni modifica resta nello storico con valore precedente e nuovo.
/// </summary>
public sealed class SettingsStore(LocalDatabase database)
{
    private readonly LocalDatabase _database = database;

    /// <summary>Chiavi che la GUI permette di modificare (sezione:chiave, come in configurazione).</summary>
    public static readonly IReadOnlyList<string> EditableKeys =
    [
        "Station:StationId",
        "Central:BaseUrl",
        "Central:ApiKey",
        "Central:EventSyncIntervalSeconds",
        "Central:OperatorSyncIntervalSeconds",
        "Central:CartCatalogSyncIntervalSeconds",
        "LimitBarcode:FileName",
        "ProcessTimeout:Minutes",
        "SerialScanner:PortName",
        "SerialScanner:BaudRate",
        "Lightstep:LicenzeKey"
    ];

    /// <summary>
    /// Legge le impostazioni salvate senza bisogno che il database sia già inizializzato: all'avvio
    /// la configurazione viene composta prima dell'host. Se il file o la tabella non esistono, non c'è nulla da sovrascrivere.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> ReadAll(string databasePath)
    {
        var values = new Dictionary<string, string?>();

        if (!System.IO.File.Exists(databasePath))
            return values;

        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT key, value FROM app_settings";

            using var reader = command.ExecuteReader();
            while (reader.Read())
                values[reader.GetString(0)] = reader.GetString(1);
        }
        catch (SqliteException)
        {
            // schema non ancora creato (prima installazione): nessuna impostazione salvata
            return new Dictionary<string, string?>();
        }

        return values;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>();

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM app_settings";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            values[reader.GetString(0)] = reader.GetString(1);

        return values;
    }

    /// <summary>Salva le modifiche e le registra nello storico, in un'unica transazione. Ignora le chiavi non modificabili.</summary>
    public async Task SaveAsync(IReadOnlyDictionary<string, string> changes, DateTimeOffset changedAt, CancellationToken cancellationToken)
    {
        var accepted = changes.Where(c => EditableKeys.Contains(c.Key)).ToList();
        if (accepted.Count == 0)
            return;

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var (key, newValue) in accepted)
        {
            string? oldValue;
            await using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT value FROM app_settings WHERE key = $key";
                read.Parameters.AddWithValue("$key", key);
                oldValue = await read.ExecuteScalarAsync(cancellationToken) as string;
            }

            if (oldValue == newValue)
                continue;

            await using (var upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = """
                    INSERT INTO app_settings (key, value, updated_at) VALUES ($key, $value, $at)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
                    """;
                upsert.Parameters.AddWithValue("$key", key);
                upsert.Parameters.AddWithValue("$value", newValue);
                upsert.Parameters.AddWithValue("$at", changedAt.ToString("O"));
                await upsert.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var history = connection.CreateCommand())
            {
                history.Transaction = transaction;
                history.CommandText = """
                    INSERT INTO app_settings_history (key, old_value, new_value, changed_at)
                    VALUES ($key, $old, $new, $at)
                    """;
                history.Parameters.AddWithValue("$key", key);
                history.Parameters.AddWithValue("$old", (object?)oldValue ?? DBNull.Value);
                history.Parameters.AddWithValue("$new", newValue);
                history.Parameters.AddWithValue("$at", changedAt.ToString("O"));
                await history.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
