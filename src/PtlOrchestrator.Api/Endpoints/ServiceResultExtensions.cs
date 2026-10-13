using PtlOrchestrator.Api.Services;

namespace PtlOrchestrator.Api.Endpoints;

internal static class ServiceResultExtensions
{
    public static IResult ToHttpResult<T>(this ServiceResult<T> result, Func<T, IResult> onSuccess)
    {
        if (result.Success)
            return onSuccess(result.Value!);

        var statusCode = result.Error switch
        {
            ServiceError.NotFound => StatusCodes.Status404NotFound,
            ServiceError.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(detail: result.Message, statusCode: statusCode);
    }
}
