using PtlOrchestrator.File;
using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Service.Impl;

public sealed class BasketLimitService(ILogger<BasketLimitService> logger,
    IFileReader<BarcodeLimit> reader) : IBasketLimitService
{
    private readonly ILogger<BasketLimitService> _logger = logger;
    private readonly IFileReader<BarcodeLimit> _reader = reader;
    private readonly Dictionary<string, int> _limits = [];

    public void Load(string csvPath, CancellationToken cancellationToken)
    {
        _limits.Clear();

        _logger.LogInformation("Caricamento limiti basket da file {CsvPath}", csvPath);

        CheckIfFileExists(csvPath, cancellationToken);

        foreach (var limit in _reader.Read(csvPath))
        {
            _limits[limit.Barcode] = limit.MaxQuantity;
        }
    }

    private void CheckIfFileExists(string filePath, CancellationToken cancellationToken)
    {
        while (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning(
                "File limiti barcode non trovato: {filePath}", filePath);
            
            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(5000);
        }     
    }

    public int GetMaxFor(string barcode)
    {
        _limits.TryGetValue(barcode, out var maxQuantity);
        return maxQuantity;
    }

    public bool HasMaxLimit(string barcode)
    {
        return _limits.ContainsKey(barcode);
    }
}