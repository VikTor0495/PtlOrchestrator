namespace PtlOrchestrator.Service;

public interface IPtlCommandService
{
    Task<PtlSendResult> SwitchOnAsync(int moduleAddress, PtlLightSpec spec, CancellationToken ct);
    Task<PtlSendResult> SwitchOffAsync(int moduleAddress, CancellationToken ct);

    /// <summary>
    /// Attende la conferma operatore (pressione tasto) per il modulo address.
    /// </summary>
    Task<bool> WaitForOperatorConfirmAsync(int moduleAddress, TimeSpan timeout, CancellationToken ct);
}
