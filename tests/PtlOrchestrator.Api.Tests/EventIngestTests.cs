using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PtlOrchestrator.Api.Services;
using PtlOrchestrator.Api.Tests.Support;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Tests;

public class EventIngestTests : ApiTestBase
{
    [Fact]
    public async Task Ingest_StoresTheEventWithAllItsFields()
    {
        var sessionId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var sent = new OperationEvent(
            Guid.NewGuid(), EventFactory.BaseTime, "ST-01", sessionId, operatorId,
            OperationEventType.BarcodeAccepted, "8001234567890", "1", "0003", "NewItem");

        var response = await SendEventsAsync(sent);

        Assert.Equal(new IngestEventsResponse(Accepted: 1, Duplicates: 0), response);
        var stored = await Factory.WithDbContextAsync(db => db.OperationEvents.SingleAsync());
        Assert.Equal(sent.Id, stored.Id);
        Assert.Equal(sent.OccurredAt, stored.OccurredAt);
        Assert.Equal("ST-01", stored.StationId);
        Assert.Equal(sessionId, stored.SessionId);
        Assert.Equal(operatorId, stored.OperatorId);
        Assert.Equal(OperationEventType.BarcodeAccepted, stored.Type);
        Assert.Equal("8001234567890", stored.Barcode);
        Assert.Equal("1", stored.CartId);
        Assert.Equal("0003", stored.BasketId);
        Assert.Equal("NewItem", stored.Details);
    }

    [Fact]
    public async Task Ingest_StampsTheTimeOfReceptionFromTheServerClock()
    {
        await SendEventsAsync(EventFactory.Create(minutesFromBase: -30));

        var stored = await Factory.WithDbContextAsync(db => db.OperationEvents.SingleAsync());
        Assert.Equal(Factory.Clock.GetUtcNow(), stored.ReceivedAt);
    }

    [Fact]
    public async Task Ingest_StoresEventTypeAsReadableText()
    {
        await SendEventsAsync(EventFactory.Create(OperationEventType.WrongModulePressed));

        var rawType = await Factory.WithDbContextAsync(db => db.Database
            .SqlQueryRaw<string>("SELECT type AS Value FROM operation_events")
            .SingleAsync());
        Assert.Equal("WrongModulePressed", rawType);
    }

    [Fact]
    public async Task Ingest_ResendingTheSameEvent_IsIdempotent()
    {
        var operationEvent = EventFactory.Create();
        await SendEventsAsync(operationEvent);

        var response = await SendEventsAsync(operationEvent);

        Assert.Equal(new IngestEventsResponse(Accepted: 0, Duplicates: 1), response);
        Assert.Equal(1, await Factory.WithDbContextAsync(db => db.OperationEvents.CountAsync()));
    }

    [Fact]
    public async Task Ingest_WithAPartiallyKnownBatch_StoresOnlyTheNewEvents()
    {
        var known = EventFactory.Create();
        await SendEventsAsync(known);

        var response = await SendEventsAsync(known, EventFactory.Create(), EventFactory.Create());

        Assert.Equal(new IngestEventsResponse(Accepted: 2, Duplicates: 1), response);
        Assert.Equal(3, await Factory.WithDbContextAsync(db => db.OperationEvents.CountAsync()));
    }

    [Fact]
    public async Task Ingest_WithTheSameIdTwiceInOneBatch_StoresItOnce()
    {
        var operationEvent = EventFactory.Create();

        var response = await SendEventsAsync(operationEvent, operationEvent);

        Assert.Equal(new IngestEventsResponse(Accepted: 1, Duplicates: 1), response);
    }

    [Fact]
    public async Task Ingest_NormalizesLocalOffsetsToUtc()
    {
        var local = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.FromHours(2));
        var operationEvent = EventFactory.Create() with { OccurredAt = local };

        await SendEventsAsync(operationEvent);

