using Microsoft.EntityFrameworkCore;
using PtlOrchestrator.Api.Persistence.Entities;

namespace PtlOrchestrator.Api.Persistence;

public class PtlDbContext : DbContext
{
    public PtlDbContext(DbContextOptions<PtlDbContext> options) : base(options)
    {
    }

    // permette a contesti derivati (es. test su SQLite) di passare le proprie opzioni
    protected PtlDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<OperatorEntity> Operators => Set<OperatorEntity>();
    public DbSet<StationEntity> Stations => Set<StationEntity>();
    public DbSet<WorkSessionEntity> WorkSessions => Set<WorkSessionEntity>();
    public DbSet<OperationEventEntity> OperationEvents => Set<OperationEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PtlDbContext).Assembly);

        SnakeCaseNaming.Apply(modelBuilder);
    }
}
