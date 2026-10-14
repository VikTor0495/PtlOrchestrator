using System.Net;
using System.Net.Http.Json;
using System.Text;
using PtlOrchestrator.Api.Services;
using PtlOrchestrator.Api.Tests.Support;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Contracts.Stations;

namespace PtlOrchestrator.Api.Tests;

public class SessionCsvWriterTests
{
    private static WorkSessionSummary Summary(string operatorName = "Mario Rossi", DateTimeOffset? ended = null)
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.NewGuid(), operatorName, "ST-01",
            new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero), ended, 5, 2, 1, 0, 3);

    [Fact]
    public void Write_ProducesHeaderAndOneRowPerSession()
    {
        var csv = SessionCsvWriter.Write([Summary(ended: new DateTimeOffset(2026, 10, 13, 9, 30, 0, TimeSpan.Zero))]);

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Turno;Operatore;Postazione", lines[0]);
        Assert.Equal(
            "11111111-1111-1111-1111-111111111111;Mario Rossi;ST-01;2026-10-13 08:00:00;2026-10-13 09:30:00;90;5;2;1;0;3",
            lines[1]);
    }

    [Fact]
    public void Write_OpenSession_HasEmptyEndAndDuration()
    {
        var row = SessionCsvWriter.Write([Summary()]).Split("\r\n")[1].Split(';');

        Assert.Equal(string.Empty, row[4]);
        Assert.Equal(string.Empty, row[5]);
    }

    [Fact]
    public void Write_QuotesSeparatorsAndQuotes()
    {
        var csv = SessionCsvWriter.Write([Summary("Rossi; \"Mario\"")]);

        Assert.Contains("\"Rossi; \"\"Mario\"\"\"", csv);
    }

    [Theory]
    [InlineData("=SOMMA(A1:A2)")]
    [InlineData("+39333")]
    [InlineData("@cmd")]
    public void Write_NeutralizesSpreadsheetFormulas(string name)
    {
        var csv = SessionCsvWriter.Write([Summary(name)]);

        Assert.Contains(";'" + name + ";", csv);
    }
}

public class ExportAndStationEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Export_ReturnsCsvWithUtf8BomAndTheSessions()
    {
        var mario = await CreateOperatorAsync(name: "Mario Rossi");
        var sessionId = Guid.NewGuid();
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: sessionId, operatorId: mario.Id),
            EventFactory.Create(OperationEventType.BarcodeAccepted, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: 1));

        var response = await Admin.GetAsync("/api/sessions/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("turni-", response.Content.Headers.ContentDisposition?.FileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("Mario Rossi", text);
        Assert.Contains(sessionId.ToString(), text);
    }

    [Fact]
    public async Task Export_WithStationKey_IsForbidden()
    {
        var response = await Station.GetAsync("/api/sessions/export");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Stations_ListsStationsSeenThroughEvents()
    {
        await SendEventsAsync(
            EventFactory.Create(stationId: "ST-02"),
            EventFactory.Create(stationId: "ST-01"));

        var stations = (await Admin.GetFromJsonAsync<List<StationInfo>>("/api/stations"))!;

        Assert.Equal(["ST-01", "ST-02"], stations.Select(s => s.Id));
    }

    [Fact]
    public async Task Stations_WithStationKey_IsForbidden()
    {
        var response = await Station.GetAsync("/api/stations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
