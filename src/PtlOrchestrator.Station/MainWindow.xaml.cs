using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PtlOrchestrator.Station.Presentation;

namespace PtlOrchestrator.Station;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;

    public MainWindow(MainViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();

        _services = services;
        DataContext = viewModel;

        // F11: schermo intero senza barra del titolo (e ritorno alla finestra normale)
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.F11)
                return;

            var fullScreen = WindowStyle == WindowStyle.None;
            WindowStyle = fullScreen ? WindowStyle.SingleBorderWindow : WindowStyle.None;
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            e.Handled = true;
        };

        // popup per l'operatore, ad esempio quando si supera il numero massimo di carrelli
        viewModel.PopupRequested += (_, message) =>
            MessageBox.Show(this, message, "Limite carrelli", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void OpenSettings()
    {
        var window = _services.GetRequiredService<SettingsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    public void OpenDiagnostics()
    {
        var window = _services.GetRequiredService<DiagnosticsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }
}
