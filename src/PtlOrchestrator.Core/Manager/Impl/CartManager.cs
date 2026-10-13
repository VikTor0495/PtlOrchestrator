using PtlOrchestrator.Builder;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Domain;
using PtlOrchestrator.File;
using PtlOrchestrator.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PtlOrchestrator.Manager.Impl;

public sealed class CartManager(
    CartContainer cartContainer,
    IPtlCommandService ptlCommandService,
    ICartReportWriter cartReportWriter,
    IBasketLimitService basketLimitService,
    IOperationRecorder operationRecorder,
    IOptions<TimeoutOptions> timeoutOptions,
    ILogger<CartManager> logger) : ICartManager
{

    private readonly CartContainer _cartContainer = cartContainer;

    private readonly IPtlCommandService _ptlCommandService = ptlCommandService;

    private readonly ICartReportWriter _cartReportWriter = cartReportWriter;

    private readonly IBasketLimitService _basketLimitService = basketLimitService;

    private readonly IOperationRecorder _operationRecorder = operationRecorder;

    private readonly TimeoutOptions _timeoutOptions = timeoutOptions.Value;

    private readonly ILogger<CartManager> _logger = logger;


    public async Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken)
    {

        CartAssignmentResult result;

        if (!_basketLimitService.HasMaxLimit(barcode))
        {
            return await RejectAsync(
                barcode,
                $"Barcode {barcode} non presente nel file di configurazione dei limiti di carico");
        }

        result = _cartContainer.AssignItem(barcode, _basketLimitService.GetMaxFor(barcode));

        if (!result.Success)
        {
            await RecordRejectionAsync(barcode, result.Reason);
            return result;
        }

        _logger.LogInformation(
              "ACCETTATO barcode {Barcode} → Cart {CartId} Basket {BasketId} ({Type})",
              barcode,
              result.Cart!.CartId,
              result.Basket!.BasketId,
              result.Type);

        await _operationRecorder.RecordAsync(
            OperationEventType.BarcodeAccepted,
            barcode,
            result.Cart.CartId,
            result.Basket.BasketId,
            details: result.Type.ToString());

        try
        {

            using var timeoutCts = SetupTimeoutCancellationTokenSource(cancellationToken);

            await HandlePickingProcessFlow(result, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            await ResetAllBaskets(false, null, cancellationToken);
            Rollback(result);

            var timeoutReason = $"Timeout raggiunto durante il processo di inserimento Carrello {result.Cart!.CartId}, Basket {result.Basket!.BasketId}";
            await _operationRecorder.RecordAsync(
                OperationEventType.OperationTimedOut,
                barcode,
                result.Cart.CartId,
                result.Basket.BasketId,
                timeoutReason);

            return CartAssignmentResult.Rejected(timeoutReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Errore PTL su Cart {CartId}, Basket {BasketId} → rollback",
                result.Cart.CartId,
                result.Basket.BasketId);
            Rollback(result);

            await _operationRecorder.RecordAsync(
                OperationEventType.PtlFault,
                barcode,
                result.Cart.CartId,
                result.Basket.BasketId,
                ex.Message);

            return CartAssignmentResult.Rejected($"Errore nel processo di inserimento Carrello {result.Cart!.CartId}, Basket {result.Basket!.BasketId}: {ex.Message}");
        }

        return result;

    }

    private async Task<CartAssignmentResult> RejectAsync(string barcode, string reason)
    {
        await RecordRejectionAsync(barcode, reason);
        return CartAssignmentResult.Rejected(reason);
    }

    private Task RecordRejectionAsync(string barcode, string? reason)
        => _operationRecorder.RecordAsync(OperationEventType.BarcodeRejected, barcode, details: reason);

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
            await HandlePickLightFlow(result, cancellationToken);


            _logger.LogInformation(
                "Conferma ricevuta per Cart {CartId}, Basket {BasketId}",
                result.Cart!.CartId,
                result.Basket.BasketId);

            await _operationRecorder.RecordAsync(
                OperationEventType.BasketConfirmed,
                result.Basket.Barcode,
                result.Cart.CartId,
                result.Basket.BasketId);


            await Task.Delay(600, cancellationToken);
            await _ptlCommandService.SendRawAsync(
                       Pp505Builder.BuildArmedNoLightButtonDisabled(GetBasketIdsEnumerable()), cancellationToken
                  );

        }
        else
        {
            _logger.LogWarning("HandlePickingProcessFlow chiamato con result o basket null");
        }
    }

    private async Task HandlePickLightFlow(CartAssignmentResult assignment, CancellationToken cancellationToken)
    {
        var basketId = assignment.Basket!.BasketId;

        await _ptlCommandService.SendRawAsync(
            Pp505Builder.BuildFlashGreenCommandThenFix(basketId), cancellationToken);

        await _ptlCommandService.SendRawAsync(
            Pp505Builder.BuildArmedNoLightEnabledButton([.. GetBasketIdsEnumerable().Where(id => id != basketId)]), cancellationToken);

        await HandleOperatorConfirmationFlow(assignment, cancellationToken);
    }


    private async Task HandleOperatorConfirmationFlow(CartAssignmentResult assignment, CancellationToken cancellationToken)
    {
        var basketId = assignment.Basket!.BasketId;

        string? lastWrongBasketId = null;


        while (true)
        {

            var confirmedModule = await _ptlCommandService
                .WaitForButtonAsync(basketId, cancellationToken);

            // CONFERMA CORRETTA
            if (confirmedModule == basketId)
            {
                if (lastWrongBasketId != null && lastWrongBasketId != basketId)
                {
                    await _ptlCommandService.SendRawAsync(
                        Pp505Builder.BuildArmedNoLightEnabledButton([lastWrongBasketId]),
                        cancellationToken
                    );
                }

                break;
            }

            _logger.LogWarning(
                "Inserimento errato in modulo {WrongBasket}, atteso {ExpectedBasket}",
                confirmedModule,
                basketId);

            await _operationRecorder.RecordAsync(
                OperationEventType.WrongModulePressed,
                assignment.Basket.Barcode,
                assignment.Cart!.CartId,
                basketId,
                details: $"Premuto modulo {confirmedModule}, atteso {basketId}");

            if (confirmedModule == lastWrongBasketId)
            {
                await _ptlCommandService.SendRawAsync(
                    Pp505Builder.BuildArmedNoLightEnabledButton([confirmedModule]),
                    cancellationToken
                );
                lastWrongBasketId = null;

                continue;
            }

            // spegni il rosso precedente (se esiste)
            if (lastWrongBasketId != null && lastWrongBasketId != basketId)
            {
                await _ptlCommandService.SendRawAsync(
                    Pp505Builder.BuildArmedNoLightEnabledButton([lastWrongBasketId]),
                    cancellationToken
                );
            }

            // accendi rosso fisso + beep sul nuovo basket errato
            await _ptlCommandService.SendRawAsync(
                Pp505Builder.BuildRedBuzzer(confirmedModule),
                cancellationToken
            );

            lastWrongBasketId = confirmedModule;
        }
    }


    public async Task ResetAll(CancellationToken cancellationToken)
    {

        await ResetAllBaskets(true, null, cancellationToken);

        _cartContainer.ResetAll();

        _logger.LogInformation("Reset completo di tutti i carrelli");

        await _operationRecorder.RecordAsync(OperationEventType.CartsReset);
    }

    private async Task ResetAllBaskets(bool greenLightFirst, string? basketAvoidReset, CancellationToken cancellationToken)
    {

        var basketsIds = GetBasketIdsEnumerable();

        if (greenLightFirst)
        {
            await SendGreenCommandToBaskets(basketsIds, cancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        await _ptlCommandService.SendRawAsync(
                Pp505Builder.BuildArmedNoLightButtonDisabled(basketAvoidReset != null ? [.. basketsIds.Where(b => b != basketAvoidReset)] : basketsIds),
                cancellationToken);

    }

    private string[] GetBasketIdsEnumerable()
    {
        return [.. _cartContainer.GetCarts().SelectMany(c => c.GetBaskets).Select(b => b.BasketId)];
    }

    private async Task SendGreenCommandToBaskets(string[] basketIds, CancellationToken cancellationToken)
    {
        await _ptlCommandService.SendRawAsync(
                Pp505Builder.BuildGreenCommand(basketIds),
                cancellationToken);
    }

    public void ShowStatus()
    {
        _logger.LogInformation("{CartStatus}", _cartContainer.GetStatusString());
    }

    private void Rollback(CartAssignmentResult assignment)
    {
        _cartContainer.Rollback(assignment);
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
