namespace PtlOrchestrator.Contracts.Operations;

/// <param name="Accepted">Eventi nuovi memorizzati.</param>
/// <param name="Duplicates">Eventi già noti al server, ignorati.</param>
public sealed record IngestEventsResponse(int Accepted, int Duplicates);
