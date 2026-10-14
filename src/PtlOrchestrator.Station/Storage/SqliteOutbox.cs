using System.Text.Json;
using Microsoft.Data.Sqlite;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Station.Storage;

public sealed class SqliteOutbox(LocalDatabase database, TimeProvider timeProvider) : IOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly LocalDatabase _database = database;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task EnqueueAsync(OperationEvent operationEvent, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // OR IGNORE: lo stesso evento accodato due volte non deve produrre duplicati né errori
        command.CommandText = """
            INSERT OR IGNORE INTO outbox_events (id, payload, created_at)
            VALUES ($id, $payload, $createdAt)
            """;
        command.Parameters.AddWithValue("$id", operationEvent.Id.ToString());
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(operationEvent, Json));
        command.Parameters.AddWithValue("$createdAt", _timeProvider.GetUtcNow().ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperationEvent>> PeekBatchAsync(int maxCount, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT payload FROM outbox_events WHERE dead = 0 ORDER BY seq LIMIT $limit";
        command.Parameters.AddWithValue("$limit", maxCount);

        var events = new List<OperationEvent>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            events.Add(JsonSerializer.Deserialize<OperationEvent>(reader.GetString(0), Json)!);

        return events;
    }

    public Task RemoveAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken)
        => ExecuteForIdsAsync("DELETE FROM outbox_events WHERE id IN ({ids})", eventIds, null, cancellationToken);

    public Task RecordFailureAsync(IReadOnlyCollection<Guid> eventIds, string error, CancellationToken cancellationToken)
        => ExecuteForIdsAsync(
            "UPDATE outbox_events SET attempts = attempts + 1, last_error = $error WHERE id IN ({ids})",
            eventIds,
            error,
            cancellationToken);

    public Task QuarantineAsync(IReadOnlyCollection<Guid> eventIds, string reason, CancellationToken cancellationToken)
        => ExecuteForIdsAsync(
            "UPDATE outbox_events SET dead = 1, last_error = $error WHERE id IN ({ids})",
            eventIds,
            reason,
            cancellationToken);

    public Task<int> CountPendingAsync(CancellationToken cancellationToken)
        => CountAsync(dead: 0, cancellationToken);

    public Task<int> CountQuarantinedAsync(CancellationToken cancellationToken)
        => CountAsync(dead: 1, cancellationToken);

    private async Task<int> CountAsync(int dead, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM outbox_events WHERE dead = $dead";
        command.Parameters.AddWithValue("$dead", dead);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task ExecuteForIdsAsync(
        string sqlTemplate,
        IReadOnlyCollection<Guid> eventIds,
        string? error,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0)
            return;

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        // un parametro per id: nessuna concatenazione di valori nella query
        var idParameters = eventIds.Select((id, index) => ($"$id{index}", id.ToString())).ToList();
        command.CommandText = sqlTemplate.Replace("{ids}", string.Join(", ", idParameters.Select(p => p.Item1)));

        foreach (var (name, value) in idParameters)
            command.Parameters.AddWithValue(name, value);

        if (error is not null)
            command.Parameters.AddWithValue("$error", Truncate(error, 500));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
