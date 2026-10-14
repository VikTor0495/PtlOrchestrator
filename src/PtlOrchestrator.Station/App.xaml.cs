using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PtlOrchestrator.Station.Presentation;
using Serilog;

namespace PtlOrchestrator.Station;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Configuration.SetBasePath(AppContext.BaseDirectory);

            builder.Services.AddSerilog((_, logger) => logger
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Fatal)
                .WriteTo.File(
                    Path.Combine(AppContext.BaseDirectory, "logs", "station-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30));

            builder.Services.AddStation(builder.Configuration);

            _host = builder.Build();
            await _host.StartAsync();

            _host.Services.GetRequiredService<MainViewModel>().StartMonitoring();

            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            // configurazione non valida o avvio fallito: meglio dirlo che restare un processo invisibile
            MessageBox.Show(ex.Message, "PTL Station - avvio non riuscito", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetService<MainViewModel>()?.Dispose();
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
