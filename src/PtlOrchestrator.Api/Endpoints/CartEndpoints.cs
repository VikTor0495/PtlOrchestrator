using PtlOrchestrator.Api.Security;
using PtlOrchestrator.Api.Services;
using PtlOrchestrator.Contracts.Carts;

namespace PtlOrchestrator.Api.Endpoints;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        // le postazioni chiedono e rilasciano i carrelli
        var stations = app.MapGroup("/api/carts")
            .RequireAuthorization(ApiKeyDefaults.StationPolicy);

        stations.MapGet("/catalog", async (ICartService cartService, CancellationToken cancellationToken)
                => Results.Ok(await cartService.GetCatalogAsync(cancellationToken)));
        stations.MapPost("/{cartCode}/checkin", CheckInAsync);
        stations.MapPost("/{cartCode}/release", ReleaseAsync);

        // il catalogo dei carrelli è gestito dall'amministrazione
        var admin = app.MapGroup("/api/carts")
            .RequireAuthorization(ApiKeyDefaults.AdminPolicy);

        admin.MapGet("/", GetAllAsync);
        admin.MapPut("/{cartCode}", SaveAsync);

        return app;
    }

    private static async Task<IResult> CheckInAsync(
        string cartCode,
        CartCheckInRequest request,
        ICartService cartService,
        CancellationToken cancellationToken)
    {
        var result = await cartService.CheckInAsync(cartCode, request, cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> ReleaseAsync(
        string cartCode,
        CartReleaseRequest request,
        ICartService cartService,
        CancellationToken cancellationToken)
    {
        var result = await cartService.ReleaseAsync(cartCode, request, cancellationToken);

        return result.ToHttpResult(_ => Results.NoContent());
    }

    private static async Task<IResult> GetAllAsync(
        ICartService cartService,
        CancellationToken cancellationToken)
        => Results.Ok(await cartService.GetAllAsync(cancellationToken));

    private static async Task<IResult> SaveAsync(
        string cartCode,
        SaveCartRequest request,
        ICartService cartService,
        CancellationToken cancellationToken)
    {
        var result = await cartService.SaveAsync(cartCode, request, cancellationToken);

        return result.ToHttpResult(Results.Ok);
    }
}
