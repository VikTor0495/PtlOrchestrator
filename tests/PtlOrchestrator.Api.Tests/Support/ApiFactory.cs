using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using PtlOrchestrator.Api.Persistence;

namespace PtlOrchestrator.Api.Tests.Support;

/// <summary>
/// Avvia l'applicazione reale in memoria, sostituendo Postgres con SQLite in memoria
/// e l'orologio di sistema con uno controllabile. Ogni istanza ha un database proprio.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string StationKey = "test-station-key-0123456789";
    public const string AdminKey = "test-admin-key-0123456789ab";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKeys:StationKey"] = StationKey,
                ["ApiKeys:AdminKey"] = AdminKey,
                ["Database:MigrateOnStartup"] = "false",
                ["ConnectionStrings:Ptl"] = "Host=non-usato"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PtlDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<PtlDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<PtlDbContext>>();

            services.AddDbContext<PtlDbContext, SqlitePtlDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<PtlDbContext>().Database.EnsureCreated();

        return host;
    }

    public HttpClient CreateClientWithKey(string? apiKey)
    {
        var client = CreateClient();

        if (apiKey is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        return client;
    }

    public async Task WithDbContextAsync(Func<PtlDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<PtlDbContext>());
    }

    public async Task<T> WithDbContextAsync<T>(Func<PtlDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<PtlDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _connection.Dispose();
    }
}
