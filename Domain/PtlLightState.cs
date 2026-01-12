namespace PtlOrchestrator.Domain;

public sealed class PtlLightState
{
    public required Enum.PtlColor Color { get; init; }
    public bool Blinking { get; init; }
    public bool Buzzer { get; init; }
}
