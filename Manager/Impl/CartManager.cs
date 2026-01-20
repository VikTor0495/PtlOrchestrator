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
using System.Configuration;

namespace PtlOrchestrator.Manager.Impl;

public sealed class CartManager(
    CartContainer cartContainer,
    IPtlCommandService ptlCommandService,
    ICartReportWriter cartReportWriter,
    IBasketLimitService basketLimitService,
    IOptions<TimeoutOptions> timeoutOptions,
    ILogger<CartManager> logger) : ICartManager
{

    private readonly CartContainer _cartContainer = cartContainer;

    private readonly IPtlCommandService _ptlCommandService = ptlCommandService;

    private readonly ICartReportWriter _cartReportWriter = cartReportWriter;

    private readonly IBasketLimitService _basketLimitService = basketLimitService;

    private readonly TimeoutOptions _timeoutOptions = timeoutOptions.Value;

    private readonly ILogger<CartManager> _logger = logger;



    public CartAssignmentResult ProcessBarcode(string barcode, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in ProcessBarcode: {Barcode}", barcode);

        CartAssignmentResult result;

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

            using var timeoutCts = SetupTimeoutCancellationTokenSource(cancellationToken);

            HandlePickingProcessFlow(result, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            ResetAllBaskets(false, cancellationToken);
            Rollback(result);
            return CartAssignmentResult.Rejected($"Timeout raggiunto durante il processo di inserimento Carrello {result.Cart!.CartId}, Basket {result.Basket!.BasketId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Errore PTL su Cart {CartId}, Basket {BasketId} → rollback",
                result.Cart.CartId,
                result.Basket.BasketId);
            Rollback(result);

            return CartAssignmentResult.Rejected($"Errore nel processo di inserimento Carrello {result.Cart!.CartId}, Basket {result.Basket!.BasketId}: {ex.Message}");
        }

        return result;

    }

    private CancellationTokenSource SetupTimeoutCancellationTokenSource(CancellationToken parentToken)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(_timeoutOptions.Minutes));
        return timeoutCts;
    }

    private void HandlePickingProcessFlow(CartAssignmentResult result, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fine HandlePickingProcessFlow per Basket {BasketId}", result.Basket?.BasketId);

        if (result != null && result.Basket != null)
        {
            HandlePickLightFlow(result.Basket.BasketId, cancellationToken);
            HandleOperatorConfirmationFlow(result.Basket.BasketId, cancellationToken);

            _logger.LogInformation(
                "Conferma ricevuta per Cart {CartId}, Basket {BasketId}",
                result.Cart!.CartId,
                result.Basket.BasketId);

            Thread.Sleep(1000);
            ResetAllBaskets(false, cancellationToken);
        }
        else
        {
            _logger.LogWarning("HandlePickingProcessFlow chiamato con result o basket null");
        }
    }

    private void HandlePickLightFlow(string basketId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in HandlePickLightFlow per Basket {BasketId}", basketId);

        try
        {
            _ptlCommandService.SendRawAsync(basketId,
                   Pp505Builder.BuildFlashGreenCommandThenOff(basketId), cancellationToken)
               .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nell'invio comando pick flow per Basket {BasketId}", basketId);
            throw;
        }

        ArmsOtherBaskets([basketId], cancellationToken);
    }

    private void HandleOperatorConfirmationFlow(string basketId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in HandleOperatorConfirmationFlow per Basket {BasketId}", basketId);

        do
        {
            var confirmedModule = _ptlCommandService.WaitForButtonAsync(
                basketId,
                cancellationToken
            ).GetAwaiter().GetResult();

            if (confirmedModule.Equals(basketId))
            {
                _logger.LogInformation(
                    "Conferma corretta ricevuta da modulo {ConfirmedModule}",
                    confirmedModule);
                break;
            }
            else
            {
                _logger.LogWarning(
                   "Conferma ricevuta da modulo inatteso {ConfirmedModule}, in attesa di {ExpectedModule}. Invio comando errore",
                   confirmedModule,
                   basketId);

                try
                {
                    _ptlCommandService.SendRawAsync(confirmedModule,
                        Pp505Builder.BuildFlashRedCommandThenOff(confirmedModule), cancellationToken)
                    .GetAwaiter().GetResult();

                    TurnOffOtherBaskets([basketId, confirmedModule], cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore nell'invio comando errore a modulo {ConfirmedModule}", confirmedModule);
                    throw;
                }

                var second_confirm = _ptlCommandService.WaitForButtonAsync(
                    confirmedModule,
                    cancellationToken
                ).GetAwaiter().GetResult();

                if (second_confirm.Equals(basketId))
                {
                    _ptlCommandService.SendRawAsync(confirmedModule,
                        Pp505Builder.BuildOffCommand(confirmedModule), cancellationToken)
                    .GetAwaiter().GetResult();
                    break;
                }
                else
                {
                    _ptlCommandService.SendRawAsync(confirmedModule,
                        Pp505Builder.BuildOffCommand(confirmedModule), cancellationToken);
                    ArmsOtherBaskets([basketId], cancellationToken);
                    continue;
                }
               
            }
             /*if (confirmedModule != basketId)
            {
                _logger.LogWarning(
                    "Conferma ricevuta da modulo inatteso {ConfirmedModule}, in attesa di {ExpectedModule}. Invio comando errore",
                    confirmedModule,
                    basketId);

                try
                {
                    _ptlCommandService.SendRawAsync(confirmedModule,
                        Pp505Builder.BuildFlashRedCommandThenOff(confirmedModule), cancellationToken)
                    .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore nell'invio comando errore a modulo {ConfirmedModule}", confirmedModule);
                    throw;
                }

                _ptlCommandService.WaitForButtonAsync(
                    basketId,
                    cancellationToken
                ).GetAwaiter().GetResult();

                ArmsOtherBaskets([basketId], cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    "Conferma corretta ricevuta da modulo {ConfirmedModule}",
                    confirmedModule);
                break;
            } */

        } while (true);

    }

    private void ArmsOtherBaskets(string[] excludingBasketId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in ArmsOtherBaskets, escludendo Basket {ExcludingBasketId}", excludingBasketId);

        var basketsToArm = _cartContainer.GetCarts()
            .SelectMany(c => c.GetBaskets)
            .Where(b => !excludingBasketId.Contains(b.BasketId))
            .ToList();

        foreach (var basket in basketsToArm)
        {
            try
            {
                _ptlCommandService.SendRawAsync(basket.BasketId,
                     Pp505Builder.BuildArmedNoLight(basket.BasketId), cancellationToken
                ).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'armare basket {BasketId}", basket.BasketId);
                throw;
            }
        }
    }

    private void TurnOffOtherBaskets(string[] excludingBasketId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in TurnOffOtherBaskets, escludendo Basket {ExcludingBasketId}", excludingBasketId);

        var basketsToOff = _cartContainer.GetCarts()
            .SelectMany(c => c.GetBaskets)
            .Where(b => !excludingBasketId.Contains(b.BasketId))
            .ToList();

        foreach (var basket in basketsToOff)
        {
            try
            {
                _ptlCommandService.SendRawAsync(basket.BasketId,
                     Pp505Builder.BuildOffCommand(basket.BasketId), cancellationToken
                ).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nello spegnere basket {BasketId}", basket.BasketId);
                throw;
            }
        }
    }

    public void ResetAll(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in ResetAll");

        ResetAllBaskets(true, cancellationToken);

        _cartContainer.ResetAll();

        _logger.LogInformation("Reset completo di tutti i carrelli");
    }

    private void ResetAllBaskets(bool greenLightFirst, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in ResetAllBaskets");

        var baskets = _cartContainer.GetCarts().SelectMany(c => c.GetBaskets).ToList();

        if (greenLightFirst)
        {
            SendGreenCommandToBaskets(baskets, cancellationToken);

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        SendOffCommandToBaskets(baskets, cancellationToken);
    }

    private void SendGreenCommandToBaskets(IReadOnlyCollection<Basket> baskets, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in SendGreenCommandToBaskets");
        foreach (var basket in baskets)
        {
            TryToSendGreenCommand(basket.BasketId, cancellationToken);
        }
    }

    private void SendOffCommandToBaskets(IReadOnlyCollection<Basket> baskets, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Entrato in SendOffCommandToBaskets");
        foreach (var basket in baskets)
        {
            TryToResetBasket(basket.BasketId, cancellationToken);
        }
    }

    public void ShowStatus()
    {
        _logger.LogInformation("{CartStatus}", _cartContainer.GetStatusString());
    }

    private void Rollback(CartAssignmentResult assignment)
    {
        _logger.LogDebug("Entrato in Rollback");
        _cartContainer.Rollback(assignment);
    }

    private void TryToResetBasket(string basketId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Entrato in TryToResetBasket per Basket {BasketId}", basketId);

            _ptlCommandService.SendRawAsync(
                basketId,
                Pp505Builder.BuildOffCommand(basketId),
                cancellationToken
            ).GetAwaiter().GetResult();

        }
        catch (Exception ex)
        {
            _logger.LogError("Errore durante il reset del basket {BasketId}: {errorMessage}", basketId, ex.Message);
        }
    }
    private void TryToSendGreenCommand(string basketId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Entrato in TryToSendGreenCommand per Basket {BasketId}", basketId);

            _ptlCommandService.SendRawAsync(
                basketId,
                Pp505Builder.BuildGreenCommand(basketId),
                cancellationToken
            ).GetAwaiter().GetResult();

        }
        catch (Exception ex)
        {
            _logger.LogError("Errore durante il reset del basket {BasketId}: {errorMessage}", basketId, ex.Message);
        }
    }

    public void WriteCsvReport()
    {
        _logger.LogDebug("Entrato in WriteCsvReport");

        try
        {
            _cartReportWriter.Write(_cartContainer.GetCarts());
        }
        catch (Exception ex)
        {
            _logger.LogError("Errore durante la scrittura del report CSV: {errorMessage}", ex.Message);
            throw;
        }
    }
}