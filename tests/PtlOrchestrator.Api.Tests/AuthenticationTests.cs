using System.Net;
using System.Net.Http.Json;
using PtlOrchestrator.Api.Tests.Support;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Tests;

public class AuthenticationTests : ApiTestBase
{
    [Fact]
    public async Task Health_IsAvailableWithoutCredentials()
    {
        using var anonymous = Factory.CreateClientWithKey(null);

        var response = await anonymous.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Events_WithoutApiKey_AreUnauthorized()
    {
        using var anonymous = Factory.CreateClientWithKey(null);

        var response = await anonymous.PostAsJsonAsync("/api/events", new IngestEventsRequest([EventFactory.Create()]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Events_WithWrongApiKey_AreUnauthorized()
    {
        using var intruder = Factory.CreateClientWithKey("chiave-sbagliata-0123456789");

        var response = await intruder.PostAsJsonAsync("/api/events", new IngestEventsRequest([EventFactory.Create()]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StationKey_CanSendEvents()
    {
        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest([EventFactory.Create()]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StationKey_CanReadTheOperatorsSnapshot()
    {
        var response = await Station.GetAsync("/api/operators/snapshot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/operators")]
    [InlineData("/api/sessions")]
    public async Task StationKey_CannotUseAdminEndpoints(string url)
    {
        var response = await Station.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminKey_CanAlsoUseStationEndpoints()
    {
        var response = await Admin.GetAsync("/api/operators/snapshot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
