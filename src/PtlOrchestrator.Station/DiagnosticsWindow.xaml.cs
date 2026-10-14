using System.Windows;
using PtlOrchestrator.Station.Presentation;

namespace PtlOrchestrator.Station;

public partial class DiagnosticsWindow : Window
{
    private readonly DiagnosticsViewModel _viewModel;

    public DiagnosticsWindow(DiagnosticsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
        => await _viewModel.RefreshAsync(CancellationToken.None);

    private async void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "Vuoi azzerare tutti i carrelli? Verrà scritto il report e le lampade tornano allo stato iniziale.",
            "Conferma azzeramento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
            return;

        ResetButton.IsEnabled = false;

        try
        {
            await _viewModel.ResetCartsAsync(CancellationToken.None);
            await _viewModel.RefreshAsync(CancellationToken.None);
        }
        finally
        {
            ResetButton.IsEnabled = true;
        }
    }
}
