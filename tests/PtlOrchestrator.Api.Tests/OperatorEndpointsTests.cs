using System.Net;
using System.Net.Http.Json;
using PtlOrchestrator.Api.Tests.Support;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Contracts.Security;

namespace PtlOrchestrator.Api.Tests;

public class OperatorEndpointsTests : ApiTestBase
{
    [Fact]
    public async Task Create_ReturnsCreatedOperatorWithLocation()
    {
        var response = await Admin.PostAsJsonAsync(
            "/api/operators",
            new SaveOperatorRequest("Mario Rossi", "BADGE-1", "4821"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<OperatorDto>())!;
        Assert.Equal($"/api/operators/{created.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal("Mario Rossi", created.Name);
        Assert.Equal("BADGE-1", created.BadgeCode);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task Create_StoresOnlyAHashOfThePin()
    {
        var created = await CreateOperatorAsync(pin: "4821");

        Assert.NotEqual("4821", created.PinHash);
        Assert.True(PinHasher.Verify("4821", created.PinHash));
        Assert.False(PinHasher.Verify("0000", created.PinHash));
    }

    [Fact]
    public async Task Create_TrimsNameAndBadge()
    {
        var created = await CreateOperatorAsync(name: "  Mario Rossi ", badgeCode: " BADGE-1 ");

        Assert.Equal("Mario Rossi", created.Name);
        Assert.Equal("BADGE-1", created.BadgeCode);
    }

    [Fact]
    public async Task Create_WithOnlyAPin_IsAllowed()
    {
        var response = await Admin.PostAsJsonAsync("/api/operators", new SaveOperatorRequest("Luisa", null, "1234"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("", "BADGE-1", "4821")]
    [InlineData("   ", "BADGE-1", "4821")]
    [InlineData("Mario", "BADGE-1", "12")]
    [InlineData("Mario", "BADGE-1", "123456789")]
    [InlineData("Mario", "BADGE-1", "12ab")]
    [InlineData("Mario", null, null)]
    [InlineData("Mario", "  ", null)]
    public async Task Create_WithInvalidData_IsRejected(string name, string? badge, string? pin)
    {
        var response = await Admin.PostAsJsonAsync("/api/operators", new SaveOperatorRequest(name, badge, pin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTooLongName_IsRejected()
    {
        var response = await Admin.PostAsJsonAsync(
            "/api/operators",
            new SaveOperatorRequest(new string('a', 101), "BADGE-1", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithABadgeAlreadyInUse_IsAConflict()
    {
        await CreateOperatorAsync(name: "Mario", badgeCode: "BADGE-1");

        var response = await Admin.PostAsJsonAsync("/api/operators", new SaveOperatorRequest("Luisa", "BADGE-1", "1234"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_TwoOperatorsWithoutBadge_IsAllowed()
    {
        await Admin.PostAsJsonAsync("/api/operators", new SaveOperatorRequest("Mario", null, "1234"));

        var response = await Admin.PostAsJsonAsync("/api/operators", new SaveOperatorRequest("Luisa", null, "5678"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesNameBadgeAndActiveFlag()
    {
        var created = await CreateOperatorAsync();

        var response = await Admin.PutAsJsonAsync(
            $"/api/operators/{created.Id}",
            new SaveOperatorRequest("Mario Bianchi", "BADGE-9", null, IsActive: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<OperatorDto>())!;
        Assert.Equal("Mario Bianchi", updated.Name);
        Assert.Equal("BADGE-9", updated.BadgeCode);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Update_WithoutPin_KeepsTheExistingPin()
    {
        var created = await CreateOperatorAsync(pin: "4821");

        var response = await Admin.PutAsJsonAsync(
            $"/api/operators/{created.Id}",
            new SaveOperatorRequest("Mario", "BADGE-1", Pin: null));

        var updated = (await response.Content.ReadFromJsonAsync<OperatorDto>())!;
        Assert.Equal(created.PinHash, updated.PinHash);
    }

    [Fact]
    public async Task Update_WithAPin_ReplacesIt()
    {
        var created = await CreateOperatorAsync(pin: "4821");

        var response = await Admin.PutAsJsonAsync(
            $"/api/operators/{created.Id}",
            new SaveOperatorRequest("Mario", "BADGE-1", "9999"));

        var updated = (await response.Content.ReadFromJsonAsync<OperatorDto>())!;
        Assert.True(PinHasher.Verify("9999", updated.PinHash));
        Assert.False(PinHasher.Verify("4821", updated.PinHash));
    }

    [Fact]
    public async Task Update_MovesTheUpdatedAtForward()
    {
        var created = await CreateOperatorAsync();
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));

        var response = await Admin.PutAsJsonAsync(
            $"/api/operators/{created.Id}",
            new SaveOperatorRequest("Mario", "BADGE-1", null));

        var updated = (await response.Content.ReadFromJsonAsync<OperatorDto>())!;
        Assert.Equal(created.UpdatedAt.AddMinutes(5), updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_OfUnknownOperator_IsNotFound()
    {
        var response = await Admin.PutAsJsonAsync(
            $"/api/operators/{Guid.NewGuid()}",
            new SaveOperatorRequest("Mario", "BADGE-1", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ToABadgeOfAnotherOperator_IsAConflict()
    {
        await CreateOperatorAsync(name: "Mario", badgeCode: "BADGE-1");
        var luisa = await CreateOperatorAsync(name: "Luisa", badgeCode: "BADGE-2");

        var response = await Admin.PutAsJsonAsync(
            $"/api/operators/{luisa.Id}",
            new SaveOperatorRequest("Luisa", "BADGE-1", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_KeepingItsOwnBadge_IsNotAConflict()
    {
        var mario = await CreateOperatorAsync(badgeCode: "BADGE-1");

        var response = await Admin.PutAsJsonAsync(
            $"/api/operators/{mario.Id}",
            new SaveOperatorRequest("Mario Bianchi", "BADGE-1", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_RemovingTheOnlyCredential_IsRejected()
    {
        var created = (await (await Admin.PostAsJsonAsync("/api/operators", new SaveOperatorRequest("Mario", "BADGE-1", null)))
            .Content.ReadFromJsonAsync<OperatorDto>())!;

        var response = await Admin.PutAsJsonAsync(
            $"/api/operators/{created.Id}",
            new SaveOperatorRequest("Mario", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsAllOperatorsSortedByName()
    {
        await CreateOperatorAsync(name: "Zeno", badgeCode: "B-3");
        await CreateOperatorAsync(name: "Anna", badgeCode: "B-1", isActive: false);
        await CreateOperatorAsync(name: "Marco", badgeCode: "B-2");

        var operators = (await Admin.GetFromJsonAsync<List<OperatorDto>>("/api/operators"))!;

        Assert.Equal(["Anna", "Marco", "Zeno"], operators.Select(o => o.Name));
    }

    [Fact]
    public async Task Snapshot_IncludesInactiveOperatorsSoStationsCanDeactivateThem()
    {
        await CreateOperatorAsync(name: "Mario", badgeCode: "B-1");
        await CreateOperatorAsync(name: "Luisa", badgeCode: "B-2", isActive: false);

        var snapshot = (await Station.GetFromJsonAsync<OperatorsSnapshotResponse>("/api/operators/snapshot"))!;

        Assert.Equal(2, snapshot.Operators.Count);
        Assert.Contains(snapshot.Operators, o => o.Name == "Luisa" && !o.IsActive);
    }

    [Fact]
    public async Task Snapshot_ReportsTheServerTime()
    {
        var snapshot = (await Station.GetFromJsonAsync<OperatorsSnapshotResponse>("/api/operators/snapshot"))!;

        Assert.Equal(Factory.Clock.GetUtcNow(), snapshot.ServerTime);
    }

    [Fact]
    public async Task Snapshot_WithSince_ReturnsOnlyOperatorsChangedAfterThatMoment()
    {
        await CreateOperatorAsync(name: "Vecchio", badgeCode: "B-1");
        Factory.Clock.Advance(TimeSpan.FromHours(1));
        var since = Factory.Clock.GetUtcNow();
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await CreateOperatorAsync(name: "Nuovo", badgeCode: "B-2");

        var url = $"/api/operators/snapshot?since={Uri.EscapeDataString(since.ToString("O"))}";
        var snapshot = (await Station.GetFromJsonAsync<OperatorsSnapshotResponse>(url))!;

        Assert.Equal(["Nuovo"], snapshot.Operators.Select(o => o.Name));
    }

    [Fact]
    public async Task StationKey_CannotCreateOperators()
    {
        var response = await Station.PostAsJsonAsync("/api/operators", new SaveOperatorRequest("Mario", "B-1", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
