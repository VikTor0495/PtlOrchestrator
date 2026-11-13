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
    ICartAssignmentClient cartAssignmentClient,
    ILightstepConnectionService connectionService,
    IOperatorSessionContext sessionContext,
    IOptions<StationOptions> stationOptions,
    IActiveCartStore activeCartStore,
    ILogger<CartManager> logger) : ICartManager
{
    private static readonly TimeSpan GreenToResetDelay = TimeSpan.FromSeconds(1);

    private readonly CartContainer _cartContainer = cartContainer;
    private readonly IPtlCommandService _ptlCommandService = ptlCommandService;
    private readonly ICartReportWriter _cartReportWriter = cartReportWriter;
    private readonly IBasketLimitService _basketLimitService = basketLimitService;
    private readonly IOperationRecorder _operationRecorder = operationRecorder;
    private readonly TimeoutOptions _timeoutOptions = timeoutOptions.Value;
    private readonly ICartAssignmentClient _assignmentClient = cartAssignmentClient;
    private readonly ILightstepConnectionService _connectionService = connectionService;
    private readonly IOperatorSessionContext _sessionContext = sessionContext;
    private readonly string _stationId = stationOptions.Value.StationId;
    private readonly IActiveCartStore _activeCartStore = activeCartStore;
    private readonly ILogger<CartManager> _logger = logger;

    public async Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken)
    {
        if (!_basketLimitService.HasMaxLimit(barcode))
        {
            return await RejectAsync(
                barcode,
                $"Codice {barcode} non riconosciuto: non è un carrello né un prodotto configurato");
        }

        var result = _cartContainer.AssignItem(barcode, _basketLimitService.GetMaxFor(barcode));

        if (!result.Success)
        {
            await RecordRejectionAsync(barcode, result.Reason);

            // carrelli attivi ma pieni: si chiude lo sventagliamento (CSV + azzeramento) e si chiede di riscansionare
            if (_cartContainer.HasItems())
            {
                await CloseWorkAsync(cancellationToken);
                return CartAssignmentResult.Rejected(
                    "Carrelli pieni: report generato e carrelli azzerati. Riscansiona il prodotto");
            }

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
            await ResetBasketsAsync(GetActiveEndpoints(), greenLightFirst: false, cancellationToken);
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

    public bool IsKnownCart(string code) => _assignmentClient.IsKnownCart(code);

    public async Task<CartCheckInOutcome> CheckInAsync(string cartCode, CancellationToken cancellationToken)
    {
        if (_cartContainer.IsActive(cartCode))
            return new(CartCheckInResult.AlreadyActive, $"Il carrello {cartCode} è già attivo");

        // il limite si controlla prima di occupare il carrello sul server
        if (_cartContainer.ActiveCount >= CartContainer.MaxActiveCarts)
            return new(CartCheckInResult.LimitReached,
                $"Massimo {CartContainer.MaxActiveCarts} carrelli per volta: rimuovine uno prima di fare un altro check-in");

        var sessionId = _sessionContext.Current?.SessionId;

        CartClaimResult claim;
        try
        {
            claim = await _assignmentClient.ClaimAsync(cartCode, _stationId, sessionId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Server carrelli non raggiungibile durante il check-in di {CartCode}", cartCode);
            return new(CartCheckInResult.Rejected, "Server non raggiungibile: impossibile verificare il carrello");
        }

        if (!claim.Success)
            return new(CartCheckInResult.Rejected, claim.Error ?? $"Carrello {cartCode} non disponibile");

        var info = claim.Cart!;
        var endpoint = new PtlEndpoint(info.ControllerIp, info.ControllerPort);

        if (!await _connectionService.TryConnectAsync(endpoint, cancellationToken))
        {
            // il carrello è stato occupato sul server ma non è utilizzabile: si libera subito
            await TryReleaseOnServerAsync(cartCode, cancellationToken);
            return new(CartCheckInResult.Rejected,
                $"Controller PTL {endpoint} non raggiungibile: carrello {cartCode} non attivato");
        }

        var activation = _cartContainer.Activate(new Cart(
            info.CartCode,
            info.BasketIds.Select(id => new Basket(basketId: id, maxQuantity: 0)),
            endpoint));

        if (activation != CartCheckInResult.Done)
        {
            await TryReleaseOnServerAsync(cartCode, cancellationToken);
            return new(activation, $"Carrello {cartCode} non attivato");
        }

        _logger.LogInformation("Check-in del carrello {CartCode} sul controller {Endpoint}", cartCode, endpoint);
        await _operationRecorder.RecordAsync(OperationEventType.CartCheckedIn, cartId: cartCode, details: endpoint.ToString());
        await PersistActiveCartsAsync();

        return new(CartCheckInResult.Done, $"Carrello {cartCode} attivato: puoi scansionare i prodotti");
    }

    public async Task<CartReleaseOutcome> ReleaseCartAsync(string cartCode, CancellationToken cancellationToken)
    {
        var cart = _cartContainer.GetActive(cartCode);
        if (cart is null)
            return new(false, $"Il carrello {cartCode} non è attivo");

        if (!_cartContainer.IsEmpty(cartCode))
            return new(false, $"Il carrello {cartCode} contiene prodotti: premi Reset prima di rimuoverlo");

        try
        {
            await _assignmentClient.ReleaseAsync(cartCode, _stationId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(false, $"Rilascio del carrello {cartCode} sul server non riuscito: {ex.Message}");
        }

        await TurnOffBasketsAsync(cart, cancellationToken);
        _cartContainer.Release(cartCode);

        _logger.LogInformation("Carrello {CartCode} rilasciato", cartCode);
        await _operationRecorder.RecordAsync(OperationEventType.CartReleased, cartId: cartCode);
        await PersistActiveCartsAsync();

        return new(true, $"Carrello {cartCode} rimosso dal turno");
    }

    public IReadOnlyList<string> ActiveCartIds()
        => [.. _cartContainer.GetCarts().Select(c => c.CartId)];

    public bool HasItems() => _cartContainer.HasItems();

    public async Task CloseWorkAsync(CancellationToken cancellationToken)
    {
        WriteCsvReport();
        await ResetAll(cancellationToken);
    }

    public async Task ResetAll(CancellationToken cancellationToken)
    {
        await ResetBasketsAsync(GetActiveEndpoints(), greenLightFirst: true, cancellationToken);

        _cartContainer.ResetAll();

        _logger.LogInformation("Reset completo di tutti i carrelli");

        await _operationRecorder.RecordAsync(OperationEventType.CartsReset);
    }

    public void ShowStatus()
    {
        _logger.LogInformation("{CartStatus}", _cartContainer.GetStatusString());
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
                Pp505Builder.BuildArmedNoLightButtonDisabled(BasketIdsOf(result.Cart!.Endpoint)),
                result.Cart.Endpoint,
                cancellationToken);
        }
        else
        {
            _logger.LogWarning("HandlePickingProcessFlow chiamato con result o basket null");
        }
    }

    private async Task HandlePickLightFlow(CartAssignmentResult assignment, CancellationToken cancellationToken)
    {
        var basketId = assignment.Basket!.BasketId;
        var endpoint = assignment.Cart!.Endpoint;

        await _ptlCommandService.SendRawAsync(
            Pp505Builder.BuildFlashGreenCommandThenFix(basketId), endpoint, cancellationToken);

        await _ptlCommandService.SendRawAsync(
            Pp505Builder.BuildArmedNoLightEnabledButton([.. BasketIdsOf(endpoint).Where(id => id != basketId)]),
            endpoint,
            cancellationToken);

        await HandleOperatorConfirmationFlow(assignment, cancellationToken);
    }

    private async Task HandleOperatorConfirmationFlow(CartAssignmentResult assignment, CancellationToken cancellationToken)
    {
        var basketId = assignment.Basket!.BasketId;
        var endpoint = assignment.Cart!.Endpoint;

        string? lastWrongBasketId = null;

        while (true)
        {
            var confirmedModule = await _ptlCommandService
                .WaitForButtonAsync(basketId, endpoint, cancellationToken);

            // CONFERMA CORRETTA
            if (confirmedModule == basketId)
            {
                if (lastWrongBasketId != null && lastWrongBasketId != basketId)
                {
                    await _ptlCommandService.SendRawAsync(
                        Pp505Builder.BuildArmedNoLightEnabledButton([lastWrongBasketId]),
                        endpoint,
                        cancellationToken);
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
                    endpoint,
                    cancellationToken);
                lastWrongBasketId = null;

                continue;
            }

            // spegni il rosso precedente (se esiste)
            if (lastWrongBasketId != null && lastWrongBasketId != basketId)
            {
                await _ptlCommandService.SendRawAsync(
                    Pp505Builder.BuildArmedNoLightEnabledButton([lastWrongBasketId]),
                    endpoint,
                    cancellationToken);
            }

            // accendi rosso fisso + beep sul nuovo basket errato
            await _ptlCommandService.SendRawAsync(
                Pp505Builder.BuildRedBuzzer(confirmedModule),
                endpoint,
                cancellationToken);

            lastWrongBasketId = confirmedModule;
        }
    }

    /// <summary>Azzera i cestelli di ogni controller: su ciascuno solo i propri moduli.</summary>
    private async Task ResetBasketsAsync(IEnumerable<PtlEndpoint?> endpoints, bool greenLightFirst, CancellationToken cancellationToken)
    {
        foreach (var endpoint in endpoints)
        {
            var basketIds = BasketIdsOf(endpoint);
            if (basketIds.Length == 0)
                continue;

            if (greenLightFirst)
            {
                await _ptlCommandService.SendRawAsync(Pp505Builder.BuildGreenCommand(basketIds), endpoint, cancellationToken);
                await Task.Delay(GreenToResetDelay, cancellationToken);
            }

            await _ptlCommandService.SendRawAsync(
                Pp505Builder.BuildArmedNoLightButtonDisabled(basketIds),
                endpoint,
                cancellationToken);
        }
    }

    private async Task TurnOffBasketsAsync(Cart cart, CancellationToken cancellationToken)
    {
        var basketIds = cart.GetBaskets.Select(b => b.BasketId).ToArray();
        if (basketIds.Length == 0)
            return;

        try
        {
            await _ptlCommandService.SendRawAsync(Pp505Builder.BuildOffCommand(basketIds), cart.Endpoint, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // il carrello è già stato rilasciato sul server: un controller spento non blocca la rimozione
            _logger.LogWarning(ex, "Spegnimento delle lampade del carrello {CartId} non riuscito", cart.CartId);
        }
    }

    /// <summary>Ricorda i carrelli attivi: dopo una chiusura o un riavvio la postazione li riprende da sola.</summary>
    private async Task PersistActiveCartsAsync()
    {
        try
        {
            await _activeCartStore.SaveAsync(ActiveCartIds(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Salvataggio dei carrelli attivi non riuscito");
        }
    }

    private async Task TryReleaseOnServerAsync(string cartCode, CancellationToken cancellationToken)
    {
        try
        {
            await _assignmentClient.ReleaseAsync(cartCode, _stationId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Rilascio del carrello {CartCode} dopo un check-in fallito non riuscito", cartCode);
        }
    }

    /// <summary>Controller usati dai carrelli attivi (il riferimento nullo è il controller predefinito).</summary>
    private IEnumerable<PtlEndpoint?> GetActiveEndpoints()
        => _cartContainer.GetCarts().Select(c => c.Endpoint).Distinct();

    private string[] BasketIdsOf(PtlEndpoint? endpoint)
        => [.. _cartContainer.GetCarts()
            .Where(c => c.Endpoint == endpoint)
            .SelectMany(c => c.GetBaskets)
            .Select(b => b.BasketId)];

    private void Rollback(CartAssignmentResult assignment)
    {
        _cartContainer.Rollback(assignment);
    }
}
