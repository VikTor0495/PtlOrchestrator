using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Service;

public interface IPtlCommandService
{
    /// <summary>Invia un comando PP505 al controller indicato (nullo = controller predefinito).</summary>
    Task SendRawAsync(string command, PtlEndpoint? endpoint, CancellationToken cancellationToken);

    /// <summary>Attende la pressione di un pulsante dal controller indicato e restituisce il modulo premuto.</summary>
    Task<string> WaitForButtonAsync(string expectedModule, PtlEndpoint? endpoint, CancellationToken cancellationToken);
}
