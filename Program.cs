using PtlOrchestrator;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;
using PtlOrchestrator.Input;
using PtlOrchestrator.Input.Impl;
using PtlOrchestrator.Manager;
using PtlOrchestrator.Manager.Impl;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Configuration.Formatter;
using PtlOrchestrator.Domain;
using PtlOrchestrator.File;
using PtlOrchestrator.File.Impl;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;


var builder = Host.CreateApplicationBuilder(args);

// Registra configurazioni tipizzate
builder.Configuration.AddJsonFile(
    "appsettings.json",
    optional: false,
    reloadOnChange: true); // reload a caldo

builder.Services
    .AddOptions<LightstepOptions>()
    .Bind(builder.Configuration.GetSection("Lightstep"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ControllerIp), "ControllerIp non valida")
    .Validate(o => o.ControllerPort > 0, "ControllerPort non valida")
    .ValidateOnStart();

builder.Services.Configure<List<CartOptions>>(
    builder.Configuration.GetSection("Carts"));

builder.Services.AddSingleton(sp =>
{
    var cartConfigs = sp
        .GetRequiredService<IOptions<List<CartOptions>>>()
        .Value;

    var carts = cartConfigs.Select(cartCfg =>
        new Cart(
            cartCfg.CartId,
            cartCfg.Baskets.Select(b =>
                new Basket(
                    basketId: b.BasketId,
                    maxQuantity: 0))
        )
    ).ToList();

    return new CartContainer(carts);
});

builder.Services
    .AddOptions<BarcodeLimitOptions>()
    .Bind(builder.Configuration.GetSection("LimitBarcode"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.FileName), "FileName non valido")
    .ValidateOnStart();

// Registra servizi applicativi
builder.Services.AddSingleton<ICartManager, CartManager>();
builder.Services.AddSingleton<IBarcodeInputService, ConsoleBarcodeInputService>();
builder.Services.AddSingleton<ILightstepConnectionService, LightstepConnectionService>();
builder.Services.AddSingleton<IPtlCommandService, LightstepPtlCommandService>();
builder.Services.AddSingleton<ICartReportWriter, CsvCartReportWriter>();
builder.Services.AddSingleton<CsvBarcodeLimitReader, CsvBarcodeLimitReader>();
builder.Services.AddSingleton<IBasketLimitService, BasketLimitService>();
builder.Services.AddSingleton<IFileReader<BarcodeLimit>, CsvBarcodeLimitReader>();
builder.Services.AddSingleton<IAppProcessingState, AppProcessingState>();

// Registra il Worker (BackgroundService principale)
builder.Services.AddHostedService<Worker>();

// Configura logging 
builder.Logging.ClearProviders();
builder.Logging.AddConsoleFormatter<CustomLogFormatter, ConsoleFormatterOptions>();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "custom";
});
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);

// Build e run
var host = builder.Build();
await host.RunAsync();

