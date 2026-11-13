using System.Windows;

namespace PtlOrchestrator.Station.Views;

public partial class NoticeWindow : Window
{
    public NoticeWindow(string title, string message)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
