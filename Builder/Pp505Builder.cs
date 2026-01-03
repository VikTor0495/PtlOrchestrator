using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Constant;

namespace PtlOrchestrator.Builders;


public static class Pp505Builder
{
    public static string Build(
        int moduleAddress,
        PtlActivation activation)
    {
        Validate(moduleAddress, activation);

        var ledCode = BuildLedCode(activation);
        var address = moduleAddress.ToString($"D{PtlConstants.ModuleAddressLength}");
        var display = activation.DisplayText ?? string.Empty;

        return string.Concat(
            PtlConstants.Command,
            PtlConstants.Mode05,
            PtlConstants.DefaultOptions,
            PtlConstants.LedModePrefix,
            ledCode,
            PtlConstants.AddressSeparator,
            address,
            PtlConstants.DisplaySeparator,
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
        int moduleAddress,
        PtlActivation activation)
    {
        if (moduleAddress < 0 || moduleAddress > 9999)
            throw new ArgumentOutOfRangeException(
                nameof(moduleAddress),
                "Module address deve essere tra 0 e 9999");

        if (activation.DisplayText is { Length: > 16 })
            throw new ArgumentException(
                "DisplayText troppo lungo (max 16 caratteri)",
                nameof(activation));
    }
}
