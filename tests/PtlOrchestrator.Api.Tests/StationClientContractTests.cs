using System.Net.Http.Json;
using PtlOrchestrator.Api.Tests.Support;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Station.Sync;

namespace PtlOrchestrator.Api.Tests;

/// <summary>Il client reale della postazione contro l'Api reale (database in memoria).</summary>
public class StationClientContractTests : ApiTestBase
{
    private HttpCentralClient CreateClient(string? apiKey = ApiFactory.StationKey)
        => new(Factory.CreateClientWithKey(apiKey));

    [Fact]
    public async Task SendEvents_AreStoredAndResendingThemIsHarmless()
    {
        var client = CreateClient();
        var events = new[] { EventFactory.Create(barcode: "8000123"), EventFactory.Create(OperationEventType.PtlFault) };

        var first = await client.SendEventsAsync(events, CancellationToken.None);
        var second = await client.SendEventsAsync(events, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(new IngestEventsResponse(2, 0), first.Value);
        Assert.Equal(new IngestEventsResponse(0, 2), second.Value);
    }

    [Fact]
    public async Task SendEvents_InvalidEvent_IsRejectedForGood()
    {
        var client = CreateClient();

        var result = await client.SendEventsAsync([EventFactory.Create(stationId: "")], CancellationToken.None);

        Assert.Equal(CentralFailure.Rejected, result.Failure);
    }

    [Fact]
    public async Task SendEvents_WithWrongKey_IsTransientSoEventsAreKept()
    {
        var client = CreateClient("chiave-sbagliata-0123456789");

        var result = await client.SendEventsAsync([EventFactory.Create()], CancellationToken.None);

        Assert.Equal(CentralFailure.Transient, result.Failure);
    }

    [Fact]
    public async Task Snapshot_ThenIncrementalSnapshot_ReturnsOnlyWhatChanged()
    {
        var client = CreateClient();
        var mario = await CreateOperatorAsync(name: "Mario", badgeCode: "B-1", pin: "4821");

        var full = await client.GetOperatorsSnapshotAsync(null, CancellationToken.None);

        Assert.True(full.IsSuccess);
        var fromServer = Assert.Single(full.Value!.Operators);
        Assert.Equal(mario.Id, fromServer.Id);
        Assert.NotNull(fromServer.PinHash);
        Assert.DoesNotContain("4821", fromServer.PinHash);

        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var nothingNew = await client.GetOperatorsSnapshotAsync(full.Value.ServerTime, CancellationToken.None);
        Assert.Empty(nothingNew.Value!.Operators);

        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var update = await Admin.PutAsJsonAsync($"/api/operators/{mario.Id}", new SaveOperatorRequest("Mario Bianchi", "B-1", null, true));
        update.EnsureSuccessStatusCode();

        var changed = await client.GetOperatorsSnapshotAsync(nothingNew.Value.ServerTime, CancellationToken.None);
        Assert.Equal("Mario Bianchi", Assert.Single(changed.Value!.Operators).Name);
    }
}
