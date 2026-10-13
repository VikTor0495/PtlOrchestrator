using PtlOrchestrator.Api.Security;
using PtlOrchestrator.Api.Services;
using PtlOrchestrator.Contracts.Operators;

namespace PtlOrchestrator.Api.Endpoints;

public static class OperatorEndpoints
{
    public static IEndpointRouteBuilder MapOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/operators/snapshot", GetSnapshotAsync)
            .RequireAuthorization(ApiKeyDefaults.StationPolicy);

        var admin = app.MapGroup("/api/operators")
            .RequireAuthorization(ApiKeyDefaults.AdminPolicy);

        admin.MapGet("/", GetAllAsync);
        admin.MapPost("/", CreateAsync);
        admin.MapPut("/{operatorId:guid}", UpdateAsync);

        return app;
    }

    private static async Task<IResult> GetSnapshotAsync(
        DateTimeOffset? since,
        IOperatorService operatorService,
        CancellationToken cancellationToken)
        => Results.Ok(await operatorService.GetSnapshotAsync(since, cancellationToken));

    private static async Task<IResult> GetAllAsync(
        IOperatorService operatorService,
        CancellationToken cancellationToken)
        => Results.Ok(await operatorService.GetAllAsync(cancellationToken));

    private static async Task<IResult> CreateAsync(
        SaveOperatorRequest request,
        IOperatorService operatorService,
        CancellationToken cancellationToken)
    {
        var result = await operatorService.CreateAsync(request, cancellationToken);

        return result.ToHttpResult(created => Results.Created($"/api/operators/{created.Id}", created));
    }

    private static async Task<IResult> UpdateAsync(
        Guid operatorId,
        SaveOperatorRequest request,
        IOperatorService operatorService,
        CancellationToken cancellationToken)
    {
        var result = await operatorService.UpdateAsync(operatorId, request, cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }
}
