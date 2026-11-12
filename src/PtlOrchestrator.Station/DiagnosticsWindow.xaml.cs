using System.Windows;
using System.Windows.Threading;
using PtlOrchestrator.Station.Presentation;

namespace PtlOrchestrator.Station;

public partial class DiagnosticsWindow : Window
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(4);

    private readonly DiagnosticsViewModel _viewModel;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshInterval };

    public DiagnosticsWindow(DiagnosticsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        // lo stato si aggiorna da solo mentre la finestra è aperta
        _timer.Tick += async (_, _) => await _viewModel.RefreshAsync(CancellationToken.None);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync(CancellationToken.None);
        _timer.Start();
    }

    private void Window_Closed(object? sender, EventArgs e) => _timer.Stop();
}
