using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Domain;

public sealed class PtlActivation
{
    /* public required PtlColor Color { get; init; }

    public bool Blinking { get; init; }

    public string? DisplayText { get; init; } */

     // m1 – stato iniziale
    public required PtlLightState On { get; init; }

    // m2 – dopo conferma (tasto / inserimento corretto)
    public PtlLightState? AfterConfirm { get; init; }

    // m3 – dopo FN (sensore / errore)
    public PtlLightState? AfterFn { get; init; }

}
