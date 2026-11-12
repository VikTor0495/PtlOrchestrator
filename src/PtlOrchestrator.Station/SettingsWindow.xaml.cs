using System.Diagnostics;
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

    // nomi per i magazzinieri: cosa è, in parole semplici
    private static readonly Dictionary<string, (string Title, string Hint)> Descriptions = new()
    {
        ["Station:StationId"] = ("Nome della postazione", "Lascia vuoto: il nome viene scelto in automatico per questo computer"),
        ["Central:BaseUrl"] = ("Indirizzo del server", "Esempio: http://192.168.1.50:5000"),
        ["Central:ApiKey"] = ("Chiave di accesso al server", "Te la fornisce chi gestisce il server"),
        ["Central:EventSyncIntervalSeconds"] = ("Invio dei dati al server ogni (secondi)", "Ogni quanto la postazione manda al server il lavoro svolto"),
        ["Central:OperatorSyncIntervalSeconds"] = ("Aggiornamento operatori ogni (secondi)", "Ogni quanto scarica l'elenco aggiornato degli operatori"),
        ["Central:CartCatalogSyncIntervalSeconds"] = ("Aggiornamento carrelli ogni (secondi)", "Ogni quanto scarica l'elenco dei carrelli disponibili"),
        ["LimitBarcode:FileName"] = ("File dei prodotti", "Nome del file (messo accanto al programma) o percorso completo, con i prodotti e le quantità massime per cesta"),
        ["ProcessTimeout:Minutes"] = ("Tempo massimo per confermare (minuti)", "Dopo questo tempo senza conferma, l'inserimento viene annullato"),
        ["SerialScanner:PortName"] = ("Porta della pistola barcode", "Esempio: COM3"),
        ["SerialScanner:BaudRate"] = ("Velocità della pistola", "Di solito 9600: cambiala solo se te lo dice l'assistenza"),
        ["Lightstep:LicenzeKey"] = ("Codice di licenza delle luci", "Codice del controller delle luci, fornito dal produttore")
    };

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

            var (title, hint) = Descriptions.TryGetValue(key, out var d) ? d : (key, string.Empty);

            // nome comprensibile con la spiegazione sotto; il nome tecnico resta solo nel suggerimento al passaggio del mouse
            var label = new StackPanel { Margin = new Thickness(0, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center, ToolTip = key };
            label.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (hint.Length > 0)
                label.Children.Add(new TextBlock { Text = hint, FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray, TextWrapping = TextWrapping.Wrap });

            var field = new TextBox
            {
                Text = _configuration[key] ?? string.Empty,
                Margin = new Thickness(0, 6, 0, 6),
                Padding = new Thickness(6, 5, 6, 5),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };

            Grid.SetRow(label, row);
            Grid.SetRow(field, row);
            Grid.SetColumn(field, 1);
            FieldsGrid.Children.Add(label);
            FieldsGrid.Children.Add(field);

            _fields[key] = field;
            row++;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync();

    private async void SaveAndRestart_Click(object sender, RoutedEventArgs e)
    {
        if (await SaveAsync())
            Restart();
    }

    /// <summary>Salva i campi modificati. Vero solo se qualcosa è stato salvato.</summary>
    private async Task<bool> SaveAsync()
    {
        var changes = new Dictionary<string, string>();

        foreach (var (key, field) in _fields)
        {
            var value = field.Text.Trim();

            if (IntegerKeys.Contains(key) && !int.TryParse(value, out _))
            {
                StatusText.Text = $"{(Descriptions.TryGetValue(key, out var d) ? d.Title : key)}: serve un numero intero";
                return false;
            }

            // si salva solo ciò che differisce dalla configurazione attuale, così non si copiano valori di default nel database
            if (value != (_configuration[key] ?? string.Empty))
                changes[key] = value;
        }

        if (changes.Count == 0)
        {
            StatusText.Text = "Nessuna modifica da salvare";
            return false;
        }

        try
        {
            await _store.SaveAsync(changes, DateTimeOffset.UtcNow, CancellationToken.None);
            StatusText.Text = $"Salvate {changes.Count} impostazioni. Valgono dal riavvio della postazione.";
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Salvataggio non riuscito: {ex.Message}";
            return false;
        }
    }

    // riapre la postazione con le impostazioni appena salvate (le variabili d'ambiente passano al nuovo processo)
    private static void Restart()
    {
        var executable = Environment.ProcessPath;
        if (executable is null)
            return;

        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false });
        Application.Current.Shutdown();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
