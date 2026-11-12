using AioiSystems.Lightstep;
using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Service;

/// <summary>
/// Connessioni verso i controller PTL. Ogni carrello ha il proprio controller: le connessioni
/// sono un pool per endpoint. Il riferimento <c>null</c> indica il controller predefinito della configurazione.
/// </summary>
public interface ILightstepConnectionService
{
    /// <summary>Vero se c'è almeno una connessione e tutte quelle aperte sono attive.</summary>
    bool IsConnected { get; }

    bool IsConnectedTo(PtlEndpoint endpoint);

    /// <summary>Connette il controller predefinito, riprovando finché non risponde.</summary>
    Task EnsureConnectedAsync(CancellationToken ct);

    /// <summary>Connette un controller specifico, riprovando finché non risponde.</summary>
    Task EnsureConnectedAsync(PtlEndpoint endpoint, CancellationToken ct);

    /// <summary>Un solo tentativo di connessione: per il check-in, che non deve bloccarsi.</summary>
    Task<bool> TryConnectAsync(PtlEndpoint endpoint, CancellationToken ct);

    EthernetController GetController(PtlEndpoint? endpoint = null);

    /// <summary>Chiude tutte le connessioni.</summary>
    void Disconnect();

    void Disconnect(PtlEndpoint endpoint);
}
