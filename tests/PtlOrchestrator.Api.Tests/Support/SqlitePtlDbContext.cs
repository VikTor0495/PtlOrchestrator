using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PtlOrchestrator.Api.Persistence;

namespace PtlOrchestrator.Api.Tests.Support;

/// <summary>
/// Variante usata solo nei test: SQLite non sa ordinare né confrontare i DateTimeOffset,
/// quindi li salva come tick UTC. In produzione Postgres usa timestamptz nativo.
/// </summary>
public sealed class SqlitePtlDbContext(DbContextOptions<SqlitePtlDbContext> options) : PtlDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
}
