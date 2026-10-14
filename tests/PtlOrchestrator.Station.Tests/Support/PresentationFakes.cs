using AioiSystems.Lightstep;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Service;
using PtlOrchestrator.Station.Presentation;
using PtlOrchestrator.Station.Storage;

namespace PtlOrchestrator.Station.Tests.Support;

internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

internal sealed class FakeOutbox : IOutbox
{
    public int Pending { get; set; }
    public int Quarantined { get; set; }

    public Task EnqueueAsync(OperationEvent operationEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<OperationEvent>> PeekBatchAsync(int maxCount, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task RemoveAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task RecordFailureAsync(IReadOnlyCollection<Guid> eventIds, string error, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task QuarantineAsync(IReadOnlyCollection<Guid> eventIds, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<int> CountPendingAsync(CancellationToken cancellationToken) => Task.FromResult(Pending);
    public Task<int> CountQuarantinedAsync(CancellationToken cancellationToken) => Task.FromResult(Quarantined);
}

internal sealed class FakeLightstep : ILightstepConnectionService
{
    public List<string> Calls { get; } = [];
    public bool IsConnected { get; set; }

    public Task EnsureConnectedAsync(CancellationToken ct)
    {
        Calls.Add("connect");
        IsConnected = true;
        return Task.CompletedTask;
    }

    public EthernetController GetController() => throw new NotSupportedException();

    public void Disconnect()
    {
        Calls.Add("disconnect");
        IsConnected = false;
    }
}
