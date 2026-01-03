using PtlOrchestrator;
using PtlOrchestrator.Service;
using PtlOrchestrator.Service.Impl;
using PtlOrchestrator.Input;
using PtlOrchestrator.Input.Impl;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Configuration.Formatter;
using PtlOrchestrator.Domain;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;


var builder = Host.CreateApplicationBuilder(args);

// Registra configurazione tipizzata (Options Pattern)
builder.Services
    .AddOptions<LightstepOptions>()
    .Bind(builder.Configuration.GetSection("Lightstep"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ControllerIp), "ControllerIp non valida")
    .Validate(o => o.ControllerPort > 0, "ControllerPort non valida")
    .ValidateOnStart();

builder.Services.Configure<List<CartOptions>>(
    builder.Configuration.GetSection("Carts"));

builder.Services.AddSingleton<CartContainer>(sp =>
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
                    maxQuantity: b.MaxQuantity))
        )
    ).ToList();

    return new CartContainer(carts);
});

// Registra servizi applicativi
builder.Services.AddSingleton<ICartManager, CartManager>();
builder.Services.AddSingleton<IBarcodeInputService, ConsoleBarcodeInputService>();
builder.Services.AddSingleton<ILightstepConnectionService, LightstepConnectionService>();

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

