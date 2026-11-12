using System.Windows;
using System.Windows.Controls;

namespace PtlOrchestrator.Station.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void Keyboard_Click(object sender, RoutedEventArgs e) => OnScreenKeyboard.Open();
}
