using System.Windows;

namespace PtlOrchestrator.Station.Views;

public partial class CheckInDialog : Window
{
    public CheckInDialog()
    {
        InitializeComponent();
    }

    /// <summary>Numero del carrello inserito dall'operatore.</summary>
    public string Code => CodeBox.Text.Trim();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (Code.Length == 0)
            return;

        DialogResult = true;
    }

    private void Keyboard_Click(object sender, RoutedEventArgs e) => OnScreenKeyboard.Open();
}
