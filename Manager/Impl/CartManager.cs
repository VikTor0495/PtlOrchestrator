using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Enum;
using PtlOrchestrator.Service;
using PtlOrchestrator.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Builder;
using PtlOrchestrator.File;
using System.Threading;

namespace PtlOrchestrator.Manager.Impl;

public sealed class CartManager(
    CartContainer cartContainer,
    IPtlCommandService ptlCommandService,
    ICartReportWriter cartReportWriter,
    IBasketLimitService basketLimitService,
    ILogger<CartManager> logger) : ICartManager
{

    private readonly CartContainer _cartContainer = cartContainer;

    private readonly IPtlCommandService _ptlCommandService = ptlCommandService;

    private readonly ICartReportWriter _cartReportWriter = cartReportWriter;

    private readonly IBasketLimitService _basketLimitService = basketLimitService;


    private readonly ILogger<CartManager> _logger = logger;

    private readonly object _lock = new();


    public CartAssignmentResult ProcessBarcode(string barcode, CancellationToken cancellationToken)
    {

        CartAssignmentResult result;

        lock (_lock)
        {
            if (!_basketLimitService.HasMaxLimit(barcode))
            {
                return CartAssignmentResult.Rejected(
                    $"Barcode {barcode} non presente nel file di configurazione dei limiti di carico");
            }

            result = _cartContainer.AssignItem(barcode, _basketLimitService.GetMaxFor(barcode));

            if (!result.Success)
            {
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
                HandlePickingProcessFlow(result, cancellationToken);
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

    private void HandlePickingProcessFlow(CartAssignmentResult result, CancellationToken cancellationToken)
    {
        if (result != null && result.Basket != null)
        {
            HandlePickLightFlow(result.Basket.BasketId, cancellationToken);

            HandleOperatorConfirmationFlow(result.Basket.BasketId, cancellationToken);

            _logger.LogInformation(
                "Conferma ricevuta per Cart {CartId}, Basket {BasketId}",
                result.Cart!.CartId,
                result.Basket.BasketId);

            Thread.Sleep(1000);

            ResetAllBaskets();
        }
    }

    private void HandlePickLightFlow(string basketId, CancellationToken cancellationToken)
    {
        _ptlCommandService.SendAsync(basketId,
               PtlActivationCommandBuilder.BuildPickFlowCommand(), cancellationToken)
           .GetAwaiter().GetResult();

        ArmsOtherBaskets(basketId, cancellationToken);
    }

    private void HandleOperatorConfirmationFlow(string basketId, CancellationToken cancellationToken)
    {
        string confirmedModule;

        do
        {
            confirmedModule = _ptlCommandService.WaitForButtonAsync(
                basketId,
                cancellationToken
            ).GetAwaiter().GetResult();

            if (confirmedModule != basketId)
            {
                _ptlCommandService.SendAsync(confirmedModule,
                    PtlActivationCommandBuilder.BuildErrorCommand(), cancellationToken)
                .GetAwaiter().GetResult();

                _logger.LogWarning(
                "Conferma ricevuta da modulo inatteso {ConfirmedModule}, in attesa di {ExpectedModule}. \n Riposizionare nel modulo corretto!",
                confirmedModule,
                basketId);

                _ptlCommandService.WaitForButtonAsync(
                                basketId,
                                cancellationToken
                            ).GetAwaiter().GetResult();
                
                ArmsOtherBaskets(basketId, cancellationToken);
            }

        } while (confirmedModule != basketId);
    }

    private void ArmsOtherBaskets(string excludingBasketId, CancellationToken cancellationToken)
    {
        foreach (var basket in _cartContainer.GetCarts()
            .SelectMany(c => c.GetBaskets)
            .Where(b => b.BasketId != excludingBasketId))
        {
            _ptlCommandService.SendRawAsync(basket.BasketId,
                 Pp505Builder.BuildArmedNoLight(basket.BasketId), cancellationToken
            ).GetAwaiter().GetResult();
        }
    }

    public void ResetAll()
    {
        ResetAllBaskets();

        _cartContainer.ResetAll();

        _logger.LogInformation("Reset completo di tutti i carrelli");
    }

    private void ResetAllBaskets()
    {
        foreach (var basket in _cartContainer.GetCarts().SelectMany(c => c.GetBaskets))
        {
            TryToResetBasket(basket.BasketId);
        }
    }

    public void ShowStatus()
    {
        _logger.LogInformation("{CartStatus}", _cartContainer.GetStatusString());
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
                PtlActivationCommandBuilder.BuildOffCommand(),
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