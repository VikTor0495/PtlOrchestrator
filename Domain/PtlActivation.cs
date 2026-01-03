using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Domain;

public sealed class PtlActivation
{
    public required PtlColor Color { get; init; }

    public bool Blinking { get; init; }

    public string? DisplayText { get; init; }
}
