using PtlOrchestrator.Domain;

namespace PtlOrchestrator.File.Impl;

public sealed class CsvCartReportWriter(
    ILogger<CsvCartReportWriter> logger
) : ICartReportWriter
{

    private readonly ILogger<CsvCartReportWriter> _logger = logger;

    public void Write(IEnumerable<Cart> carts)
    {
        if (!carts.Any() || !carts.Any(cart => cart.GetBaskets.Any(basket => !basket.IsEmpty)))
        {
            _logger.LogInformation("CSV non generato perchè non ci sono prodotti nel carrello.");
            return;
        }

        TryToWhriteCsvReport(carts);
    }

    private void TryToWhriteCsvReport(IEnumerable<Cart> carts)
    {
        try
        {
            using var writer = CreateFileInBaseDir();
            WriteCsvReport(writer, carts);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Errore durante la scrittura del report CSV");
        }
    }

    private void WriteCsvReport(StreamWriter writer, IEnumerable<Cart> carts)
    {

        writer.WriteLine("Carrello;Cesta;Barcode;Quantita'");

        foreach (var cart in carts)
        {
            foreach (var basket in cart.GetBaskets)
            {
                if (basket.IsEmpty)
                    continue;
                    
                writer.WriteLine(
                    $"{cart.CartId};" +
                    $"{basket.BasketId};" +
                    $"{basket.Barcode};" +
                    $"{basket.CurrentQuantity}");
            }
        }

        _logger.LogInformation("Report CSV generato correttamente.");

    }
    private StreamWriter CreateFileInBaseDir()
    {
        var baseDir = AppContext.BaseDirectory;
        var reportDir = Path.Combine(baseDir, "report");

        Directory.CreateDirectory(reportDir);

        var fileName = $"{DateTime.Now:yyyyMMdd-HHmm}.csv";
        var filePath = Path.Combine(reportDir, fileName);

        _logger.LogInformation("Generazione report CSV in {filePath}", filePath);


        return new StreamWriter(new FileStream(filePath, FileMode.Create), System.Text.Encoding.UTF8);
    }
}
