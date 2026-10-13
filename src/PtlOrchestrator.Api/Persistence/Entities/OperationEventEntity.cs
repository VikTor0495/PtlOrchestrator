using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Persistence.Entities;

/// <summary>
/// Riga del log append-only. Non ha chiavi esterne verso sessioni e operatori di proposito:
/// l'acquisizione di un evento non deve mai fallire per un ritardo sui dati di riferimento.
/// </summary>
public sealed class OperationEventEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string StationId { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public Guid? OperatorId { get; set; }
    public OperationEventType Type { get; set; }
    public string? Barcode { get; set; }
    public string? CartId { get; set; }
    public string? BasketId { get; set; }
    public string? Details { get; set; }
}
