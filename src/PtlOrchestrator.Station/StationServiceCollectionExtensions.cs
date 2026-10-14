using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PtlOrchestrator.DependencyInjection;
using PtlOrchestrator.Service;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Presentation;
using PtlOrchestrator.Station.Scanner;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Sync;

namespace PtlOrchestrator.Station;

public static class StationServiceCollectionExtensions
{
    public const string ApiKeyHeader = "X-Api-Key";

    /// <summary>
    /// Composizione completa della postazione: il core PTL più database locale, sincronizzazione
    /// con il server centrale e scanner seriale. Non registra il logging né le finestre.
    /// </summary>
    public static IServiceCollection AddStation(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidatedOptions(services, configuration);

        // il database deve essere pronto prima di tutti gli altri servizi in background (si avviano in ordine di registrazione)
        services.AddSingleton<LocalDatabase>();
        services.AddHostedService<LocalDatabaseInitializer>();

        services.AddPtlCore(configuration);

        services.AddSingleton<SqliteOperatorCache>();
        services.AddSingleton<IOperatorCache>(sp => sp.GetRequiredService<SqliteOperatorCache>());
        services.AddSingleton<IOperatorDirectory>(sp => sp.GetRequiredService<SqliteOperatorCache>());
        services.AddSingleton<IOutbox, SqliteOutbox>();
        services.AddSingleton<IOperationEventSink, OutboxEventSink>();

        services.AddSingleton<ISerialConnectionFactory, SystemSerialConnectionFactory>();
        services.AddSingleton<IBarcodeSource, SerialBarcodeSource>();

        services.AddHttpClient<ICentralClient, HttpCentralClient>((provider, client) =>
        {
            var central = provider.GetRequiredService<IOptions<CentralOptions>>().Value;

            // senza la barra finale l'ultimo segmento dell'indirizzo base verrebbe scartato nei percorsi relativi
            client.BaseAddress = new Uri(central.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(central.TimeoutSeconds);
            client.DefaultRequestHeaders.Add(ApiKeyHeader, central.ApiKey);
        });

        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddSingleton<EventSyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<EventSyncService>());
        services.AddSingleton<OperatorSyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<OperatorSyncService>());

        return services;
    }

    private static void AddValidatedOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<LocalDataOptions>()
            .Bind(configuration.GetSection("LocalData"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.DatabasePath), "LocalData:DatabasePath non valido")
            .ValidateOnStart();

        services
            .AddOptions<CentralOptions>()
            .Bind(configuration.GetSection("Central"))
            .Validate(
                o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
                "Central:BaseUrl deve essere un indirizzo http/https assoluto")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Central:ApiKey mancante")
            .Validate(o => o.TimeoutSeconds > 0, "Central:TimeoutSeconds deve essere maggiore di zero")
            .Validate(o => o.EventSyncIntervalSeconds > 0, "Central:EventSyncIntervalSeconds deve essere maggiore di zero")
            .Validate(o => o.OperatorSyncIntervalSeconds > 0, "Central:OperatorSyncIntervalSeconds deve essere maggiore di zero")
            .Validate(o => o.EventBatchSize > 0, "Central:EventBatchSize deve essere maggiore di zero")
            .ValidateOnStart();

        services
            .AddOptions<SerialScannerOptions>()
            .Bind(configuration.GetSection("SerialScanner"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.PortName), "SerialScanner:PortName mancante")
            .Validate(o => o.BaudRate > 0, "SerialScanner:BaudRate non valido")
            .Validate(o => o.MaxCodeLength > 0, "SerialScanner:MaxCodeLength deve essere maggiore di zero")
            .ValidateOnStart();
    }

    /// <summary>Crea lo schema del database locale prima che parta qualunque altro servizio.</summary>
    private sealed class LocalDatabaseInitializer(LocalDatabase database) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => database.InitializeAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
