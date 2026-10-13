namespace PtlOrchestrator.Contracts.Operators;

/// <summary>
/// Operatore così come lo vede la postazione. Il PIN non viaggia mai in chiaro:
/// <see cref="PinHash"/> è nel formato prodotto da <c>PinHasher</c>.
/// </summary>
public sealed record OperatorDto(
    Guid Id,
    string Name,
    string? BadgeCode,
    string? PinHash,
    bool IsActive,
    DateTimeOffset UpdatedAt);
