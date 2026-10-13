using Microsoft.Extensions.Logging;
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
            ? await ProcessProductAsync(code, cancellationToken)
            : await ProcessBadgeAsync(code, cancellationToken);
    }

    private async Task<ScanOutcome> ProcessBadgeAsync(string badgeCode, CancellationToken cancellationToken)
    {
        var login = await _operatorLoginService.LoginWithBadgeAsync(badgeCode, cancellationToken);

        return login.Success
            ? ScanOutcome.OperatorLoggedIn(login.Session!)
            : ScanOutcome.LoginRejected(login.Failure!.Value);
    }

    private async Task<ScanOutcome> ProcessProductAsync(string barcode, CancellationToken cancellationToken)
    {
        if (!_appProcessingState.TryEnter())
            return ScanOutcome.SystemBusy();

        try
        {
            var assignment = await _cartManager.ProcessBarcode(barcode, cancellationToken);

            return assignment.Success
                ? ScanOutcome.ProductAccepted(assignment)
                : ScanOutcome.ProductRejected(assignment);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Errore processamento barcode {Barcode}", barcode);
            return ScanOutcome.ProcessingFailed(ex.Message);
        }
        finally
        {
            _appProcessingState.Exit();
        }
    }
}
