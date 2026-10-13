using AioiSystems.Lightstep;

namespace PtlOrchestrator.Service;

public interface ILightstepConnectionService
{

    bool IsConnected { get; }

    Task EnsureConnectedAsync(CancellationToken ct);

    EthernetController GetController();

    void Disconnect();
}
