namespace PtlOrchestrator.Contracts.Operators;

public sealed record OperatorsSnapshotResponse(
    IReadOnlyList<OperatorDto> Operators,
    DateTimeOffset ServerTime);
