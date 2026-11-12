using System.Windows;
using System.Windows.Controls;
using PtlOrchestrator.Station.Presentation;

namespace PtlOrchestrator.Station.Views;

public partial class WorkView : UserControl
{
    public WorkView()
    {
        InitializeComponent();
    }

    private async void CheckIn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CheckInDialog { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel viewModel)
            await viewModel.CheckInCartAsync(dialog.Code, CancellationToken.None);
    }

    private void Keyboard_Click(object sender, RoutedEventArgs e) => OnScreenKeyboard.Open();

    private void Settings_Click(object sender, RoutedEventArgs e)
        => (Window.GetWindow(this) as MainWindow)?.OpenSettings();

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
        => (Window.GetWindow(this) as MainWindow)?.OpenDiagnostics();
}
