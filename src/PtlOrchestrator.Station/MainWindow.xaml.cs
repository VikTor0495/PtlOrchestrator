using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PtlOrchestrator.Station.Presentation;

namespace PtlOrchestrator.Station;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IServiceProvider _services;

    public MainWindow(MainViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _services = services;
        DataContext = viewModel;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<SettingsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<DiagnosticsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }
}
