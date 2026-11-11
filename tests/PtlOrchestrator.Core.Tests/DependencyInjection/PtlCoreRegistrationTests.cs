using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Core.Tests.Support;
using PtlOrchestrator.DependencyInjection;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.DependencyInjection;

public class PtlCoreRegistrationTests
{
    private static Dictionary<string, string?> ValidConfiguration() => new()
    {
        ["Station:StationId"] = "ST-01",
        ["LimitBarcode:FileName"] = "prodotti_config.csv",
        ["ProcessTimeout:Minutes"] = "5"
    };

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPtlCore(configuration);

        // le parti che dipendono dall'ambiente le fornisce l'host
        services.AddSingleton<IBarcodeSource>(new FakeBarcodeSource());
        services.AddSingleton<IOperatorDirectory>(new FakeOperatorDirectory());
        services.AddSingleton<IOperationEventSink>(new CollectingOperationEventSink());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Theory]
    [InlineData(typeof(ICartManager))]
    [InlineData(typeof(IScanDispatcher))]
    [InlineData(typeof(IScanActivity))]
    [InlineData(typeof(IOperatorLoginService))]
    [InlineData(typeof(IOperationRecorder))]
    [InlineData(typeof(IOperatorSessionContext))]
    [InlineData(typeof(IAppProcessingState))]
    [InlineData(typeof(IBasketLimitService))]
    [InlineData(typeof(IPtlCommandService))]
    [InlineData(typeof(ILightstepConnectionService))]
    [InlineData(typeof(ICartAssignmentClient))]
    [InlineData(typeof(CartContainer))]
    public void EveryCoreServiceCanBeResolved(Type serviceType)
    {
        using var provider = BuildProvider(ValidConfiguration());

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Fact]
    public void ScanListener_IsRegisteredAsTheHostedServiceAndAsTheScanActivitySource()
    {
        using var provider = BuildProvider(ValidConfiguration());

        var hosted = provider.GetServices<IHostedService>().OfType<ScanListener>().Single();
        var activity = provider.GetRequiredService<IScanActivity>();

        Assert.Same(hosted, activity);
    }

    [Fact]
    public void CartContainer_StartsWithoutActiveCarts()
    {
        using var provider = BuildProvider(ValidConfiguration());

        // i carrelli arrivano dal server al check-in: il turno parte da zero
        var container = provider.GetRequiredService<CartContainer>();

        Assert.Empty(container.GetCarts());
    }

    [Fact]
    public void ConfiguredOptions_AreBound()
    {
        using var provider = BuildProvider(ValidConfiguration());

        Assert.Equal("ST-01", provider.GetRequiredService<IOptions<StationOptions>>().Value.StationId);
        Assert.Equal(5, provider.GetRequiredService<IOptions<TimeoutOptions>>().Value.Minutes);
    }

    [Fact]
    public async Task ScanListener_StartsAndStopsCleanly()
    {
        using var provider = BuildProvider(ValidConfiguration());
        var listener = provider.GetServices<IHostedService>().OfType<ScanListener>().Single();

        var exception = await Record.ExceptionAsync(async () =>
        {
            await listener.StartAsync(CancellationToken.None);
            await listener.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        });

        Assert.Null(exception);
    }

    // ---- validazione della configurazione ----

    public static TheoryData<string, string?> InvalidSettings => new()
    {
        { "Station:StationId", "" },
        { "Station:StationId", null },
        { "LimitBarcode:FileName", "" },
        { "ProcessTimeout:Minutes", "0" },
        { "ProcessTimeout:Minutes", "-3" },
    };

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void InvalidSettings_AreRejectedWhenTheOptionsAreRead(string key, string? value)
    {
        var settings = ValidConfiguration();
        settings[key] = value;
        using var provider = BuildProvider(settings);

        var exception = Record.Exception(() =>
        {
            _ = provider.GetRequiredService<IOptions<StationOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<BarcodeLimitOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<TimeoutOptions>>().Value;
        });

        Assert.IsType<OptionsValidationException>(exception);
    }
}
