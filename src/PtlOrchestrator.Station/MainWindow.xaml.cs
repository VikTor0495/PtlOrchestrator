using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PtlOrchestrator.Station.Presentation;

namespace PtlOrchestrator.Station;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private Views.NoticeWindow? _notice;
    private bool _noticeClosesWhenProductIsPlaced;

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
        viewModel.PopupRequested += (_, popup) =>
        {
            // un solo avviso alla volta: se l'operatore scansiona ancora, quello aperto resta e non si duplica
            if (_notice is not null)
                return;

            _noticeClosesWhenProductIsPlaced = popup.ClosesWhenProductIsPlaced;
            _notice = new Views.NoticeWindow(popup.Title, popup.Message) { Owner = this };
            _notice.Closed += (_, _) => _notice = null;
            _notice.Show();
        };

        // a prodotto posizionato l'avviso "posiziona prima il prodotto" non serve più
        viewModel.PopupDismissRequested += (_, _) =>
        {
            if (_notice is not null && _noticeClosesWhenProductIsPlaced)
                _notice.Close();
        };
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
