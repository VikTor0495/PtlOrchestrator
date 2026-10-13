using Microsoft.Extensions.Logging;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Contracts.Security;
using PtlOrchestrator.Domain.Operations;

namespace PtlOrchestrator.Service.Impl;

public sealed class OperatorLoginService(
    IOperatorDirectory operatorDirectory,
    IOperatorSessionContext sessionContext,
    IOperationRecorder operationRecorder,
    IAppProcessingState appProcessingState,
    TimeProvider timeProvider,
    ILogger<OperatorLoginService> logger) : IOperatorLoginService
{
    private readonly IOperatorDirectory _operatorDirectory = operatorDirectory;
    private readonly IOperatorSessionContext _sessionContext = sessionContext;
    private readonly IOperationRecorder _operationRecorder = operationRecorder;
    private readonly IAppProcessingState _appProcessingState = appProcessingState;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<OperatorLoginService> _logger = logger;

    public async Task<LoginResult> LoginWithBadgeAsync(string badgeCode, CancellationToken cancellationToken)
    {
        var candidate = await _operatorDirectory.FindByBadgeAsync(badgeCode.Trim(), cancellationToken);

        return await StartSessionAsync(candidate, _ => true);
    }

    public async Task<LoginResult> LoginWithPinAsync(Guid operatorId, string pin, CancellationToken cancellationToken)
    {
        var candidate = await _operatorDirectory.FindByIdAsync(operatorId, cancellationToken);

        return await StartSessionAsync(candidate, operatorDto => PinHasher.Verify(pin, operatorDto.PinHash));
    }

    public async Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken)
    {
        if (!_sessionContext.HasActiveSession)
            return LogoutResult.NoActiveSession;

        if (_appProcessingState.IsBusy)
            return LogoutResult.SystemBusy;

        var session = _sessionContext.Current!;

        // l'evento va registrato prima di chiudere la sessione, altrimenti perde l'attribuzione
        await _operationRecorder.RecordAsync(OperationEventType.SessionEnded);
        _sessionContext.End();

        _logger.LogInformation("Sessione terminata per operatore {OperatorName}", session.OperatorName);
        return LogoutResult.Done;
    }

    private async Task<LoginResult> StartSessionAsync(OperatorDto? candidate, Func<OperatorDto, bool> credentialIsValid)
    {
        if (_sessionContext.HasActiveSession)
            return LoginResult.Failed(LoginFailure.SessionAlreadyActive);

        if (candidate is null)
            return LoginResult.Failed(LoginFailure.UnknownOperator);

        if (!candidate.IsActive)
            return LoginResult.Failed(LoginFailure.OperatorInactive);

        if (!credentialIsValid(candidate))
            return LoginResult.Failed(LoginFailure.InvalidPin);

        var now = _timeProvider.GetUtcNow();
        var session = new OperatorSession(Guid.CreateVersion7(now), candidate.Id, candidate.Name, now);

        _sessionContext.Begin(session);
        await _operationRecorder.RecordAsync(OperationEventType.SessionStarted);

        _logger.LogInformation("Sessione avviata per operatore {OperatorName}", session.OperatorName);
        return LoginResult.Succeeded(session);
    }
}
