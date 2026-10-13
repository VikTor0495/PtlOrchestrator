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
        ["Lightstep:ControllerIp"] = "192.168.1.9",
        ["Lightstep:ControllerPort"] = "15003",
        ["LimitBarcode:FileName"] = "prodotti_config.csv",
        ["ProcessTimeout:Minutes"] = "5",
        ["Carts:0:CartId"] = "1",
        ["Carts:0:Baskets:0:BasketId"] = "0001",
        ["Carts:0:Baskets:1:BasketId"] = "0002",
        ["Carts:1:CartId"] = "2",
        ["Carts:1:Baskets:0:BasketId"] = "0003"
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
    public void CartContainer_IsBuiltFromTheConfiguredCartsAndBaskets()
    {
        using var provider = BuildProvider(ValidConfiguration());

        var carts = provider.GetRequiredService<CartContainer>().GetCarts().ToList();

        Assert.Equal(["1", "2"], carts.Select(c => c.CartId));
        Assert.Equal(["0001", "0002"], carts[0].GetBaskets.Select(b => b.BasketId));
        Assert.Equal(["0003"], carts[1].GetBaskets.Select(b => b.BasketId));
        Assert.All(carts.SelectMany(c => c.GetBaskets), basket => Assert.True(basket.IsEmpty));
    }

    [Fact]
    public void ConfiguredOptions_AreBound()
    {
        using var provider = BuildProvider(ValidConfiguration());

        Assert.Equal("ST-01", provider.GetRequiredService<IOptions<StationOptions>>().Value.StationId);
        Assert.Equal("192.168.1.9", provider.GetRequiredService<IOptions<LightstepOptions>>().Value.ControllerIp);
        Assert.Equal(15003, provider.GetRequiredService<IOptions<LightstepOptions>>().Value.ControllerPort);
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
        { "Lightstep:ControllerIp", "" },
        { "Lightstep:ControllerPort", "0" },
        { "LimitBarcode:FileName", "" },
        { "ProcessTimeout:Minutes", "0" },
        { "ProcessTimeout:Minutes", "-3" },
        { "Carts:0:CartId", "" },
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
            _ = provider.GetRequiredService<IOptions<LightstepOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<BarcodeLimitOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<TimeoutOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<List<CartOptions>>>().Value;
        });

        Assert.IsType<OptionsValidationException>(exception);
    }

    [Fact]
    public void DuplicatedBasketIdsAcrossCarts_AreRejected()
    {
        var settings = ValidConfiguration();
        settings["Carts:1:Baskets:0:BasketId"] = "0001";
        using var provider = BuildProvider(settings);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<CartContainer>());

        Assert.Contains("univoci", exception.Message);
    }

    [Fact]
    public void CartsWithoutBaskets_AreRejected()
    {
        var settings = ValidConfiguration()
            .Where(pair => !pair.Key.StartsWith("Carts:1:Baskets"))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        using var provider = BuildProvider(settings);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<CartContainer>());

        Assert.Contains("almeno una cesta", exception.Message);
    }

    [Fact]
    public void MissingCarts_AreRejected()
    {
        var settings = ValidConfiguration()
            .Where(pair => !pair.Key.StartsWith("Carts:"))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        using var provider = BuildProvider(settings);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<CartContainer>());

        Assert.Contains("almeno un carrello", exception.Message);
    }
}
