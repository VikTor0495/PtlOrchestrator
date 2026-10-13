using PtlOrchestrator.Api.Persistence.Configurations;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Services;

public static class OperationEventValidator
{
    public const int MaxBatchSize = 1000;

    /// <summary>Restituisce i problemi riscontrati, vuoto se il lotto è valido.</summary>
    public static IReadOnlyList<string> Validate(IngestEventsRequest? request)
    {
        if (request?.Events is null || request.Events.Count == 0)
            return ["Il lotto non contiene eventi"];

        if (request.Events.Count > MaxBatchSize)
            return [$"Un lotto non può superare {MaxBatchSize} eventi"];

        var problems = new List<string>();

        for (var i = 0; i < request.Events.Count; i++)
        {
            var problem = FindProblem(request.Events[i]);
            if (problem is not null)
                problems.Add($"Evento #{i}: {problem}");
        }

        return problems;
    }

    private static string? FindProblem(OperationEvent? operationEvent)
    {
        if (operationEvent is null)
            return "evento nullo";

        if (operationEvent.Id == Guid.Empty)
            return "Id mancante";

        if (string.IsNullOrWhiteSpace(operationEvent.StationId))
            return "StationId mancante";

        if (operationEvent.StationId.Length > StationConfiguration.IdMaxLength)
            return $"StationId oltre {StationConfiguration.IdMaxLength} caratteri";

        if (!Enum.IsDefined(operationEvent.Type))
            return "tipo evento sconosciuto";

        // oltre la lunghezza della colonna Postgres rifiuterebbe l'intero lotto, non solo questo evento
        if (operationEvent.Barcode is { Length: > OperationEventConfiguration.BarcodeMaxLength })
            return $"Barcode oltre {OperationEventConfiguration.BarcodeMaxLength} caratteri";

        if (operationEvent.CartId is { Length: > OperationEventConfiguration.LocationMaxLength })
            return $"CartId oltre {OperationEventConfiguration.LocationMaxLength} caratteri";

        if (operationEvent.BasketId is { Length: > OperationEventConfiguration.LocationMaxLength })
            return $"BasketId oltre {OperationEventConfiguration.LocationMaxLength} caratteri";

        return null;
    }
}
