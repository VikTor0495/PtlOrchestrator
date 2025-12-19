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

// Configura Host con Dependency Injection
var builder = Host.CreateApplicationBuilder(args);

// Registra configurazione tipizzata (Options Pattern)
builder.Services.Configure<CartsConfiguration>(
    builder.Configuration.GetSection("CartsConfiguration"));

// Registra servizi applicativi
builder.Services.AddSingleton<ICartManager, CartManager>();
builder.Services.AddSingleton<ITcpLightController, TcpLightController>();
builder.Services.AddSingleton<IBarcodeInputService, ConsoleBarcodeInputService>();

// Registra il Worker (BackgroundService principale)
builder.Services.AddHostedService<Worker>();

// Configura logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Build e run
var host = builder.Build();
await host.RunAsync();
