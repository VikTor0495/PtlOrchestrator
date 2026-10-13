using Microsoft.Extensions.Logging.Abstractions;
using PtlOrchestrator.Domain;
using PtlOrchestrator.File.Impl;
using PtlOrchestrator.Core.Tests.Support;

namespace PtlOrchestrator.Core.Tests.File;

/// <summary>
/// Il writer scrive sempre in {AppContext.BaseDirectory}/report con un nome basato sull'ora:
/// i test individuano i file creati confrontando la cartella prima e dopo la scrittura
/// e li eliminano al termine.
/// </summary>
public sealed class CsvCartReportWriterTests : IDisposable
{
    private static readonly string ReportDirectory = Path.Combine(AppContext.BaseDirectory, "report");

    private readonly CsvCartReportWriter _writer = new(NullLogger<CsvCartReportWriter>.Instance);
    private readonly DateTime _startedAtUtc = DateTime.UtcNow.AddSeconds(-1);

    public void Dispose()
    {
        foreach (var file in GetFilesWrittenByThisTest())
            System.IO.File.Delete(file);
    }

    [Fact]
    public void Write_WithoutCarts_DoesNotCreateReport()
    {
        _writer.Write([]);

        Assert.Empty(GetFilesWrittenByThisTest());
    }

    [Fact]
    public void Write_WhenAllBasketsAreEmpty_DoesNotCreateReport()
    {
        var carts = new[] { CartFactory.CreateCart("1", "0001", "0002") };

        _writer.Write(carts);

        Assert.Empty(GetFilesWrittenByThisTest());
    }

    [Fact]
    public void Write_CreatesSemicolonSeparatedReportWithHeader()
    {
        var cart = CartFactory.CreateCart("1", "0001", "0002");
        cart.TryAddItem("AAA", maxQuantity: 5);
        cart.TryAddItem("AAA", maxQuantity: 5);

        _writer.Write([cart]);

        var lines = ReadSingleReport();
        Assert.Equal("Carrello;Cesta;Barcode;Quantita'", lines[0]);
        Assert.Equal("1;0001;AAA;2", lines[1]);
    }

    [Fact]
    public void Write_ListsOnlyOccupiedBasketsOfEveryCart()
    {
        var cartOne = CartFactory.CreateCart("1", "0001", "0002", "0003");
        cartOne.TryAddItem("AAA", maxQuantity: 5);
        cartOne.TryAddItem("BBB", maxQuantity: 5);
        var cartTwo = CartFactory.CreateCart("2", "0004");
        cartTwo.TryAddItem("CCC", maxQuantity: 5);

        _writer.Write([cartOne, cartTwo]);

        var lines = ReadSingleReport();
        Assert.Equal(
            ["1;0001;AAA;1", "1;0002;BBB;1", "2;0004;CCC;1"],
            lines.Skip(1));
    }

    private string[] ReadSingleReport()
    {
        var file = Assert.Single(GetFilesWrittenByThisTest());
        return System.IO.File.ReadAllLines(file);
    }

    private IEnumerable<string> GetFilesWrittenByThisTest()
    {
        if (!Directory.Exists(ReportDirectory))
            return [];

        return Directory
            .GetFiles(ReportDirectory, "*.csv")
            .Where(file => System.IO.File.GetLastWriteTimeUtc(file) >= _startedAtUtc);
    }
}
