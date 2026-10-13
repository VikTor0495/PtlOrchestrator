namespace PtlOrchestrator.Api.Persistence.Entities;

public sealed class OperatorEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BadgeCode { get; set; }
    public string? PinHash { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
