namespace PtlOrchestrator.Contracts.Operators;

/// <param name="Pin">PIN in chiaro da impostare; se nullo in modifica il PIN esistente resta invariato.</param>
public sealed record SaveOperatorRequest(
    string Name,
    string? BadgeCode,
    string? Pin,
    bool IsActive = true);
