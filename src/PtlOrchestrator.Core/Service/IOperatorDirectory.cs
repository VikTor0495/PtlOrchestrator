using PtlOrchestrator.Contracts.Operators;

namespace PtlOrchestrator.Service;

/// <summary>
/// Anagrafica operatori disponibile in locale sulla postazione (cache sincronizzata dal server):
/// il login deve funzionare anche senza rete.
/// </summary>
public interface IOperatorDirectory
{
    Task<OperatorDto?> FindByBadgeAsync(string badgeCode, CancellationToken cancellationToken);

    Task<OperatorDto?> FindByIdAsync(Guid operatorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OperatorDto>> GetActiveAsync(CancellationToken cancellationToken);
}
