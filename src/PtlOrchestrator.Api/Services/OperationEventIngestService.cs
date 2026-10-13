using Microsoft.EntityFrameworkCore;
using PtlOrchestrator.Api.Persistence;
using PtlOrchestrator.Api.Persistence.Configurations;
using PtlOrchestrator.Api.Persistence.Entities;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Services;

public sealed class OperationEventIngestService(
    PtlDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<OperationEventIngestService> logger) : IOperationEventIngestService
{
    private readonly PtlDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<OperationEventIngestService> _logger = logger;

    public async Task<IngestEventsResponse> IngestAsync(IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken)
    {
        try
        {
            return await StoreAsync(events, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // due richieste concorrenti con gli stessi eventi: la seconda perde sulla chiave primaria.
            // Ripartendo da un contesto pulito gli eventi appena scritti risultano duplicati.
            _logger.LogWarning(ex, "Conflitto durante l'acquisizione degli eventi, nuovo tentativo");
            _dbContext.ChangeTracker.Clear();

            return await StoreAsync(events, cancellationToken);
        }
    }

    private async Task<IngestEventsResponse> StoreAsync(IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken)
    {
        var receivedAt = _timeProvider.GetUtcNow();

        var newEvents = await SelectNewEventsAsync(events, cancellationToken);

        if (newEvents.Count > 0)
        {
            await TouchStationsAsync(newEvents, receivedAt, cancellationToken);

            _dbContext.OperationEvents.AddRange(newEvents.Select(e => ToEntity(e, receivedAt)));

            await ProjectSessionsAsync(newEvents, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new IngestEventsResponse(newEvents.Count, events.Count - newEvents.Count);
    }

    private async Task<List<OperationEvent>> SelectNewEventsAsync(IReadOnlyList<OperationEvent> events, CancellationToken cancellationToken)
    {
        var distinctById = events
            .GroupBy(e => e.Id)
            .Select(group => group.First())
            .ToList();

        var ids = distinctById.Select(e => e.Id).ToList();

        var alreadyStored = await _dbContext.OperationEvents
            .Where(e => ids.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        return [.. distinctById.Where(e => !alreadyStored.Contains(e.Id))];
    }

    private async Task TouchStationsAsync(IReadOnlyList<OperationEvent> newEvents, DateTimeOffset receivedAt, CancellationToken cancellationToken)
    {
        var stationIds = newEvents.Select(e => e.StationId).Distinct().ToList();

        var known = await _dbContext.Stations
            .Where(s => stationIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var stationId in stationIds)
        {
            if (known.TryGetValue(stationId, out var station))
            {
                station.LastSeenAt = receivedAt;
                continue;
            }

            _dbContext.Stations.Add(new StationEntity
            {
                Id = stationId,
                FirstSeenAt = receivedAt,
                LastSeenAt = receivedAt
            });
        }
    }

    /// <summary>
    /// Aggiorna la tabella dei turni a partire dagli eventi di sessione. Gli eventi possono
    /// arrivare fuori ordine (es. fine turno prima dell'inizio): ogni evento imposta solo il suo campo.
    /// </summary>
    private async Task ProjectSessionsAsync(IReadOnlyList<OperationEvent> newEvents, CancellationToken cancellationToken)
    {
        var sessionEvents = newEvents
            .Where(e => e.Type is OperationEventType.SessionStarted or OperationEventType.SessionEnded)
            .Where(e => e.SessionId is not null && e.OperatorId is not null)
            .OrderBy(e => e.OccurredAt)
            .ToList();

        if (sessionEvents.Count == 0)
            return;

        var sessionIds = sessionEvents.Select(e => e.SessionId!.Value).Distinct().ToList();
        var operatorIds = sessionEvents.Select(e => e.OperatorId!.Value).Distinct().ToList();

        var sessions = await _dbContext.WorkSessions
            .Where(s => sessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var knownOperators = (await _dbContext.Operators
            .Where(o => operatorIds.Contains(o.Id))
            .Select(o => o.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        foreach (var sessionEvent in sessionEvents)
        {
            if (!knownOperators.Contains(sessionEvent.OperatorId!.Value))
            {
                _logger.LogWarning(
                    "Evento {EventId} di sessione per operatore sconosciuto {OperatorId}: turno non proiettato",
                    sessionEvent.Id,
                    sessionEvent.OperatorId);
                continue;
            }

            var session = GetOrAddSession(sessions, sessionEvent);
            var occurredAt = sessionEvent.OccurredAt.ToUniversalTime();

            if (sessionEvent.Type == OperationEventType.SessionStarted)
                session.StartedAt = occurredAt;
            else
                session.EndedAt = occurredAt;
        }
    }

    private WorkSessionEntity GetOrAddSession(Dictionary<Guid, WorkSessionEntity> sessions, OperationEvent sessionEvent)
    {
        var sessionId = sessionEvent.SessionId!.Value;

        if (sessions.TryGetValue(sessionId, out var existing))
            return existing;

        var session = new WorkSessionEntity
        {
            Id = sessionId,
            OperatorId = sessionEvent.OperatorId!.Value,
            StationId = sessionEvent.StationId,
            StartedAt = sessionEvent.OccurredAt.ToUniversalTime()
        };

        sessions[sessionId] = session;
        _dbContext.WorkSessions.Add(session);
        return session;
    }

    private static OperationEventEntity ToEntity(OperationEvent operationEvent, DateTimeOffset receivedAt)
        => new()
        {
            Id = operationEvent.Id,
            // Postgres (timestamptz) accetta solo offset 0: si normalizza tutto a UTC
            OccurredAt = operationEvent.OccurredAt.ToUniversalTime(),
            ReceivedAt = receivedAt,
            StationId = operationEvent.StationId,
            SessionId = operationEvent.SessionId,
            OperatorId = operationEvent.OperatorId,
            Type = operationEvent.Type,
            Barcode = operationEvent.Barcode,
            CartId = operationEvent.CartId,
            BasketId = operationEvent.BasketId,
            Details = Truncate(operationEvent.Details, OperationEventConfiguration.DetailsMaxLength)
        };

    private static string? Truncate(string? value, int maxLength)
        => value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
}
