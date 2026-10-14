using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Service;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Scanner;
using PtlOrchestrator.Station.Storage;
using PtlOrchestrator.Station.Sync;

namespace PtlOrchestrator.Station.Tests;

public sealed class StationCompositionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PtlStationComposition-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private Dictionary<string, string?> ValidSettings() => new()
    {
        ["Station:StationId"] = "ST-TEST",
        ["Lightstep:ControllerIp"] = "127.0.0.1",
        ["Lightstep:ControllerPort"] = "15003",
        ["Carts:0:CartId"] = "1",
        ["Carts:0:Baskets:0:BasketId"] = "0001",
        ["LimitBarcode:FileName"] = "limiti.csv",
        ["ProcessTimeout:Minutes"] = "5",
        ["LocalData:DatabasePath"] = Path.Combine(_directory, "station.db"),
        ["Central:BaseUrl"] = "http://central.test:5000",
        ["Central:ApiKey"] = "chiave",
        ["SerialScanner:PortName"] = "COM3"
    };

    private ServiceProvider BuildProvider(Action<Dictionary<string, string?>>? customize = null)
    {
        var settings = ValidSettings();
        customize?.Invoke(settings);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStation(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AddStation_ResolvesTheServicesTheStationNeeds()
    {
        using var provider = BuildProvider();

        Assert.IsType<OutboxEventSink>(provider.GetRequiredService<IOperationEventSink>());
        Assert.IsType<SerialBarcodeSource>(provider.GetRequiredService<IBarcodeSource>());
        Assert.IsType<SqliteOperatorCache>(provider.GetRequiredService<IOperatorDirectory>());
        Assert.IsType<SqliteOutbox>(provider.GetRequiredService<IOutbox>());
        Assert.IsType<HttpCentralClient>(provider.GetRequiredService<ICentralClient>());
        Assert.NotNull(provider.GetRequiredService<IOperatorLoginService>());
        Assert.NotNull(provider.GetRequiredService<IScanDispatcher>());
    }

    [Fact]
    public void AddStation_OperatorCacheIsTheSameInstanceForSyncAndLogin()
    {
        using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<IOperatorCache>(), provider.GetRequiredService<IOperatorDirectory>());
    }

    [Fact]
    public async Task AddStation_DatabaseInitializerRunsBeforeTheOtherBackgroundServices()
    {
        using var provider = BuildProvider();

        var hosted = provider.GetServices<IHostedService>().ToList();
        var initializerIndex = hosted.FindIndex(s => s.GetType().Name == "LocalDatabaseInitializer");

        Assert.Equal(0, initializerIndex);
        Assert.Contains(hosted, s => s is EventSyncService);
        Assert.Contains(hosted, s => s is OperatorSyncService);

        await hosted[initializerIndex].StartAsync(CancellationToken.None);
        Assert.True(System.IO.File.Exists(Path.Combine(_directory, "station.db")));
    }

    [Fact]
    public void AddStation_CentralClientUsesBaseUrlTimeoutAndApiKey()
    {
        using var provider = BuildProvider(s =>
        {
            s["Central:BaseUrl"] = "http://central.test:5000/";
            s["Central:TimeoutSeconds"] = "7";
        });

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        // il client tipizzato riceve un HttpClient creato con la stessa configurazione
        var client = factory.CreateClient(nameof(ICentralClient));

        Assert.Equal(new Uri("http://central.test:5000/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(7), client.Timeout);
        Assert.Equal("chiave", client.DefaultRequestHeaders.GetValues("X-Api-Key").Single());
    }

    [Theory]
    [InlineData("Central:BaseUrl", "")]
    [InlineData("Central:BaseUrl", "non un indirizzo")]
    [InlineData("Central:BaseUrl", "ftp://server")]
    [InlineData("Central:ApiKey", "")]
    [InlineData("Central:EventBatchSize", "0")]
    [InlineData("Central:TimeoutSeconds", "0")]
    [InlineData("SerialScanner:PortName", "")]
    [InlineData("SerialScanner:MaxCodeLength", "0")]
    [InlineData("LocalData:DatabasePath", "")]
    public void AddStation_InvalidSettings_AreRejectedAtStartup(string key, string value)
    {
        using var provider = BuildProvider(s => s[key] = value);

        var rejected = Record.Exception(() =>
        {
            _ = provider.GetRequiredService<IOptions<CentralOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<SerialScannerOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<LocalDataOptions>>().Value;
        });

        Assert.IsType<OptionsValidationException>(rejected);
    }
}
