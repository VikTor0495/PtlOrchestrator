using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Enum;
using PtlOrchestrator.Service;
using PtlOrchestrator.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PtlOrchestrator.Manager.Impl;

public sealed class CartManager(
    CartContainer cartContainer,
    IPtlCommandService ptlCommandService,
    ILogger<CartManager> logger) : ICartManager
{

    private readonly CartContainer _cartContainer = cartContainer;
    private readonly IPtlCommandService _ptlCommandService = ptlCommandService;
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
                return result;

            }

            _logger.LogInformation(
                  "ACCETTATO barcode {Barcode} → Cart {CartId} Basket {BasketId} ({Type})",
                  barcode,
                  result.Cart!.CartId,
                  result.Basket!.BasketId,
                  result.Type);

            try
            {
                _ptlCommandService.SendAsync(
                    result.Basket.BasketId,
                    BuildActivation(result),
                    CancellationToken.None
                ).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Errore PTL su Cart {CartId}, Basket {BasketId} → rollback",
                    result.Cart.CartId,
                    result.Basket.BasketId);

                Rollback(result);

                throw; // oppure ritorni result fallito
            }

            return result;
        }
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

    private void Rollback(CartAssignmentResult assignment)
    {
        _cartContainer.Rollback(assignment);

    }

    private static PtlActivation BuildActivation(CartAssignmentResult result)
{
    var basket = result.Basket!;

    // Basket pieno → ROSSO fisso
    if (basket.IsFull)
    {
        return new PtlActivation
        {
            Color = PtlColor.Red,
            Blinking = false,
            DisplayText = basket.CurrentQuantity.ToString() + "/" + basket.MaxQuantity.ToString()
        };
    }

    // Inserimento normale → VERDE lampeggiante
    return new PtlActivation
    {
        Color = PtlColor.Green,
        Blinking = true,
        DisplayText = basket.CurrentQuantity.ToString() + "/" + basket.MaxQuantity.ToString()
    };
}
}
