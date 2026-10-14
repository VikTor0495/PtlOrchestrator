using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.File;
using PtlOrchestrator.File.Impl;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Manager.Impl;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.DependencyInjection;

public static class PtlCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registra tutto ciò che serve a pilotare il PTL e a gestire sessioni e scansioni.
    /// L'host deve fornire le parti che dipendono dall'ambiente: <see cref="IBarcodeSource"/>,
    /// <see cref="IOperatorDirectory"/> e <see cref="IOperationEventSink"/>.
    /// </summary>
    public static IServiceCollection AddPtlCore(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidatedOptions(services, configuration);
        AddCartContainer(services);

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<ICartManager, CartManager>();
        services.AddSingleton<ILightstepConnectionService, LightstepConnectionService>();
        services.AddSingleton<IPtlCommandService, LightstepPtlCommandService>();
        services.AddSingleton<ICartReportWriter, CsvCartReportWriter>();
        services.AddSingleton<IFileReader<BarcodeLimit>, CsvBarcodeLimitReader>();
        services.AddSingleton<IBasketLimitService, BasketLimitService>();
        services.AddSingleton<IAppProcessingState, AppProcessingState>();

        services.AddSingleton<IOperatorSessionContext, OperatorSessionContext>();
        services.AddSingleton<IOperationRecorder, OperationRecorder>();
        services.AddSingleton<IOperatorLoginService, OperatorLoginService>();
        services.AddSingleton<IScanDispatcher, ScanDispatcher>();

        // prepara il PTL all'avvio e lo riconsegna allo spegnimento; parte prima dello ScanListener
        services.AddSingleton<PtlLifecycleService>();
        services.AddHostedService(sp => sp.GetRequiredService<PtlLifecycleService>());

        // un'unica istanza, esposta sia come servizio in background sia come sorgente di notifiche per la UI
        services.AddSingleton<ScanListener>();
        services.AddSingleton<IScanActivity>(sp => sp.GetRequiredService<ScanListener>());
        services.AddHostedService(sp => sp.GetRequiredService<ScanListener>());

        return services;
    }

    private static void AddValidatedOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<StationOptions>()
            .Bind(configuration.GetSection("Station"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.StationId), "Station:StationId non valido")
            .ValidateOnStart();

        services
            .AddOptions<LightstepOptions>()
            .Bind(configuration.GetSection("Lightstep"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ControllerIp), "Lightstep:ControllerIp non valida")
            .Validate(o => o.ControllerPort > 0, "Lightstep:ControllerPort non valida")
            .ValidateOnStart();

        services
            .AddOptions<BarcodeLimitOptions>()
            .Bind(configuration.GetSection("LimitBarcode"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.FileName), "LimitBarcode:FileName non valido")
            .ValidateOnStart();

        services
            .AddOptions<TimeoutOptions>()
            .Bind(configuration.GetSection("ProcessTimeout"))
            .Validate(o => o.Minutes > 0, "ProcessTimeout:Minutes deve essere maggiore di zero")
            .ValidateOnStart();

        services
            .AddOptions<List<CartOptions>>()
            .Bind(configuration.GetSection("Carts"))
            .Validate(carts => carts.Count > 0, "Carts: serve almeno un carrello")
            .Validate(carts => carts.All(c => !string.IsNullOrWhiteSpace(c.CartId)), "Carts: CartId mancante")
            .Validate(carts => carts.All(c => c.Baskets.Count > 0), "Carts: ogni carrello deve avere almeno una cesta")
            .Validate(HasUniqueBasketIds, "Carts: gli ID delle ceste devono essere univoci in tutti i carrelli")
            .ValidateOnStart();
    }

    // due ceste con lo stesso ID indirizzerebbero la stessa lampada fisica
    private static bool HasUniqueBasketIds(List<CartOptions> carts)
    {
        var basketIds = carts.SelectMany(c => c.Baskets).Select(b => b.BasketId).ToList();

        return basketIds.All(id => !string.IsNullOrWhiteSpace(id))
            && basketIds.Distinct().Count() == basketIds.Count;
    }

    private static void AddCartContainer(IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var cartConfigs = provider.GetRequiredService<IOptions<List<CartOptions>>>().Value;

            var carts = cartConfigs.Select(cartConfig =>
                new Cart(
                    cartConfig.CartId,
                    cartConfig.Baskets.Select(b => new Basket(basketId: b.BasketId, maxQuantity: 0))));

            return new CartContainer(carts);
        });
    }
}
