using AioiSystems.Lightstep;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Domain;
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

    public bool IsConnectedTo(PtlEndpoint endpoint) => IsConnected;

    public Task EnsureConnectedAsync(PtlEndpoint endpoint, CancellationToken ct) => EnsureConnectedAsync(ct);

    public Task<bool> TryConnectAsync(PtlEndpoint endpoint, CancellationToken ct)
    {
        Calls.Add("try-connect " + endpoint);
        IsConnected = true;
        return Task.FromResult(true);
    }

    public EthernetController GetController(PtlEndpoint? endpoint = null) => throw new NotSupportedException();

    public void Disconnect()
    {
        Calls.Add("disconnect");
        IsConnected = false;
    }

    public void Disconnect(PtlEndpoint endpoint) => Disconnect();
}