        var stored = await Factory.WithDbContextAsync(db => db.OperationEvents.SingleAsync());
        Assert.Equal(TimeSpan.Zero, stored.OccurredAt.Offset);
        Assert.Equal(local.UtcDateTime, stored.OccurredAt.UtcDateTime);
    }

    [Fact]
    public async Task Ingest_RegistersTheStationAndUpdatesWhenItWasLastSeen()
    {
        await SendEventsAsync(EventFactory.Create(stationId: "ST-07"));
        var firstSeen = Factory.Clock.GetUtcNow();
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        await SendEventsAsync(EventFactory.Create(stationId: "ST-07"));

        var station = await Factory.WithDbContextAsync(db => db.Stations.SingleAsync());
        Assert.Equal("ST-07", station.Id);
        Assert.Equal(firstSeen, station.FirstSeenAt);
        Assert.Equal(firstSeen.AddMinutes(10), station.LastSeenAt);
    }

    [Fact]
    public async Task Ingest_WithEventsFromSeveralStations_RegistersEachOnce()
    {
        await SendEventsAsync(
            EventFactory.Create(stationId: "ST-01"),
            EventFactory.Create(stationId: "ST-02"),
            EventFactory.Create(stationId: "ST-01"));

        var stations = await Factory.WithDbContextAsync(db => db.Stations.Select(s => s.Id).OrderBy(id => id).ToListAsync());
        Assert.Equal(["ST-01", "ST-02"], stations);
    }

    [Fact]
    public async Task Ingest_TruncatesOverlongDetailsInsteadOfFailing()
    {
        await SendEventsAsync(EventFactory.Create(details: new string('x', 5000)));

        var stored = await Factory.WithDbContextAsync(db => db.OperationEvents.SingleAsync());
        Assert.Equal(1000, stored.Details!.Length);
    }

    // ---- proiezione dei turni di lavoro ----

    [Fact]
    public async Task Sessions_StartAndEndEvents_BuildAWorkSession()
    {
        var mario = await CreateOperatorAsync();
        var sessionId = Guid.NewGuid();

        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: 0),
            EventFactory.Create(OperationEventType.SessionEnded, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: 90));

        var session = await Factory.WithDbContextAsync(db => db.WorkSessions.SingleAsync());
        Assert.Equal(sessionId, session.Id);
        Assert.Equal(mario.Id, session.OperatorId);
        Assert.Equal("ST-01", session.StationId);
        Assert.Equal(EventFactory.BaseTime, session.StartedAt);
        Assert.Equal(EventFactory.BaseTime.AddMinutes(90), session.EndedAt);
    }

    [Fact]
    public async Task Sessions_WithOnlyTheStartEvent_StayOpen()
    {
        var mario = await CreateOperatorAsync();

        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: mario.Id));

        var session = await Factory.WithDbContextAsync(db => db.WorkSessions.SingleAsync());
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public async Task Sessions_StartAndEndInSeparateBatches_CompleteTheSameSession()
    {
        var mario = await CreateOperatorAsync();
        var sessionId = Guid.NewGuid();

        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: sessionId, operatorId: mario.Id));
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionEnded, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: 60));

        var session = await Factory.WithDbContextAsync(db => db.WorkSessions.SingleAsync());
        Assert.Equal(EventFactory.BaseTime, session.StartedAt);
        Assert.Equal(EventFactory.BaseTime.AddMinutes(60), session.EndedAt);
    }

    [Fact]
    public async Task Sessions_EndArrivingBeforeStart_StillConvergesToTheRightTimes()
    {
        var mario = await CreateOperatorAsync();
        var sessionId = Guid.NewGuid();

        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionEnded, sessionId: sessionId, operatorId: mario.Id, minutesFromBase: 60));
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: sessionId, operatorId: mario.Id));

        var session = await Factory.WithDbContextAsync(db => db.WorkSessions.SingleAsync());
        Assert.Equal(EventFactory.BaseTime, session.StartedAt);
        Assert.Equal(EventFactory.BaseTime.AddMinutes(60), session.EndedAt);
    }

    [Fact]
    public async Task Sessions_ForAnUnknownOperator_KeepTheEventsButSkipTheProjection()
    {
        await SendEventsAsync(
            EventFactory.Create(OperationEventType.SessionStarted, sessionId: Guid.NewGuid(), operatorId: Guid.NewGuid()));

        Assert.Equal(1, await Factory.WithDbContextAsync(db => db.OperationEvents.CountAsync()));
        Assert.Equal(0, await Factory.WithDbContextAsync(db => db.WorkSessions.CountAsync()));
    }

    [Fact]
    public async Task Sessions_OtherEventTypes_DoNotCreateWorkSessions()
    {
        var mario = await CreateOperatorAsync();

        await SendEventsAsync(
            EventFactory.Create(OperationEventType.BarcodeAccepted, sessionId: Guid.NewGuid(), operatorId: mario.Id));

        Assert.Equal(0, await Factory.WithDbContextAsync(db => db.WorkSessions.CountAsync()));
    }

    // ---- validazione ----

    [Fact]
    public async Task Ingest_WithAnEmptyBatch_IsRejected()
    {
        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithMoreThanTheMaximumBatchSize_IsRejected()
    {
        var events = Enumerable.Range(0, OperationEventValidator.MaxBatchSize + 1)
            .Select(_ => EventFactory.Create())
            .ToList();

        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest(events));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Factory.WithDbContextAsync(db => db.OperationEvents.CountAsync()));
    }

    [Fact]
    public async Task Ingest_WithTheMaximumBatchSize_IsAccepted()
    {
        var events = Enumerable.Range(0, OperationEventValidator.MaxBatchSize)
            .Select(_ => EventFactory.Create())
            .ToList();

        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest(events));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OperationEventValidator.MaxBatchSize, await Factory.WithDbContextAsync(db => db.OperationEvents.CountAsync()));
    }

    [Fact]
    public async Task Ingest_WithOneInvalidEvent_RejectsTheWholeBatchAndStoresNothing()
    {
        var valid = EventFactory.Create();
        var invalid = EventFactory.Create(stationId: " ");

        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest([valid, invalid]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await Factory.WithDbContextAsync(db => db.OperationEvents.CountAsync()));
    }

    [Fact]
    public async Task Ingest_WithAnEmptyEventId_IsRejected()
    {
        var invalid = EventFactory.Create(id: Guid.Empty) with { Id = Guid.Empty };

        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest([invalid]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithAnUnknownEventType_IsRejected()
    {
        var invalid = EventFactory.Create() with { Type = (OperationEventType)999 };

        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest([invalid]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithABarcodeLongerThanTheColumn_IsRejected()
    {
        var invalid = EventFactory.Create(barcode: new string('9', 129));

        var response = await Station.PostAsJsonAsync("/api/events", new IngestEventsRequest([invalid]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithAnUnknownTypeNameInTheJson_IsRejected()
    {
        var body = $$"""
            {"events":[{"id":"{{Guid.NewGuid()}}","occurredAt":"2026-10-13T08:00:00Z","stationId":"ST-01","type":"Inventato"}]}
            """;

        var response = await Station.PostAsync("/api/events", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_SerializesTheEventTypeAsAString()
    {
        var body = $$"""
            {"events":[{"id":"{{Guid.NewGuid()}}","occurredAt":"2026-10-13T08:00:00Z","stationId":"ST-01","type":"CartsReset"}]}
            """;

        var response = await Station.PostAsync("/api/events", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
