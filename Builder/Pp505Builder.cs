using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Constant;

namespace PtlOrchestrator.Builders;


public static class Pp505Builder
{
    public static string Build(
        string moduleAddress,
        PtlActivation activation)
    {
        Validate(moduleAddress, activation);

        var ledCode = BuildLedCode(activation);
        var display = activation.DisplayText ?? string.Empty;

        return string.Concat(
            PtlConstants.Command,
            PtlConstants.Mode05,
            PtlConstants.DefaultOptions,
            PtlConstants.LedModePrefix,
            ledCode,
            PtlConstants.AddressSeparator,
            moduleAddress,
            display
        );
    }

    private static string BuildLedCode(PtlActivation activation)
    {
        // Struttura mXYZ
        // X = colore
        // Y = blinking
        // Z = riservato (1 = default)

        var color = ((int)activation.Color).ToString();
        var blink = activation.Blinking ? "1" : "0";
        const string reserved = "1";

        return $"{color}{blink}{reserved}";
    }

    private static void Validate(
        string moduleAddress,
        PtlActivation activation)
    {
        if (int.TryParse(moduleAddress, out var address) && (address < 0 || address > 9999))
            throw new ArgumentOutOfRangeException(
                nameof(moduleAddress),
                "Module address deve essere tra 0 e 9999");

        if (activation.DisplayText is { Length: > 16 })
            throw new ArgumentException(
                "DisplayText troppo lungo (max 16 caratteri)",
                nameof(activation));
    }
}
