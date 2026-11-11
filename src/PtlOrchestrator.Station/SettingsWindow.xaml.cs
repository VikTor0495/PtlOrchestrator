using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Configuration;
using PtlOrchestrator.Station.Storage;

namespace PtlOrchestrator.Station;

public partial class SettingsWindow : Window
{
    // campi numerici: un valore non intero non viene salvato
    private static readonly HashSet<string> IntegerKeys =
    [
        "Central:EventSyncIntervalSeconds",
        "Central:OperatorSyncIntervalSeconds",
        "Central:CartCatalogSyncIntervalSeconds",
        "ProcessTimeout:Minutes",
        "SerialScanner:BaudRate"
    ];

    private readonly IConfiguration _configuration;
    private readonly SettingsStore _store;
    private readonly Dictionary<string, TextBox> _fields = [];

    public SettingsWindow(IConfiguration configuration, SettingsStore store)
    {
        InitializeComponent();

        _configuration = configuration;
        _store = store;

        var row = 0;
        foreach (var key in SettingsStore.EditableKeys)
        {
            FieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = key, Margin = new Thickness(0, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            var field = new TextBox { Text = _configuration[key] ?? string.Empty, Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(4, 3, 4, 3) };

            Grid.SetRow(label, row);
            Grid.SetRow(field, row);
            Grid.SetColumn(field, 1);
            FieldsGrid.Children.Add(label);
            FieldsGrid.Children.Add(field);

            _fields[key] = field;
            row++;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var changes = new Dictionary<string, string>();

        foreach (var (key, field) in _fields)
        {
            var value = field.Text.Trim();

            if (IntegerKeys.Contains(key) && !int.TryParse(value, out _))
            {
                StatusText.Text = $"{key}: serve un numero intero";
                return;
            }

            // si salva solo ciò che differisce dalla configurazione attuale, così non si copiano valori di default nel database
            if (value != (_configuration[key] ?? string.Empty))
                changes[key] = value;
        }

        if (changes.Count == 0)
        {
            StatusText.Text = "Nessuna modifica da salvare";
            return;
        }

        try
        {
            await _store.SaveAsync(changes, DateTimeOffset.UtcNow, CancellationToken.None);
            StatusText.Text = $"Salvate {changes.Count} impostazioni. Riavvia la postazione per applicarle.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Salvataggio non riuscito: {ex.Message}";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
