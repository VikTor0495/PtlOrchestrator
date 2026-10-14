using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Operators;
using PtlOrchestrator.Station.Sync;

namespace PtlOrchestrator.Station.Tests.Sync;

public class HttpCentralClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request);
        }
    }

    private static (HttpCentralClient Client, StubHandler Handler) Create(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://central.test/") };
        return (new HttpCentralClient(http), handler);
    }

    private static Task<HttpResponseMessage> Status(HttpStatusCode code) => Task.FromResult(new HttpResponseMessage(code));

    private static Task<HttpResponseMessage> Json<T>(T body)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });

    private static OperationEvent SampleEvent() => new(
        Guid.NewGuid(), DateTimeOffset.UtcNow, "ST1", null, null, OperationEventType.BarcodeAccepted, Barcode: "123");

    [Fact]
    public async Task SendEvents_PostsToEventsEndpointAndReturnsResponse()
    {
        var (client, handler) = Create(_ => Json(new IngestEventsResponse(1, 0)));
        var evt = SampleEvent();

        var result = await client.SendEventsAsync([evt], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new IngestEventsResponse(1, 0), result.Value);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/events", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains(evt.Id.ToString(), handler.LastBody);
        Assert.Contains("BarcodeAccepted", handler.LastBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task SendEvents_InvalidContent_IsRejected(HttpStatusCode code)
    {
        var (client, _) = Create(_ => Status(code));

        var result = await client.SendEventsAsync([SampleEvent()], CancellationToken.None);

        Assert.Equal(CentralFailure.Rejected, result.Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task SendEvents_AuthOrServerProblems_AreTransient(HttpStatusCode code)
    {
        var (client, _) = Create(_ => Status(code));

        var result = await client.SendEventsAsync([SampleEvent()], CancellationToken.None);

        Assert.Equal(CentralFailure.Transient, result.Failure);
    }

    [Fact]
    public async Task SendEvents_NetworkError_IsTransientWithoutThrowing()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connessione rifiutata"));

        var result = await client.SendEventsAsync([SampleEvent()], CancellationToken.None);

        Assert.Equal(CentralFailure.Transient, result.Failure);
        Assert.Contains("connessione rifiutata", result.Message);
    }

    [Fact]
    public async Task SendEvents_HttpTimeout_IsTransient()
    {
        var (client, _) = Create(_ => throw new TaskCanceledException("timeout"));

        var result = await client.SendEventsAsync([SampleEvent()], CancellationToken.None);

        Assert.Equal(CentralFailure.Transient, result.Failure);
    }

    [Fact]
    public async Task SendEvents_CallerCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var (client, _) = Create(_ =>
        {
            cts.Cancel();
            throw new TaskCanceledException();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendEventsAsync([SampleEvent()], cts.Token));
    }

    [Fact]
    public async Task SendEvents_InvalidJsonBody_IsTransient()
    {
        var (client, _) = Create(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("non json", Encoding.UTF8, "application/json") }));

        var result = await client.SendEventsAsync([SampleEvent()], CancellationToken.None);

        Assert.Equal(CentralFailure.Transient, result.Failure);
    }

    [Fact]
    public async Task GetOperatorsSnapshot_WithoutSince_RequestsFullSnapshot()
    {
        var (client, handler) = Create(_ => Json(new OperatorsSnapshotResponse([], DateTimeOffset.UtcNow)));

        var result = await client.GetOperatorsSnapshotAsync(null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/api/operators/snapshot", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetOperatorsSnapshot_WithSince_SendsEscapedIsoTimestamp()
    {
        var since = new DateTimeOffset(2026, 3, 1, 10, 30, 0, TimeSpan.FromHours(1));
        var (client, handler) = Create(_ => Json(new OperatorsSnapshotResponse([], since)));

        await client.GetOperatorsSnapshotAsync(since, CancellationToken.None);

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.StartsWith("?since=", query);
        var parsed = System.Web.HttpUtility.ParseQueryString(query)["since"];
        Assert.Equal(since, DateTimeOffset.Parse(parsed!, CultureInfo.InvariantCulture));
    }
}
