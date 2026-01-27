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
using System.Threading.Tasks;

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


    public async Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken)
    {

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

            await HandlePickingProcessFlow(result, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            await ResetAllBaskets(false, null, cancellationToken);
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

    private async Task HandlePickingProcessFlow(CartAssignmentResult result, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fine HandlePickingProcessFlow per Basket {BasketId}", result.Basket?.BasketId);

        if (result != null && result.Basket != null)
        {
            await HandlePickLightFlow(result.Basket.BasketId, cancellationToken);
            await HandleOperatorConfirmationFlow(result.Basket.BasketId, cancellationToken);

            _logger.LogInformation(
                "Conferma ricevuta per Cart {CartId}, Basket {BasketId}",
                result.Cart!.CartId,
                result.Basket.BasketId);

            
            await Task.Delay(1000, cancellationToken);
            await _ptlCommandService.SendRawAsync(result.Basket.BasketId,
                       Pp505Builder.BuildOffCommand(result.Basket.BasketId), cancellationToken
                  );

           // await ResetAllBaskets(false, result.Basket.BasketId, cancellationToken);
        }
        else
        {
            _logger.LogWarning("HandlePickingProcessFlow chiamato con result o basket null");
        }
    }

    private async Task HandlePickLightFlow(string basketId, CancellationToken cancellationToken)
    {

        await _ptlCommandService.SendRawAsync(basketId,
                                      Pp505Builder.BuildFlashGreenCommandThenOff(basketId), cancellationToken);

        ActiveAllModulesForPicking(basketId, cancellationToken);
    }


    private async Task HandleOperatorConfirmationFlow(string basketId, CancellationToken cancellationToken)
    {

        string? lastWrongBasketId = null;

        while (true)
        {
            var confirmedModule = await _ptlCommandService
                .WaitForButtonAsync(basketId, cancellationToken);

            // CONFERMA CORRETTA
            if (confirmedModule == basketId)
            {
                _logger.LogInformation(
                    "Conferma corretta ricevuta da modulo {BasketId}",
                    basketId);

                if (lastWrongBasketId != null && lastWrongBasketId != basketId)
                {
                    await _ptlCommandService.SendRawAsync(
                        lastWrongBasketId,
                        Pp505Builder.BuildArmedNoLight(lastWrongBasketId),
                        cancellationToken
                    );
                }

                break;
            }

            _logger.LogWarning(
                "Inserimento errato in modulo {WrongBasket}, atteso {ExpectedBasket}",
                confirmedModule,
                basketId);

            if (confirmedModule == lastWrongBasketId)
            {
                await _ptlCommandService.SendRawAsync(
                    confirmedModule,
                    Pp505Builder.BuildArmedNoLight(confirmedModule),
                    cancellationToken
                );
                lastWrongBasketId = null;

                continue;
            }

            // spegni il rosso precedente (se esiste)
            if (lastWrongBasketId != null && lastWrongBasketId != basketId)
            {
                await _ptlCommandService.SendRawAsync(
                    lastWrongBasketId,
                    Pp505Builder.BuildArmedNoLight(lastWrongBasketId),
                    cancellationToken
                );
            }

            // accendi rosso fisso + beep sul nuovo basket errato
            await _ptlCommandService.SendRawAsync(
                confirmedModule,
                Pp505Builder.BuildRedBuzzer(confirmedModule),
                cancellationToken
            );

            lastWrongBasketId = confirmedModule;
        }
    }


    private void ActiveAllModulesForPicking(string excludingBasketId, CancellationToken cancellationToken)
    {

        var basketsToArm = _cartContainer.GetCarts()
            .SelectMany(c => c.GetBaskets)
            .Where(b => b.BasketId != excludingBasketId).ToList();

        foreach (var basket in basketsToArm)
        {
            try
            {
                _ptlCommandService.SendRawAsync(basket.BasketId,
                                 Pp505Builder.BuildArmedNoLight(basket.BasketId), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'armare basket {BasketId}", basket.BasketId);
                throw;
            }
        }
    }

    private void TurnOffOtherBasketsAsync(string[] excludingBasketId, CancellationToken cancellationToken)
    {

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
                  );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nello spegnere basket {BasketId}", basket.BasketId);
                throw;
            }
        }
    }

    public async Task ResetAll(CancellationToken cancellationToken)
    {

        await ResetAllBaskets(true, null,  cancellationToken);

        _cartContainer.ResetAll();

        _logger.LogInformation("Reset completo di tutti i carrelli");
    }

    private async Task ResetAllBaskets(bool greenLightFirst, string? basketAvoidReset,CancellationToken cancellationToken)
    {

        var baskets = _cartContainer.GetCarts().SelectMany(c => c.GetBaskets).ToList();

        if (greenLightFirst)
        {
            SendGreenCommandToBaskets(baskets, cancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        SendOffCommandToBaskets(basketAvoidReset != null ? [.. baskets.Where(b => b.BasketId != basketAvoidReset)] : baskets, cancellationToken);
    }

    private void SendGreenCommandToBaskets(IReadOnlyCollection<Basket> baskets, CancellationToken cancellationToken)
    {
        foreach (var basket in baskets)
        {
            TryToSendGreenCommand(basket.BasketId, cancellationToken);
        }


    }

    private void SendOffCommandToBaskets(IReadOnlyCollection<Basket> baskets, CancellationToken cancellationToken)
    {
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
        _cartContainer.Rollback(assignment);
    }


    private void TryToResetBasket(string basketId, CancellationToken cancellationToken)
    {
        try
        {

            _ptlCommandService.SendRawAsync(
                basketId,
                Pp505Builder.BuildOffCommand(basketId),
                cancellationToken);

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
            _ptlCommandService.SendRawAsync(
                basketId,
                Pp505Builder.BuildGreenCommand(basketId),
                cancellationToken
            );

        }
        catch (Exception ex)
        {
            _logger.LogError("Errore durante il reset del basket {BasketId}: {errorMessage}", basketId, ex.Message);
        }
    }

    public void WriteCsvReport()
    {
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