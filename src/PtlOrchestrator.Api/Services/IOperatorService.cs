using PtlOrchestrator.Contracts.Operators;

namespace PtlOrchestrator.Api.Services;

public interface IOperatorService
{
    Task<IReadOnlyList<OperatorDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Operatori modificati dopo <paramref name="since"/> (tutti se nullo), inclusi i disattivati.</summary>
    Task<OperatorsSnapshotResponse> GetSnapshotAsync(DateTimeOffset? since, CancellationToken cancellationToken);

    Task<ServiceResult<OperatorDto>> CreateAsync(SaveOperatorRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<OperatorDto>> UpdateAsync(Guid operatorId, SaveOperatorRequest request, CancellationToken cancellationToken);
}
