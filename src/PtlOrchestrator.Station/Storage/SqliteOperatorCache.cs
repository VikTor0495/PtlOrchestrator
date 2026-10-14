using System.Globalization;
using Microsoft.Data.Sqlite;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Station.Storage;

/// <summary>
/// Cache locale degli operatori: è sia la destinazione della sincronizzazione (<see cref="IOperatorCache"/>)
/// sia l'anagrafica usata dal login (<see cref="IOperatorDirectory"/>), che quindi funziona senza rete.
/// </summary>
public sealed class SqliteOperatorCache(LocalDatabase database) : IOperatorCache, IOperatorDirectory
{
    private const string LastSyncKey = "operators_last_sync";
    private const string SelectColumns = "SELECT id, name, badge_code, pin_hash, is_active, updated_at FROM cached_operators";

    private readonly LocalDatabase _database = database;

    public async Task UpsertAsync(IReadOnlyCollection<OperatorDto> operators, CancellationToken cancellationToken)
    {
        if (operators.Count == 0)
            return;

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var operatorDto in operators)
            await UpsertOneAsync(connection, transaction, operatorDto, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastSyncAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT value FROM sync_state WHERE key = $key";
        command.Parameters.AddWithValue("$key", LastSyncKey);

        var value = await command.ExecuteScalarAsync(cancellationToken) as string;

        return value is null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    public async Task SetLastSyncAsync(DateTimeOffset serverTime, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO sync_state (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        command.Parameters.AddWithValue("$key", LastSyncKey);
        command.Parameters.AddWithValue("$value", serverTime.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<OperatorDto?> FindByBadgeAsync(string badgeCode, CancellationToken cancellationToken)
        => QuerySingleAsync("WHERE badge_code = $value", badgeCode, cancellationToken);

    public Task<OperatorDto?> FindByIdAsync(Guid operatorId, CancellationToken cancellationToken)
        => QuerySingleAsync("WHERE id = $value", operatorId.ToString(), cancellationToken);

    public async Task<IReadOnlyList<OperatorDto>> GetActiveAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"{SelectColumns} WHERE is_active = 1 ORDER BY name";

        var operators = new List<OperatorDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            operators.Add(Map(reader));

        return operators;
    }

    private async Task<OperatorDto?> QuerySingleAsync(string whereClause, string value, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = $"{SelectColumns} {whereClause} LIMIT 1";
        command.Parameters.AddWithValue("$value", value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static async Task UpsertOneAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        OperatorDto operatorDto,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;

        // l'aggiornamento è applicato solo se il dato del server è più recente: una sync ripetuta o riordinata non fa regredire la cache
        command.CommandText = """
            INSERT INTO cached_operators (id, name, badge_code, pin_hash, is_active, updated_at)
            VALUES ($id, $name, $badge, $pin, $active, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                badge_code = excluded.badge_code,
                pin_hash = excluded.pin_hash,
                is_active = excluded.is_active,
                updated_at = excluded.updated_at
            WHERE excluded.updated_at >= cached_operators.updated_at
            """;
        command.Parameters.AddWithValue("$id", operatorDto.Id.ToString());
        command.Parameters.AddWithValue("$name", operatorDto.Name);
        command.Parameters.AddWithValue("$badge", (object?)operatorDto.BadgeCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$pin", (object?)operatorDto.PinHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$active", operatorDto.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", FormatSortable(operatorDto.UpdatedAt));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // formato UTC a lunghezza fissa: il confronto fra stringhe coincide con quello cronologico
    private static string FormatSortable(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    private static OperatorDto Map(SqliteDataReader reader)
        => new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetInt32(4) == 1,
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
}
