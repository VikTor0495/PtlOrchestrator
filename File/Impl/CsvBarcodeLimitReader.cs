using PtlOrchestrator.Domain;
using PtlOrchestrator.Configuration;
using Microsoft.Extensions.Options;

namespace PtlOrchestrator.File.Impl;

public sealed class CsvBarcodeLimitReader : IFileReader<BarcodeLimit>
{

    private BarcodeLimitOptions _options;
    private readonly ILogger<CsvBarcodeLimitReader> _logger;
    private readonly object _lock = new();

    public CsvBarcodeLimitReader(
            IOptionsMonitor<BarcodeLimitOptions> options,
            ILogger<CsvBarcodeLimitReader> logger)
    {
        _logger = logger;

        // valore iniziale
        _options = options.CurrentValue;

        // hook reload a caldo
        options.OnChange(OnOptionsChanged);
    }

    public IEnumerable<BarcodeLimit> Read(string filePath)
    {
        CheckIfFileExists(filePath);

        return ReadCsvLines(filePath);
    }

    private IEnumerable<BarcodeLimit> ReadCsvLines(string filePath)
    {

        BarcodeLimitOptions snapshotOptions;

        // options coerenti per tutta la lettura
        lock (_lock)
        {
            snapshotOptions = _options;
        }


        foreach (var line in System.IO.File.ReadLines(filePath).Skip(1))
        {

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(',');

            var barcode = parts[snapshotOptions.BarcodeColumnIndex].Trim();

            if (!int.TryParse(parts[snapshotOptions.LimitColumnIndex], out var max))
            {
                _logger.LogWarning(
                    "Valore non valido per il limite del barcode {Barcode}: {Value}", barcode, parts[snapshotOptions.LimitColumnIndex]);
                continue;
            }

            yield return new BarcodeLimit(barcode, max);
        }
    }

    private static void CheckIfFileExists(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException(
                $"File limiti barcode non trovato: {filePath}");
    }


    // Gestione aggiornamento a caldo delle opzioni
    private void OnOptionsChanged(BarcodeLimitOptions newOptions)
    {
        lock (_lock)
        {
            _options = newOptions;

            _logger.LogInformation(
                "BarcodeLimitOptions aggiornate a caldo: File={FilePath}, BarcodeCol={BarcodeCol}, LimitCol={LimitCol}",
                newOptions.FileName,
                newOptions.BarcodeColumnIndex,
                newOptions.LimitColumnIndex);
        }
    }
}
