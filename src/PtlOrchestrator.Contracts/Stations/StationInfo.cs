namespace PtlOrchestrator.Contracts.Stations;

/// <summary>Postazione nota al server; <see cref="LastSeenAt"/> è l'ultimo invio di eventi ricevuto.</summary>
public sealed record StationInfo(string Id, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt);
