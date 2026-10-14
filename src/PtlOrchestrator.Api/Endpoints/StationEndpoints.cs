using PtlOrchestrator.Api.Security;
using PtlOrchestrator.Api.Services;

namespace PtlOrchestrator.Api.Endpoints;

public static class StationEndpoints
{
    public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stations", async (IStationService stationService, CancellationToken cancellationToken)
                => Results.Ok(await stationService.GetAllAsync(cancellationToken)))
            .RequireAuthorization(ApiKeyDefaults.AdminPolicy);

        return app;
    }
}
