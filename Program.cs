using PtlController;
using PtlController.Service;
using PtlController.Service.Impl;
using PtlController.Input;
using PtlController.Input.Impl;
using PtlController.Output;
using PtlController.Output.Impl;
using PtlController.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;



var builder = Host.CreateApplicationBuilder(args);

// Registra configurazione tipizzata (Options Pattern)
builder.Services
    .AddOptions<LightstepOptions>()
    .Bind(builder.Configuration.GetSection("Lightstep"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ControllerIp), "ControllerIp non valida")
    .Validate(o => o.ControllerPort > 0, "ControllerPort non valida")
    .ValidateOnStart();

builder.Services.Configure<CartsOptions>(
    builder.Configuration.GetSection("CartsOptions"));

// Registra servizi applicativi
builder.Services.AddSingleton<ICartManager, CartManager>();
builder.Services.AddSingleton<ITcpLightController, TcpLightController>();
builder.Services.AddSingleton<IBarcodeInputService, ConsoleBarcodeInputService>();

// Libs
builder.Services.AddSingleton<LightstepConnectionService>();

// Registra il Worker (BackgroundService principale)
builder.Services.AddHostedService<Worker>();

// Configura logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Build e run
var host = builder.Build();
await host.RunAsync();
