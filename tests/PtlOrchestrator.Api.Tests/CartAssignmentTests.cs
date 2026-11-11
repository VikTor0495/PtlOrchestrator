using System.Net;
using System.Net.Http.Json;
using PtlOrchestrator.Api.Tests.Support;
using PtlOrchestrator.Contracts.Carts;

namespace PtlOrchestrator.Api.Tests;

public class CartAssignmentTests : ApiTestBase
{
    private async Task<HttpResponseMessage> SaveCartAsync(string code, string ip = "192.168.1.9", int port = 15003, params string[] baskets)
        => await Admin.PutAsJsonAsync($"/api/carts/{code}",
            new SaveCartRequest(ip, port, baskets.Length == 0 ? ["0001", "0002"] : baskets));

    private async Task<HttpResponseMessage> CheckInAsync(string cartCode, string stationId)
        => await Station.PostAsJsonAsync($"/api/carts/{cartCode}/checkin", new CartCheckInRequest(stationId));

    private async Task<HttpResponseMessage> ReleaseAsync(string cartCode, string stationId)
        => await Station.PostAsJsonAsync($"/api/carts/{cartCode}/release", new CartReleaseRequest(stationId));

    [Fact]
    public async Task CheckIn_ReturnsTheControllerEndpointAndBasketsRegisteredOnTheServer()
    {
        (await SaveCartAsync("1", "192.168.1.20", 15010)).EnsureSuccessStatusCode();

        var response = await CheckInAsync("1", "ST-01");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CartCheckInResponse>();
        Assert.Equal("1", body!.CartCode);
        Assert.Equal("192.168.1.20", body.ControllerIp);
        Assert.Equal(15010, body.ControllerPort);
        Assert.Equal(["0001", "0002"], body.BasketIds);
    }

    [Fact]
    public async Task CheckIn_OfACartOccupiedByAnotherStation_IsRejectedWithConflict()
    {
        (await SaveCartAsync("1")).EnsureSuccessStatusCode();
        (await CheckInAsync("1", "ST-01")).EnsureSuccessStatusCode();

        var response = await CheckInAsync("1", "ST-02");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("ST-01", problem);
    }

    [Fact]
    public async Task CheckIn_RepeatedByTheSameStation_IsIdempotent()
    {
        (await SaveCartAsync("1")).EnsureSuccessStatusCode();
        (await CheckInAsync("1", "ST-01")).EnsureSuccessStatusCode();

        var response = await CheckInAsync("1", "ST-01");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Release_FreesTheCartForAnotherStation()
    {
        (await SaveCartAsync("1")).EnsureSuccessStatusCode();
        (await CheckInAsync("1", "ST-01")).EnsureSuccessStatusCode();
        (await ReleaseAsync("1", "ST-01")).EnsureSuccessStatusCode();

        var response = await CheckInAsync("1", "ST-02");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Release_ByAStationThatDoesNotOwnTheCart_IsRejected()
    {
        (await SaveCartAsync("1")).EnsureSuccessStatusCode();
        (await CheckInAsync("1", "ST-01")).EnsureSuccessStatusCode();

        var response = await ReleaseAsync("1", "ST-02");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CheckIn_OfUnknownOrInactiveCart_IsNotFound()
    {
        var unknown = await CheckInAsync("99", "ST-01");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        await Admin.PutAsJsonAsync("/api/carts/2", new SaveCartRequest("192.168.1.9", 15003, ["0003"], IsActive: false));
        var inactive = await CheckInAsync("2", "ST-01");
        Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);
    }

    [Fact]
    public async Task Catalogue_RejectsABasketAlreadyOwnedByAnotherCart()
    {
        (await SaveCartAsync("1", baskets: ["0001"])).EnsureSuccessStatusCode();

        var response = await SaveCartAsync("2", baskets: ["0001"]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("999.1.1.1")]
    public async Task Catalogue_RejectsAnInvalidControllerAddress(string ip)
    {
        var response = await SaveCartAsync("1", ip);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Catalogue_RejectsAnInvalidPort()
    {
        var response = await SaveCartAsync("1", port: 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Catalogue_ListsCartsWithTheStationThatOccupiesThem()
    {
        (await SaveCartAsync("1")).EnsureSuccessStatusCode();
        (await CheckInAsync("1", "ST-01")).EnsureSuccessStatusCode();

        var carts = await Admin.GetFromJsonAsync<List<CartDto>>("/api/carts");

        var cart = Assert.Single(carts!);
        Assert.Equal("ST-01", cart.OccupiedByStationId);
    }

    [Fact]
    public async Task StationCatalogue_ListsOnlyActiveCartCodes()
    {
        (await SaveCartAsync("1")).EnsureSuccessStatusCode();
        (await SaveCartAsync("2", baskets: ["0003"])).EnsureSuccessStatusCode();
        await Admin.PutAsJsonAsync("/api/carts/3", new SaveCartRequest("192.168.1.9", 15003, ["0009"], IsActive: false));

        var codes = await Station.GetFromJsonAsync<List<string>>("/api/carts/catalog");

        Assert.Equal(["1", "2"], codes);
    }
}
