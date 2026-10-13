namespace PtlOrchestrator.Api.Persistence.Entities;

/// <summary>
/// Turno di lavoro di un operatore su una postazione. È una proiezione ricavata dagli eventi
/// SessionStarted/SessionEnded: la fonte di verità resta il log degli eventi.
/// </summary>
public sealed class WorkSessionEntity
{
    public Guid Id { get; set; }
    public Guid OperatorId { get; set; }
    public string StationId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public OperatorEntity? Operator { get; set; }
}
