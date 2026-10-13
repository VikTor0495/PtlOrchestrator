using Microsoft.Extensions.Logging.Abstractions;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Service.Impl;
using PtlOrchestrator.Tests.Support;

namespace PtlOrchestrator.Tests.Service;

public sealed class BasketLimitServiceTests : IDisposable
{
    private readonly TempDirectory _tempDirectory = new();
    private readonly FakeFileReader<BarcodeLimit> _reader = new();
    private readonly BasketLimitService _service;

    public BasketLimitServiceTests()
    {
        _service = new BasketLimitService(NullLogger<BasketLimitService>.Instance, _reader);
    }

    public void Dispose() => _tempDirectory.Dispose();

    [Fact]
    public void Load_PopulatesLimitsFromReader()
    {
        _reader.SetItems(new BarcodeLimit("AAA", 4), new BarcodeLimit("BBB", 9));

        _service.Load(CreateExistingCsv(), CancellationToken.None);

        Assert.True(_service.HasMaxLimit("AAA"));
        Assert.Equal(4, _service.GetMaxFor("AAA"));
        Assert.Equal(9, _service.GetMaxFor("BBB"));
    }

    [Fact]
    public void HasMaxLimit_ForUnknownBarcode_IsFalse()
    {
        _reader.SetItems(new BarcodeLimit("AAA", 4));
        _service.Load(CreateExistingCsv(), CancellationToken.None);

        Assert.False(_service.HasMaxLimit("ZZZ"));
    }

    [Fact]
    public void GetMaxFor_ForUnknownBarcode_ReturnsZero()
    {
        _service.Load(CreateExistingCsv(), CancellationToken.None);

        Assert.Equal(0, _service.GetMaxFor("ZZZ"));
    }

    [Fact]
    public void Load_WithDuplicatedBarcode_KeepsTheLastValue()
    {
        _reader.SetItems(new BarcodeLimit("AAA", 4), new BarcodeLimit("AAA", 7));

        _service.Load(CreateExistingCsv(), CancellationToken.None);

        Assert.Equal(7, _service.GetMaxFor("AAA"));
    }

    [Fact]
    public void Load_CalledAgain_DiscardsPreviousLimits()
    {
        var csvPath = CreateExistingCsv();
        _reader.SetItems(new BarcodeLimit("AAA", 4));
        _service.Load(csvPath, CancellationToken.None);

        _reader.SetItems(new BarcodeLimit("BBB", 2));
        _service.Load(csvPath, CancellationToken.None);

        Assert.False(_service.HasMaxLimit("AAA"));
        Assert.True(_service.HasMaxLimit("BBB"));
    }

    [Fact]
    public void Load_WhenFileIsMissingAndCancellationRequested_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => _service.Load(_tempDirectory.PathOf("missing.csv"), cts.Token));

        Assert.Equal(0, _reader.ReadCalls);
    }

    private string CreateExistingCsv()
        => _tempDirectory.CreateFile("limits.csv", "barcode,limit");
}
