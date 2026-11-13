using Microsoft.Data.Sqlite;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Station.Storage;

/// <summary>Archivio su SQLite dei carrelli attivi: li ritrova dopo una chiusura o un riavvio della postazione.</summary>
public sealed class SqliteActiveCartStore(LocalDatabase database) : IActiveCartStore
{
    private readonly LocalDatabase _database = database;

    public async Task SaveAsync(IReadOnlyCollection<string> cartCodes, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM active_carts";
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var code in cartCodes.Distinct())
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO active_carts (cart_code) VALUES ($code)";
            insert.Parameters.AddWithValue("$code", code);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken)
    {
        var codes = new List<string>();

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT cart_code FROM active_carts ORDER BY cart_code";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            codes.Add(reader.GetString(0));

        return codes;
    }
}
