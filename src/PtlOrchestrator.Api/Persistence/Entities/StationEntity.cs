namespace PtlOrchestrator.Api.Persistence.Entities;

public sealed class StationEntity
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
