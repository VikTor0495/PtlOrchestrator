namespace PtlOrchestrator.Contracts.Operations;

/// <summary>Riepilogo di un turno di lavoro con i contatori delle operazioni svolte.</summary>
public sealed record WorkSessionSummary(
    Guid Id,
    Guid OperatorId,
    string OperatorName,
    string StationId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int ProductsAccepted,
    int ProductsRejected,
    int WrongModulePresses,
    int Timeouts,
    int Faults);
