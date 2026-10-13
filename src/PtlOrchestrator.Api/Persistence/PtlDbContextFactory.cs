using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PtlOrchestrator.Api.Persistence;

/// <summary>
/// Usato solo da "dotnet ef" per generare le migrazioni: non apre connessioni,
/// la stringa serve unicamente a scegliere il provider Npgsql.
/// </summary>
internal sealed class PtlDbContextFactory : IDesignTimeDbContextFactory<PtlDbContext>
{
    public PtlDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PtlDbContext>()
            .UseNpgsql("Host=localhost;Database=ptl_design_time")
            .Options;

        return new PtlDbContext(options);
    }
}
