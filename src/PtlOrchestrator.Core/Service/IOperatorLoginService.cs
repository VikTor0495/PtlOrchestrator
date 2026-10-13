using PtlOrchestrator.Domain.Operations;

namespace PtlOrchestrator.Service;

public interface IOperatorLoginService
{
    Task<LoginResult> LoginWithBadgeAsync(string badgeCode, CancellationToken cancellationToken);

    Task<LoginResult> LoginWithPinAsync(Guid operatorId, string pin, CancellationToken cancellationToken);

    Task<LogoutResult> LogoutAsync(CancellationToken cancellationToken);
}
