using PtlOrchestrator.Api.Security;
using PtlOrchestrator.Api.Services;

namespace PtlOrchestrator.Api.Endpoints;

public static class SessionEndpoints
{
    private const int DefaultTake = 100;
    private const int MaxTake = 500;

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/sessions")
            .RequireAuthorization(ApiKeyDefaults.AdminPolicy);

        sessions.MapGet("/", ListAsync);
        sessions.MapGet("/{sessionId:guid}/events", GetEventsAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? operatorId,
        int? take,
        ISessionReportService reportService,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take ?? DefaultTake, 1, MaxTake);

        var sessions = await reportService.ListAsync(new SessionQuery(from, to, operatorId, limit), cancellationToken);

        return Results.Ok(sessions);
    }

    private static async Task<IResult> GetEventsAsync(
        Guid sessionId,
        ISessionReportService reportService,
        CancellationToken cancellationToken)
    {
        var events = await reportService.GetEventsAsync(sessionId, cancellationToken);

        return events is null
            ? Results.Problem(detail: "Turno non trovato", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(events);
    }
}
