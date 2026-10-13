using System.Net;
using System.Net.Http.Json;
using PtlOrchestrator.Api.Tests.Support;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Tests;

public class SessionReportTests : ApiTestBase
{
    [Fact]
    public async Task List_SummarizesTheOperationsOfEachSession()
    {
        var mario = await CreateOperatorAsync(name: "Mario Rossi");
        var sessionId = Guid.NewGuid();

        OperationEvent InSession(OperationEventType type, int minute)
            => EventFactory.Create(type, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: minute);

        await SendEventsAsync(
            InSession(OperationEventType.SessionStarted, 0),
            InSession(OperationEventType.BarcodeAccepted, 1),
            InSession(OperationEventType.BasketConfirmed, 2),
            InSession(OperationEventType.BarcodeAccepted, 3),
            InSession(OperationEventType.WrongModulePressed, 4),
            InSession(OperationEventType.BasketConfirmed, 5),
            InSession(OperationEventType.BarcodeRejected, 6),
            InSession(OperationEventType.BarcodeAccepted, 7),
            InSession(OperationEventType.OperationTimedOut, 8),
            InSession(OperationEventType.PtlFault, 9),
            InSession(OperationEventType.SessionEnded, 60));

        var sessions = (await Admin.GetFromJsonAsync<List<WorkSessionSummary>>("/api/sessions"))!;

        var summary = Assert.Single(sessions);
        Assert.Equal(sessionId, summary.Id);
        Assert.Equal("Mario Rossi", summary.OperatorName);
        Assert.Equal("ST-01", summary.StationId);
        Assert.Equal(EventFactory.BaseTime, summary.StartedAt);
        Assert.Equal(EventFactory.BaseTime.AddMinutes(60), summary.EndedAt);
        Assert.Equal(2, summary.ProductsAccepted);
        Assert.Equal(1, summary.ProductsRejected);
        Assert.Equal(1, summary.WrongModulePresses);
        Assert.Equal(1, summary.Timeouts);
        Assert.Equal(1, summary.Faults);
    }

    [Fact]
    public async Task List_KeepsTheCountersOfDifferentSessionsSeparate()
    {
        var mario = await CreateOperatorAsync(name: "Mario", badgeCode: "B-1");
        var luisa = await CreateOperatorAsync(name: "Luisa", badgeCode: "B-2");
        var marioSession = Guid.NewGuid();
        var luisaSession = Guid.NewGuid();

        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: marioSession, operatorId: mario.Id),
            EventFactory.Create(OperationEventType.BasketConfirmed, sessionId: marioSession, operatorId: mario.Id),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: luisaSession, operatorId: luisa.Id, minutesFromBase: 1),
            EventFactory.Create(OperationEventType.BasketConfirmed, sessionId: luisaSession, operatorId: luisa.Id),
            EventFactory.Create(OperationEventType.BasketConfirmed, sessionId: luisaSession, operatorId: luisa.Id));

        var sessions = (await Admin.GetFromJsonAsync<List<WorkSessionSummary>>("/api/sessions"))!;

        Assert.Equal(1, sessions.Single(s => s.OperatorName == "Mario").ProductsAccepted);
        Assert.Equal(2, sessions.Single(s => s.OperatorName == "Luisa").ProductsAccepted);
    }

    [Fact]
    public async Task List_ReturnsTheMostRecentSessionsFirst()
    {
        var mario = await CreateOperatorAsync();
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 0),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 120),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 60));

        var sessions = (await Admin.GetFromJsonAsync<List<WorkSessionSummary>>("/api/sessions"))!;

        Assert.Equal(
            [EventFactory.BaseTime.AddMinutes(120), EventFactory.BaseTime.AddMinutes(60), EventFactory.BaseTime],
            sessions.Select(s => s.StartedAt));
    }

    [Fact]
    public async Task List_CanBeFilteredByOperator()
    {
        var mario = await CreateOperatorAsync(name: "Mario", badgeCode: "B-1");
        var luisa = await CreateOperatorAsync(name: "Luisa", badgeCode: "B-2");
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: luisa.Id));

        var sessions = (await Admin.GetFromJsonAsync<List<WorkSessionSummary>>($"/api/sessions?operatorId={luisa.Id}"))!;

        Assert.Equal(["Luisa"], sessions.Select(s => s.OperatorName));
    }

    [Fact]
    public async Task List_CanBeFilteredByDateRange()
    {
        var mario = await CreateOperatorAsync();
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 0),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 60),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 120));

        var from = Uri.EscapeDataString(EventFactory.BaseTime.AddMinutes(30).ToString("O"));
        var to = Uri.EscapeDataString(EventFactory.BaseTime.AddMinutes(90).ToString("O"));
        var sessions = (await Admin.GetFromJsonAsync<List<WorkSessionSummary>>($"/api/sessions?from={from}&to={to}"))!;

        Assert.Equal([EventFactory.BaseTime.AddMinutes(60)], sessions.Select(s => s.StartedAt));
    }

    [Fact]
    public async Task List_HonoursTheTakeLimit()
    {
        var mario = await CreateOperatorAsync();
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 0),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 1),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 2));

        var sessions = (await Admin.GetFromJsonAsync<List<WorkSessionSummary>>("/api/sessions?take=2"))!;

        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public async Task List_WithoutSessions_ReturnsAnEmptyList()
    {
        var sessions = (await Admin.GetFromJsonAsync<List<WorkSessionSummary>>("/api/sessions"))!;

        Assert.Empty(sessions);
    }

    [Fact]
    public async Task Events_OfASession_AreReturnedInChronologicalOrder()
    {
        var mario = await CreateOperatorAsync();
        var sessionId = Guid.NewGuid();
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.BasketConfirmed, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: 5),
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: 0),
            EventFactory.Create(OperationEventType.BarcodeAccepted, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: 3),
            EventFactory.Create(OperationEventType.BarcodeAccepted, sessionId: Guid.NewGuid(), operatorId: mario.Id, minutesFromBase: 4));

        var events = (await Admin.GetFromJsonAsync<List<OperationEvent>>($"/api/sessions/{sessionId}/events"))!;

        Assert.Equal(
            [OperationEventType.SessionStarted, OperationEventType.BarcodeAccepted, OperationEventType.BasketConfirmed],
            events.Select(e => e.Type));
    }

    [Fact]
    public async Task Events_OfAnUnknownSession_AreNotFound()
    {
        var response = await Admin.GetAsync($"/api/sessions/{Guid.NewGuid()}/events");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
