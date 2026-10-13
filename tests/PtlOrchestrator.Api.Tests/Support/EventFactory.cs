using PtlOrchestrator.Contracts.Operations;

namespace PtlOrchestrator.Api.Tests.Support;

internal static class EventFactory
{
    public static readonly DateTimeOffset BaseTime = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    public static OperationEvent Create(
        OperationEventType type = OperationEventType.BarcodeAccepted,
        string stationId = "ST-01",
        Guid? sessionId = null,
        Guid? operatorId = null,
        int minutesFromBase = 0,
        Guid? id = null,
        string? barcode = null,
        string? details = null)
        => new(
            id ?? Guid.NewGuid(),
            BaseTime.AddMinutes(minutesFromBase),
            stationId,
            sessionId,
            operatorId,
            type,
            barcode,
            Details: details);
}
