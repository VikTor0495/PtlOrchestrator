using PtlController.Configuration;
using PtlController.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PtlController.Service.Impl;

public sealed class CartManager(
    CartContainer cartContainer,
    ILogger<CartManager> logger) : ICartManager
{

    private readonly CartContainer _cartContainer = cartContainer;
    private readonly ILogger<CartManager> _logger = logger;

    private readonly object _lock = new();


    public CartAssignmentResult ProcessBarcode(string barcode)
    {
        CartAssignmentResult result;

        lock (_lock)
        {
            result = _cartContainer.AssignItem(barcode);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "RIFIUTATO barcode {Barcode}: {Reason}",
                    barcode,
                    result.Reason);
            }
            else
            {
                _logger.LogInformation(
                    "ACCETTATO barcode {Barcode} → Cart {CartId} Basket {BasketId} ({Type})",
                    barcode,
                    result.Cart!.CartId,
                    result.Basket!.BasketId,
                    result.Type);
            }
        }

        return result;
    }


    public void ResetAll()
    {
        _cartContainer.ResetAll();
        _logger.LogInformation("Reset completo di tutti i carrelli");
    }

    public void ShowStatus()
    {
        _cartContainer.ShowStatus();
    }
}
