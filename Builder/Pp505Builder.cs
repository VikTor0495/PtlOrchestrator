using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Constant;
using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Builder;


public static class Pp505Builder
{
    public static string Build(
        string moduleAddress,
        PtlActivation activation)
    {
        Validate(moduleAddress, activation);

        return activation.Color == PtlColor.Off
                ? PtlConstants.TurnOffCommandPrefix + moduleAddress : string.Concat(
        PtlConstants.Command,
        PtlConstants.FnDataAfterKey,
        PtlConstants.SignalLightCtrl,
        PtlConstants.LedModePrefix,
        BuildLedCode(activation),
        PtlConstants.AfterConfirmPrefix,
        "1\u0011!", // Led always OFF, display ON
        moduleAddress,
        NormalizeDisplay(activation.DisplayText)
        );
    }


    private static string NormalizeDisplay(string? text)
    {
        text ??= string.Empty;

        if (text.Length > PtlConstants.DisplayLength)
            text = text[..PtlConstants.DisplayLength];

        return text.PadLeft(PtlConstants.DisplayLength, ' ');
    }


    private static string BuildLedCode(PtlActivation activation)
    {
        return (activation.Color, activation.Blinking) switch
        {
            // GREEN
            (PtlColor.Green, false) => "1!",
            (PtlColor.Green, true) => "11#",

            // RED
            (PtlColor.Red, true) => "3\u0011!",
            (PtlColor.Red, false) => "2\u0011!",


            // OFF / fallback
            (PtlColor.Off, _) => string.Empty,

            _ => throw new ArgumentOutOfRangeException(
                nameof(activation),
                $"Combinazione LED non supportata: {activation.Color} / blink={activation.Blinking}")
        };
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
