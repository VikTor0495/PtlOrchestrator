using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.File.Impl;
using PtlOrchestrator.Tests.Support;

namespace PtlOrchestrator.Tests.File;

public sealed class CsvBarcodeLimitReaderTests : IDisposable
{
    private readonly TempDirectory _tempDirectory = new();

    public void Dispose() => _tempDirectory.Dispose();

    [Fact]
    public void Read_SkipsHeaderAndParsesRows()
    {
        var path = CreateCsv("barcode,limit", "AAA,4", "BBB,9");

        var limits = CreateReader().Read(path).ToList();

        Assert.Equal([new BarcodeLimit("AAA", 4), new BarcodeLimit("BBB", 9)], limits);
    }

    [Fact]
    public void Read_TrimsBarcode()
    {
        var path = CreateCsv("barcode,limit", "  AAA  ,4");

        var limits = CreateReader().Read(path).ToList();

        Assert.Equal([new BarcodeLimit("AAA", 4)], limits);
    }

    [Fact]
    public void Read_IgnoresBlankLines()
    {
        var path = CreateCsv("barcode,limit", "AAA,4", "", "   ", "BBB,9");

        var limits = CreateReader().Read(path).ToList();

        Assert.Equal(2, limits.Count);
    }

    [Fact]
    public void Read_SkipsRowsWithNonNumericLimit()
    {
        var path = CreateCsv("barcode,limit", "AAA,abc", "BBB,9");

        var limits = CreateReader().Read(path).ToList();

        Assert.Equal([new BarcodeLimit("BBB", 9)], limits);
    }

    [Fact]
    public void Read_HonoursConfiguredColumnIndexes()
    {
        var path = CreateCsv("limit,descrizione,barcode", "4,prodotto,AAA");

        var reader = CreateReader(barcodeColumnIndex: 2, limitColumnIndex: 0);

        Assert.Equal([new BarcodeLimit("AAA", 4)], reader.Read(path).ToList());
    }

    [Fact]
    public void Read_WhenFileDoesNotExist_ThrowsImmediately()
    {
        var reader = CreateReader();

        Assert.Throws<FileNotFoundException>(
            () => reader.Read(_tempDirectory.PathOf("missing.csv")));
    }

    private string CreateCsv(params string[] lines)
        => _tempDirectory.CreateFile("limits.csv", string.Join(Environment.NewLine, lines));

    private static CsvBarcodeLimitReader CreateReader(int barcodeColumnIndex = 0, int limitColumnIndex = 1)
    {
        var options = Options.Create(new BarcodeLimitOptions
        {
            BasketId = "unused",
            FileName = "unused.csv",
            BarcodeColumnIndex = barcodeColumnIndex,
            LimitColumnIndex = limitColumnIndex
        });

        return new CsvBarcodeLimitReader(options, NullLogger<CsvBarcodeLimitReader>.Instance);
    }
}
