namespace PtlOrchestrator.Contracts.Operations;

public sealed record IngestEventsRequest(IReadOnlyList<OperationEvent> Events);
