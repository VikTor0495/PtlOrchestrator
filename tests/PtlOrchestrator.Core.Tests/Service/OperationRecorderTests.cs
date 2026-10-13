using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Core.Tests.Support;
using PtlOrchestrator.Domain.Operations;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Core.Tests.Service;

public class OperationRecorderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 30, 0, TimeSpan.Zero);

    private readonly CollectingOperationEventSink _sink = new();
    private readonly OperatorSessionContext _sessionContext = new();
    private readonly OperationRecorder _recorder;

    public OperationRecorderTests()
    {
        _recorder = new OperationRecorder(
            _sink,
            _sessionContext,
            Options.Create(new StationOptions { StationId = "ST-01" }),
            new FakeTimeProvider(Now),
            NullLogger<OperationRecorder>.Instance);
    }

    [Fact]
    public async Task RecordAsync_StampsStationAndTime()
    {
        await _recorder.RecordAsync(OperationEventType.CartsReset);

        var recorded = Assert.Single(_sink.Events);
        Assert.Equal("ST-01", recorded.StationId);
        Assert.Equal(Now, recorded.OccurredAt);
        Assert.Equal(OperationEventType.CartsReset, recorded.Type);
    }

    [Fact]
    public async Task RecordAsync_WithoutActiveSession_LeavesOperatorAndSessionEmpty()
    {
        await _recorder.RecordAsync(OperationEventType.CartsReset);

        var recorded = Assert.Single(_sink.Events);
        Assert.Null(recorded.SessionId);
        Assert.Null(recorded.OperatorId);
    }

    [Fact]
    public async Task RecordAsync_WithActiveSession_AttributesTheEventToOperatorAndSession()
    {
        var session = new OperatorSession(Guid.NewGuid(), Guid.NewGuid(), "Mario", Now);
        _sessionContext.Begin(session);

        await _recorder.RecordAsync(OperationEventType.BarcodeAccepted, "BC1", "1", "0001", "NewItem");

        var recorded = Assert.Single(_sink.Events);
        Assert.Equal(session.SessionId, recorded.SessionId);
        Assert.Equal(session.OperatorId, recorded.OperatorId);
        Assert.Equal("BC1", recorded.Barcode);
        Assert.Equal("1", recorded.CartId);
        Assert.Equal("0001", recorded.BasketId);
        Assert.Equal("NewItem", recorded.Details);
    }

    [Fact]
    public async Task RecordAsync_AfterSessionEnds_StopsAttributingToTheOperator()
    {
        _sessionContext.Begin(new OperatorSession(Guid.NewGuid(), Guid.NewGuid(), "Mario", Now));
        _sessionContext.End();

        await _recorder.RecordAsync(OperationEventType.CartsReset);

        Assert.Null(Assert.Single(_sink.Events).OperatorId);
    }

    [Fact]
    public async Task RecordAsync_GeneratesAUniqueIdForEveryEvent()
    {
        await _recorder.RecordAsync(OperationEventType.CartsReset);
        await _recorder.RecordAsync(OperationEventType.CartsReset);

        Assert.Equal(2, _sink.Events.Select(e => e.Id).Distinct().Count());
        Assert.All(_sink.Events, e => Assert.NotEqual(Guid.Empty, e.Id));
    }

    [Fact]
    public async Task RecordAsync_WhenSinkFails_DoesNotThrow()
    {
        _sink.Failure = new IOException("disco pieno");

        var exception = await Record.ExceptionAsync(
            () => _recorder.RecordAsync(OperationEventType.CartsReset));

        Assert.Null(exception);
    }
}
