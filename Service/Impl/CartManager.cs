using PtlController.Configuration;
using PtlController.Output;
using PtlController.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PtlController.Service.Impl;

public sealed class CartManager(
    ILogger<CartManager> logger,
    CartContainer cartContainer,
    ITcpLightController ptl) : ICartManager
{
    private readonly ILogger<CartManager> _logger = logger;
    private readonly CartContainer _cartContainer = cartContainer;
    private readonly ITcpLightController _ptl = ptl;

    public async Task ProcessBarcodeAsync(string barcode, CancellationToken ct)
    {
        var result = _cartContainer.AssignItem(barcode);

        if (!result.Success)
        {
            _logger.LogWarning(
                "RIFIUTATO barcode {Barcode}: {Reason}",
                barcode,
                result.Reason);

            // futuro: buzzer / luce errore
            return;
        }

        _logger.LogInformation(
            "ACCETTATO barcode {Barcode} → Cart {CartId}, Basket {BasketId} ({Type})",
            barcode,
            result.Cart!.CartId,
            result.Basket!.BasketId,
            result.Type);

        //await _ptl.SwitchOnAsync( da rivedere 
        //    result.Basket.ModuleAddress,
        //    ct);
    }
    
    public void ResetAll()
    {
        _cartContainer.ResetAll();
        _logger.LogWarning("Reset completo di tutti i carrelli");
    }

}
