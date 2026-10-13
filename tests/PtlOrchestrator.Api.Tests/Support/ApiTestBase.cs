using System.Net.Http.Json;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;

namespace PtlOrchestrator.Api.Tests.Support;

/// <summary>Base dei test d'integrazione: un'applicazione isolata per ogni test.</summary>
public abstract class ApiTestBase : IDisposable
{
    protected ApiTestBase()
    {
        Factory = new ApiFactory();
        Station = Factory.CreateClientWithKey(ApiFactory.StationKey);
        Admin = Factory.CreateClientWithKey(ApiFactory.AdminKey);
    }

    protected ApiFactory Factory { get; }

    /// <summary>Client autenticato con la chiave delle postazioni.</summary>
    protected HttpClient Station { get; }

    /// <summary>Client autenticato con la chiave amministrativa.</summary>
    protected HttpClient Admin { get; }

    protected async Task<OperatorDto> CreateOperatorAsync(
        string name = "Mario Rossi",
        string? badgeCode = "BADGE-1",
        string? pin = "4821",
        bool isActive = true)
    {
        var response = await Admin.PostAsJsonAsync("/api/operators", new SaveOperatorRequest(name, badgeCode, pin, isActive));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<OperatorDto>())!;
    }

    protected async Task<IngestEventsResponse> SendEventsAsync(params OperationEvent[] events)
    {
        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest(events));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<IngestEventsResponse>())!;
    }

    public void Dispose()
    {
        Station.Dispose();
        Admin.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
