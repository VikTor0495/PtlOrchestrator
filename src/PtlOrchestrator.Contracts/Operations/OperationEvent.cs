namespace PtlOrchestrator.Contracts.Operations;

/// <summary>
/// Fatto immutabile accaduto su una postazione. L'<see cref="Id"/> è generato dalla postazione:
/// il server lo usa per ignorare i duplicati quando una sincronizzazione viene ritentata.
/// </summary>
public sealed record OperationEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string StationId,
    Guid? SessionId,
    Guid? OperatorId,
    OperationEventType Type,
    string? Barcode = null,
    string? CartId = null,
    string? BasketId = null,
    string? Details = null);
