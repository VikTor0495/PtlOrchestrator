using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Contracts.Security;
using PtlOrchestrator.Service;

namespace PtlOrchestrator.Core.Tests.Support;

internal sealed class FakeOperatorDirectory : IOperatorDirectory
{
    private readonly List<OperatorDto> _operators = [];

    public OperatorDto Add(
        string name = "Mario Rossi",
        string? badgeCode = "BADGE-1",
        string? pin = "4821",
        bool isActive = true)
    {
        var operatorDto = new OperatorDto(
            Guid.NewGuid(),
            name,
            badgeCode,
            pin is null ? null : PinHasher.Hash(pin),
            isActive,
            DateTimeOffset.UtcNow);

        _operators.Add(operatorDto);
        return operatorDto;
    }

    public Task<OperatorDto?> FindByBadgeAsync(string badgeCode, CancellationToken cancellationToken)
        => Task.FromResult(_operators.FirstOrDefault(o => o.BadgeCode == badgeCode));

    public Task<OperatorDto?> FindByIdAsync(Guid operatorId, CancellationToken cancellationToken)
        => Task.FromResult(_operators.FirstOrDefault(o => o.Id == operatorId));

    public Task<IReadOnlyList<OperatorDto>> GetActiveAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OperatorDto>>([.. _operators.Where(o => o.IsActive)]);
}
