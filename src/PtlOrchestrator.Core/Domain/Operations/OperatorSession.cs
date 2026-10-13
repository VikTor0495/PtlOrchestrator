namespace PtlOrchestrator.Domain.Operations;

public sealed record OperatorSession(
    Guid SessionId,
    Guid OperatorId,
    string OperatorName,
    DateTimeOffset StartedAt);
