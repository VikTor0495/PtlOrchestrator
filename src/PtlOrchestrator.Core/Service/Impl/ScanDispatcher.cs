using Microsoft.Extensions.Logging;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Manager;

namespace PtlOrchestrator.Service.Impl;

public sealed class ScanDispatcher(
    IOperatorSessionContext sessionContext,
    IOperatorLoginService operatorLoginService,
    ICartManager cartManager,
    IAppProcessingState appProcessingState,
    ILogger<ScanDispatcher> logger) : IScanDispatcher
{
    private readonly IOperatorSessionContext _sessionContext = sessionContext;
    private readonly IOperatorLoginService _operatorLoginService = operatorLoginService;
    private readonly ICartManager _cartManager = cartManager;
    private readonly IAppProcessingState _appProcessingState = appProcessingState;
    private readonly ILogger<ScanDispatcher> _logger = logger;

    public async Task<ScanOutcome> DispatchAsync(string scannedCode, CancellationToken cancellationToken)
    {
        var code = scannedCode.Trim();

        if (code.Length == 0)
            return ScanOutcome.Ignored();

        return _sessionContext.HasActiveSession
            ? await ProcessSessionScanAsync(code, cancellationToken)
            : await ProcessBadgeAsync(code, cancellationToken);
    }

    private async Task<ScanOutcome> ProcessBadgeAsync(string badgeCode, CancellationToken cancellationToken)
    {
        var login = await _operatorLoginService.LoginWithBadgeAsync(badgeCode, cancellationToken);

        return login.Success
            ? ScanOutcome.OperatorLoggedIn(login.Session!)
            : ScanOutcome.LoginRejected(login.Failure!.Value);
    }

    /// <summary>Con turno attivo un codice è il check-in di un carrello oppure un prodotto da assegnare.</summary>
    private async Task<ScanOutcome> ProcessSessionScanAsync(string code, CancellationToken cancellationToken)
    {
        if (!_appProcessingState.TryEnter())
            return ScanOutcome.SystemBusy();

        try
        {
            if (_cartManager.IsKnownCart(code))
                return await ProcessCartCheckInAsync(code, cancellationToken);

            var assignment = await _cartManager.ProcessBarcode(code, cancellationToken);

            return assignment.Success
                ? ScanOutcome.ProductAccepted(assignment)
                : ScanOutcome.ProductRejected(assignment);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Errore processamento scansione {Code}", code);
            return ScanOutcome.ProcessingFailed(ex.Message);
        }
        finally
        {
            _appProcessingState.Exit();
        }
    }

    private async Task<ScanOutcome> ProcessCartCheckInAsync(string cartId, CancellationToken cancellationToken)
    {
        var outcome = await _cartManager.CheckInAsync(cartId, cancellationToken);

        return outcome.Success
            ? ScanOutcome.CartCheckedIn(outcome.Message)
            : ScanOutcome.CartCheckInRejected(outcome.Message);
    }
}
