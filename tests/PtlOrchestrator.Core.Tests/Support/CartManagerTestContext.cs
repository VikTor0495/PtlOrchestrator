using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Builder;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager.Impl;

namespace PtlOrchestrator.Core.Tests.Support;

/// <summary>
/// Assembla un CartManager con doppi di test e un singolo carrello con i moduli 0001..000N,
/// esponendo i collaboratori per configurare lo scenario e verificare i comandi inviati.
/// </summary>
internal sealed class CartManagerTestContext
{
    public const string Barcode = "8001234567890";

    public CartManagerTestContext(
        int basketCount = 3,
        int barcodeLimit = 2,
        int timeoutMinutes = 5)
    {
        BasketIds = [.. Enumerable.Range(1, basketCount).Select(n => n.ToString("D4"))];
        Container = CartFactory.CreateSingleCartContainer(BasketIds);
        Ptl = new FakePtlCommandService();
        ReportWriter = new FakeCartReportWriter();

        var limits = new Dictionary<string, int> { [Barcode] = barcodeLimit };

        Manager = new CartManager(
            Container,
            Ptl,
            ReportWriter,
            new StubBasketLimitService(limits),
            Options.Create(new TimeoutOptions { Minutes = timeoutMinutes }),
            NullLogger<CartManager>.Instance);
    }

    public string[] BasketIds { get; }

    public CartContainer Container { get; }

    public FakePtlCommandService Ptl { get; }

    public FakeCartReportWriter ReportWriter { get; }

    public CartManager Manager { get; }

    public IReadOnlyCollection<Basket> Baskets => Container.GetCarts().Single().GetBaskets;

    public string[] AllBasketsExcept(string basketId)
        => [.. BasketIds.Where(id => id != basketId)];

    public string FlashGreenFor(string basketId)
        => Pp505Builder.BuildFlashGreenCommandThenFix(basketId);

    public string EnableButtonsOf(params string[] basketIds)
        => Pp505Builder.BuildArmedNoLightEnabledButton(basketIds);

    public string DisableButtonsOfAllBaskets()
        => Pp505Builder.BuildArmedNoLightButtonDisabled(BasketIds);

    public string RedBuzzerFor(string basketId)
        => Pp505Builder.BuildRedBuzzer(basketId);

    public string GreenOnAllBaskets()
        => Pp505Builder.BuildGreenCommand(BasketIds);
}
