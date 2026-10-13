using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Core.Tests.Support;
using static PtlOrchestrator.Core.Tests.Support.CartManagerTestContext;

namespace PtlOrchestrator.Core.Tests.Manager;

public class CartManagerOperationEventsTests
{
    private const string FirstBasket = "0001";
    private const string SecondBasket = "0002";

    private readonly CartManagerTestContext _context = new();

    [Fact]
    public async Task ProcessBarcode_OnCorrectConfirmation_RecordsAcceptedThenConfirmed()
    {
        _context.Ptl.EnqueueButtonPresses(FirstBasket);

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.Equal(
            [OperationEventType.BarcodeAccepted, OperationEventType.BasketConfirmed],
            _context.Recorder.Types);
    }

    [Fact]
    public async Task ProcessBarcode_RecordsBarcodeCartAndBasketOnTheAcceptedEvent()
    {
        _context.Ptl.EnqueueButtonPresses(FirstBasket);

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        var accepted = _context.Recorder.Recorded.First();
        Assert.Equal(Barcode, accepted.Barcode);
        Assert.Equal("1", accepted.CartId);
        Assert.Equal(FirstBasket, accepted.BasketId);
        Assert.Equal("NewItem", accepted.Details);
    }

    [Fact]
    public async Task ProcessBarcode_ForSecondItemOfSameBarcode_RecordsExistingItem()
    {
        _context.Ptl.EnqueueButtonPresses(FirstBasket, FirstBasket);

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);
        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        var acceptedEvents = _context.Recorder.Recorded.Where(r => r.Type == OperationEventType.BarcodeAccepted);
        Assert.Equal(["NewItem", "ExistingItem"], acceptedEvents.Select(r => r.Details));
    }

    [Fact]
    public async Task ProcessBarcode_WithUnknownBarcode_RecordsRejectionWithReason()
    {
        await _context.Manager.ProcessBarcode("UNKNOWN", CancellationToken.None);

        var rejected = Assert.Single(_context.Recorder.Recorded);
        Assert.Equal(OperationEventType.BarcodeRejected, rejected.Type);
        Assert.Equal("UNKNOWN", rejected.Barcode);
        Assert.Contains("non presente", rejected.Details);
    }

    [Fact]
    public async Task ProcessBarcode_WhenNoBasketIsLeft_RecordsRejection()
    {
        var context = new CartManagerTestContext(basketCount: 1, barcodeLimit: 1);
        context.Ptl.EnqueueButtonPresses(FirstBasket);
        await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);
        context.Recorder.Recorded.Clear();

        await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        var rejected = Assert.Single(context.Recorder.Recorded);
        Assert.Equal(OperationEventType.BarcodeRejected, rejected.Type);
    }

    [Fact]
    public async Task ProcessBarcode_OnWrongModule_RecordsEachWrongPressWithTheExpectedBasket()
    {
        _context.Ptl.EnqueueButtonPresses(SecondBasket, SecondBasket, FirstBasket);

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        var wrongPresses = _context.Recorder.Recorded
            .Where(r => r.Type == OperationEventType.WrongModulePressed)
            .ToList();
        Assert.Equal(2, wrongPresses.Count);
        Assert.All(wrongPresses, press => Assert.Equal(FirstBasket, press.BasketId));
        Assert.All(wrongPresses, press => Assert.Contains(SecondBasket, press.Details));
    }

    [Fact]
    public async Task ProcessBarcode_WhenTimeoutElapses_RecordsTimeout()
    {
        var context = new CartManagerTestContext(timeoutMinutes: 0);

        await context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        Assert.Equal(
            [OperationEventType.BarcodeAccepted, OperationEventType.OperationTimedOut],
            context.Recorder.Types);
        Assert.DoesNotContain(OperationEventType.BasketConfirmed, context.Recorder.Types);
    }

    [Fact]
    public async Task ProcessBarcode_WhenPtlFails_RecordsFaultWithTheErrorMessage()
    {
        _context.Ptl.SendFailure = new InvalidOperationException("PTL ha risposto con NAK");

        await _context.Manager.ProcessBarcode(Barcode, CancellationToken.None);

        var fault = _context.Recorder.Recorded.Last();
        Assert.Equal(OperationEventType.PtlFault, fault.Type);
        Assert.Equal("PTL ha risposto con NAK", fault.Details);
        Assert.Equal(FirstBasket, fault.BasketId);
    }

    [Fact]
    public async Task ResetAll_RecordsCartsReset()
    {
        await _context.Manager.ResetAll(CancellationToken.None);

        Assert.Equal([OperationEventType.CartsReset], _context.Recorder.Types);
    }
}
