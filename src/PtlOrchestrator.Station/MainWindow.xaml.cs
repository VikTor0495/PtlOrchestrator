using System.ComponentModel;
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

        // dopo un tentativo di accesso il ViewModel azzera il PIN: il campo deve seguire
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // PasswordBox non supporta il binding: il PIN passa dal ViewModel solo come testo temporaneo
    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.Pin = ((PasswordBox)sender).Password;

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<DiagnosticsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Pin) && _viewModel.Pin.Length == 0)
            PinBox.Clear();
    }
}
