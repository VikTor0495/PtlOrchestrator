using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        // la postazione sostituisce questa implementazione con il client HTTP del server carrelli
        services.TryAddSingleton<ICartAssignmentClient, NoCartAssignmentClient>();
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

        // il controller predefinito è facoltativo: i carrelli usano l'indirizzo censito sul server
        services
            .AddOptions<LightstepOptions>()
            .Bind(configuration.GetSection("Lightstep"));

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

    }

    private static void AddCartContainer(IServiceCollection services)
    {
        // il turno parte da zero: i carrelli si attivano con il check-in, che li censisce sul server
        services.AddSingleton(_ => new CartContainer([], activeCartIds: []));
    }
}
