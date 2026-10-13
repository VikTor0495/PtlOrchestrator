using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Service.Impl;

public sealed class OperationRecorder(
    IOperationEventSink sink,
    IOperatorSessionContext sessionContext,
    IOptions<StationOptions> stationOptions,
    TimeProvider timeProvider,
    ILogger<OperationRecorder> logger) : IOperationRecorder
{
    private readonly IOperationEventSink _sink = sink;
    private readonly IOperatorSessionContext _sessionContext = sessionContext;
    private readonly StationOptions _stationOptions = stationOptions.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<OperationRecorder> _logger = logger;

    public async Task RecordAsync(
        OperationEventType type,
        string? barcode = null,
        string? cartId = null,
        string? basketId = null,
        string? details = null)
    {
        try
        {
            var operationEvent = CreateEvent(type, barcode, cartId, basketId, details);

            await _sink.WriteAsync(operationEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registrazione dell'evento {EventType} non riuscita", type);
        }
    }

    private OperationEvent CreateEvent(
        OperationEventType type,
        string? barcode,
        string? cartId,
        string? basketId,
        string? details)
    {
        var now = _timeProvider.GetUtcNow();
        var session = _sessionContext.Current;

        return new OperationEvent(
            Id: Guid.CreateVersion7(now),
            OccurredAt: now,
            StationId: _stationOptions.StationId,
            SessionId: session?.SessionId,
            OperatorId: session?.OperatorId,
            Type: type,
            Barcode: barcode,
            CartId: cartId,
            BasketId: basketId,
            Details: details);
    }
}
