using PtlOrchestrator.Domain;
using PtlOrchestrator.Core.Tests.Support;
using static PtlOrchestrator.Core.Tests.Support.CartManagerTestContext;

namespace PtlOrchestrator.Core.Tests.Manager;

public class CartManagerTests
{
    private const string FirstBasket = "0001";
    private const string SecondBasket = "0002";
    private const string ThirdBasket = "0003";

    private readonly CartManagerTestContext _context = new();

    [Fact]
    public async Task ProcessBarcode_WithUnknownBarcode_IsRejectedWithoutTouchingThePtl()
    {
        var result = await _context.Manager.ProcessBarcode("UNKNOWN", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CartAssignmentType.Rejected, result.Type);
        Assert.Contains("UNKNOWN", result.Reason);
        Assert.Empty(_context.Ptl.SentCommands);
    }

    [Fact]
    public async Task ProcessBarcode_WhenOperatorConfirmsTheRightModule_AssignsNewBasketAndSetsItsLimit()
    {
        _context.Ptl.EnqueueButtonPresses(FirstBasket);

        var result = await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CartAssignmentType.NewItem, result.Type);
        Assert.Equal(FirstBasket, result.Basket!.BasketId);
        Assert.Equal(2, result.Basket.MaxQuantity);
        Assert.Equal(1, result.Basket.CurrentQuantity);
    }

    [Fact]
    public async Task ProcessBarcode_OnCorrectConfirmation_DrivesTheLightsInTheExpectedOrder()
    {
        _context.Ptl.EnqueueButtonPresses(FirstBasket);

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.Equal(
            [
                _context.FlashGreenFor(FirstBasket),
                _context.EnableButtonsOf(SecondBasket, ThirdBasket),
                _context.DisableButtonsOfAllBaskets()
            ],
            _context.Ptl.SentCommands);
        Assert.Equal([FirstBasket], _context.Ptl.WaitedModules);
    }

    [Fact]
    public async Task ProcessBarcode_WithSameBarcodeTwice_ReusesTheSameBasket()
    {
        _context.Ptl.EnqueueButtonPresses(FirstBasket, FirstBasket);

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);
        var second = await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.True(second.Success);
        Assert.Equal(CartAssignmentType.ExistingItem, second.Type);
        Assert.Equal(FirstBasket, second.Basket!.BasketId);
        Assert.Equal(2, second.Basket.CurrentQuantity);
    }

    [Fact]
    public async Task ProcessBarcode_WhenBasketIsFull_OpensTheNextBasket()
    {
        var context = new CartManagerTestContext(barcodeLimit: 1);
        context.Ptl.EnqueueButtonPresses(FirstBasket, SecondBasket);

        await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);
        var second = await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.True(second.Success);
        Assert.Equal(SecondBasket, second.Basket!.BasketId);
    }

    [Fact]
    public async Task ProcessBarcode_WhenNoBasketIsLeft_IsRejectedWithoutTouchingThePtl()
    {
        var context = new CartManagerTestContext(basketCount: 1, barcodeLimit: 1);
        context.Ptl.EnqueueButtonPresses(FirstBasket);
        await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);
        var commandsBefore = context.Ptl.SentCommands.Count;

        var result = await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(commandsBefore, context.Ptl.SentCommands.Count);
    }

    [Fact]
    public async Task ProcessBarcode_OnWrongModule_LightsRedWithBuzzerThenRestoresItOnCorrectConfirmation()
    {
        _context.Ptl.EnqueueButtonPresses(SecondBasket, FirstBasket);

        var result = await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            [
                _context.FlashGreenFor(FirstBasket),
                _context.EnableButtonsOf(SecondBasket, ThirdBasket),
                _context.RedBuzzerFor(SecondBasket),
                _context.EnableButtonsOf(SecondBasket),
                _context.DisableButtonsOfAllBaskets()
            ],
            _context.Ptl.SentCommands);
    }

    [Fact]
    public async Task ProcessBarcode_WhenSameWrongModuleIsPressedTwice_SwitchesItsRedLightOff()
    {
        _context.Ptl.EnqueueButtonPresses(SecondBasket, SecondBasket, FirstBasket);

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.Equal(
            [
                _context.FlashGreenFor(FirstBasket),
                _context.EnableButtonsOf(SecondBasket, ThirdBasket),
                _context.RedBuzzerFor(SecondBasket),
                _context.EnableButtonsOf(SecondBasket),
                _context.DisableButtonsOfAllBaskets()
            ],
            _context.Ptl.SentCommands);
    }

    [Fact]
    public async Task ProcessBarcode_WhenADifferentWrongModuleIsPressed_ClearsThePreviousRedLightFirst()
    {
        _context.Ptl.EnqueueButtonPresses(SecondBasket, ThirdBasket, FirstBasket);

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.Equal(
            [
                _context.FlashGreenFor(FirstBasket),
                _context.EnableButtonsOf(SecondBasket, ThirdBasket),
                _context.RedBuzzerFor(SecondBasket),
                _context.EnableButtonsOf(SecondBasket),
                _context.RedBuzzerFor(ThirdBasket),
                _context.EnableButtonsOf(ThirdBasket),
                _context.DisableButtonsOfAllBaskets()
            ],
            _context.Ptl.SentCommands);
    }

    [Fact]
    public async Task ProcessBarcode_WhenTimeoutElapses_IsRejectedAndRollsBackTheAssignment()
    {
        var context = new CartManagerTestContext(timeoutMinutes: 0);

        var result = await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Timeout", result.Reason);
        Assert.All(context.Baskets, basket => Assert.True(basket.IsEmpty));
    }

    [Fact]
    public async Task ProcessBarcode_WhenTimeoutElapses_FinishesByDisablingAllModulesWithoutGreenPhase()
    {
        var context = new CartManagerTestContext(timeoutMinutes: 0);

        await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.Equal(context.DisableButtonsOfAllBaskets(), context.Ptl.SentCommands.Last());
        Assert.DoesNotContain(context.GreenOnAllBaskets(), context.Ptl.SentCommands);
    }

    [Fact]
    public async Task ProcessBarcode_WhenPtlFails_IsRejectedWithErrorAndRollsBackTheAssignment()
    {
        _context.Ptl.SendFailure = new InvalidOperationException("PTL ha risposto con NAK");

        var result = await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("PTL ha risposto con NAK", result.Reason);
        Assert.Contains(FirstBasket, result.Reason);
        Assert.All(_context.Baskets, basket => Assert.True(basket.IsEmpty));
    }

    [Fact]
    public async Task ProcessBarcode_WhenPtlFailsOnExistingItem_KeepsThePreviouslyConfirmedItems()
    {
        _context.Ptl.EnqueueButtonPresses(FirstBasket);
        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);
        _context.Ptl.SendFailure = new InvalidOperationException("errore");

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        var basket = _context.Baskets.First();
        Assert.Equal(1, basket.CurrentQuantity);
        Assert.Equal(Barcode, basket.Barcode);
    }

    [Fact]
    public async Task ResetAll_LightsEverythingGreenThenDisablesModulesAndEmptiesTheCarts()
    {
        _context.Ptl.EnqueueButtonPresses(FirstBasket);
        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);
        _context.Ptl.SentCommands.Clear();

        await _context.Manager.ResetAll(CancellationToken.None);

        Assert.Equal(
            [_context.GreenOnAllBaskets(), _context.DisableButtonsOfAllBaskets()],
            _context.Ptl.SentCommands);
        Assert.All(_context.Baskets, basket => Assert.True(basket.IsEmpty));
    }

    [Fact]
    public void WriteCsvReport_DelegatesToTheReportWriter()
    {
        _context.Manager.WriteCsvReport();

        Assert.Equal(1, _context.ReportWriter.WriteCalls);
    }

    [Fact]
    public void WriteCsvReport_WhenWriterFails_RethrowsTheException()
    {
        _context.ReportWriter.Failure = new IOException("disco pieno");

        var exception = Assert.Throws<IOException>(() => _context.Manager.WriteCsvReport());

        Assert.Equal("disco pieno", exception.Message);
    }

    [Fact]
    public void ShowStatus_DoesNotThrow()
    {
        var exception = Record.Exception(() => _context.Manager.ShowStatus());

        Assert.Null(exception);
    }
}
