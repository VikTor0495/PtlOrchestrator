using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Enum;
using PtlOrchestrator.Service;
using PtlOrchestrator.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Builder;
using PtlOrchestrator.Report;

namespace PtlOrchestrator.Manager.Impl;

public sealed class CartManager(
    CartContainer cartContainer,
    IPtlCommandService ptlCommandService,
    ICartReportWriter cartReportWriter,
    ILogger<CartManager> logger) : ICartManager
{

    private readonly CartContainer _cartContainer = cartContainer;

    private readonly IPtlCommandService _ptlCommandService = ptlCommandService;

    private readonly ICartReportWriter _cartReportWriter = cartReportWriter;


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
                SendAddCommandAndWaitConfirm(result);

                if (result.Basket.IsFull)
                {
                    SendFullBasketCommand(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Errore PTL su Cart {CartId}, Basket {BasketId} → rollback",
                    result.Cart.CartId,
                    result.Basket.BasketId);

                Rollback(result);

                throw;
            }


            return result;
        }
    }

    private void SendFullBasketCommand(CartAssignmentResult result)
    {
        if (result != null && result.Basket != null)
        {
            _ptlCommandService.SendAsync(
                result.Basket.BasketId,
                PtlActivationCommandBuilder.BuildRedFixedActivation(result.Basket),
                CancellationToken.None
            ).GetAwaiter().GetResult();
        }
    }

    private void SendAddCommandAndWaitConfirm(CartAssignmentResult result)
    {
        if (result != null && result.Basket != null)
        {
            _ptlCommandService.SendAsync(result.Basket.BasketId,
                PtlActivationCommandBuilder.BuildGreenActivation(result.Basket),
                CancellationToken.None)
            .GetAwaiter().GetResult();

            _ptlCommandService.WaitForButtonAsync(
                result.Basket.BasketId,
                CancellationToken.None
            ).GetAwaiter().GetResult();

            _logger.LogInformation(
                "Conferma ricevuta per Cart {CartId}, Basket {BasketId}",
                result.Cart!.CartId,
                result.Basket.BasketId);
        }
    }

    public void ResetAll()
    {
        foreach (var basket in _cartContainer.GetCarts().SelectMany(c => c.GetBaskets))
        {
            TryToResetBasket(basket.BasketId);
        }

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

    private void TryToResetBasket(string basketId)
    {
        try
        {
            _ptlCommandService.SendAsync(
                basketId,
                PtlActivationCommandBuilder.BuildOffActivation(),
                CancellationToken.None
            ).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError("Errore durante il reset del basket {BasketId}: {errorMessage}", basketId, ex.Message);
        }


    }

    public void WriteCsvReport()
    {
        _cartReportWriter.Write(_cartContainer.GetCarts());
    }
}