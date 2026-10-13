using Microsoft.EntityFrameworkCore;
using PtlOrchestrator.Api.Persistence;
using PtlOrchestrator.Api.Persistence.Entities;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Services;

public sealed class SessionReportService(PtlDbContext dbContext) : ISessionReportService
{
    private readonly PtlDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<WorkSessionSummary>> ListAsync(SessionQuery query, CancellationToken cancellationToken)
    {
        var sessions = await FilterSessions(query)
            .OrderByDescending(s => s.StartedAt)
            .Take(query.Take)
            .Select(s => new { Session = s, OperatorName = s.Operator!.Name })
            .ToListAsync(cancellationToken);

        var counters = await CountEventsBySessionAsync([.. sessions.Select(s => s.Session.Id)], cancellationToken);

        return [.. sessions.Select(s => ToSummary(s.Session, s.OperatorName, counters))];
    }

    public async Task<IReadOnlyList<OperationEvent>?> GetEventsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var sessionExists = await _dbContext.WorkSessions.AnyAsync(s => s.Id == sessionId, cancellationToken);
        if (!sessionExists)
            return null;

        var events = await _dbContext.OperationEvents
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);

        return [.. events.Select(ToContract)];
    }

    private IQueryable<WorkSessionEntity> FilterSessions(SessionQuery query)
    {
        var sessions = _dbContext.WorkSessions.AsNoTracking();

        if (query.From is not null)
        {
            var from = query.From.Value.ToUniversalTime();
            sessions = sessions.Where(s => s.StartedAt >= from);
        }

        if (query.To is not null)
        {
            var to = query.To.Value.ToUniversalTime();
            sessions = sessions.Where(s => s.StartedAt < to);
        }

        if (query.OperatorId is not null)
        {
            var operatorId = query.OperatorId.Value;
            sessions = sessions.Where(s => s.OperatorId == operatorId);
        }

        return sessions;
    }

    private async Task<Dictionary<(Guid SessionId, OperationEventType Type), int>> CountEventsBySessionAsync(
        List<Guid> sessionIds,
        CancellationToken cancellationToken)
    {
        var grouped = await _dbContext.OperationEvents
            .AsNoTracking()
            .Where(e => e.SessionId != null && sessionIds.Contains(e.SessionId.Value))
            .GroupBy(e => new { SessionId = e.SessionId!.Value, e.Type })
            .Select(g => new { g.Key.SessionId, g.Key.Type, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(g => (g.SessionId, g.Type), g => g.Count);
    }

    private static WorkSessionSummary ToSummary(
        WorkSessionEntity session,
        string operatorName,
        Dictionary<(Guid SessionId, OperationEventType Type), int> counters)
    {
        int CountOf(OperationEventType type)
            => counters.GetValueOrDefault((session.Id, type));

        return new WorkSessionSummary(
            session.Id,
            session.OperatorId,
            operatorName,
            session.StationId,
            session.StartedAt,
            session.EndedAt,
            ProductsAccepted: CountOf(OperationEventType.BasketConfirmed),
            ProductsRejected: CountOf(OperationEventType.BarcodeRejected),
            WrongModulePresses: CountOf(OperationEventType.WrongModulePressed),
            Timeouts: CountOf(OperationEventType.OperationTimedOut),
            Faults: CountOf(OperationEventType.PtlFault));
    }

    private static OperationEvent ToContract(OperationEventEntity entity)
        => new(
            entity.Id,
            entity.OccurredAt,
            entity.StationId,
            entity.SessionId,
            entity.OperatorId,
            entity.Type,
            entity.Barcode,
            entity.CartId,
            entity.BasketId,
            entity.Details);
}
