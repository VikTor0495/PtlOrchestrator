using PtlOrchestrator.Api.Security;
using PtlOrchestrator.Api.Services;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Endpoints;

public static class OperationEventEndpoints
{
    public static IEndpointRouteBuilder MapOperationEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events", IngestAsync)
            .RequireAuthorization(ApiKeyDefaults.StationPolicy);

        return app;
    }

    private static async Task<IResult> IngestAsync(
        IngestEventsRequest request,
        IOperationEventIngestService ingestService,
        CancellationToken cancellationToken)
    {
        var problems = OperationEventValidator.Validate(request);

        if (problems.Count > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["events"] = [.. problems] });

        var response = await ingestService.IngestAsync(request.Events, cancellationToken);

        return Results.Ok(response);
    }
}
