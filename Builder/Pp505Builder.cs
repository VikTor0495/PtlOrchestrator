using System.Text;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Constant;
using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Builder;


public static class Pp505Builder
{

    public static string BuildFlashGreenCommandThenOff(string moduleAddress)
    {
        Validate(moduleAddress);

        var sb = new StringBuilder();

        sb.Append(PtlConstants.Command);
        sb.Append(PtlConstants.FnDataAfterKey);
        sb.Append(PtlConstants.SignalLightCtrl);

        // m1 – stato iniziale: verde lampeggiante
        sb.Append(PtlConstants.LedModePrefix);
        sb.Append("11#"); // verde lampeggiante
        //sb.Append('\u0011'); // DC1

        // m2 – after confirm: verde fisso
        sb.Append(PtlConstants.AfterConfirmPrefix);
        sb.Append("1!"); // verde fisso
        sb.Append('\u0012'); // DC2

        sb.Append(moduleAddress);

        sb.Append(GetBlankDisplay());

        return sb.ToString();
    }

    public static string BuildGreenCommand(string moduleAddress)
    {
        Validate(moduleAddress);

        var sb = new StringBuilder();

        sb.Append(PtlConstants.Command);
        sb.Append(PtlConstants.FnDataAfterKey);
        sb.Append(PtlConstants.SignalLightCtrl);

        // LED verde fisso
        sb.Append(PtlConstants.LedModePrefix);
        sb.Append("1!"); // verde fisso
        sb.Append('\u0011'); // DC1

        sb.Append(moduleAddress);

        sb.Append(GetBlankDisplay());

        return sb.ToString();
    }

    public static string BuildFlashRedCommandThenOff(string moduleAddress)
    {
        Validate(moduleAddress);

        var sb = new StringBuilder();

        sb.Append(PtlConstants.Command);
        sb.Append(PtlConstants.FnDataAfterKey);
        sb.Append(PtlConstants.SignalLightCtrl);

        // m1 – stato iniziale: rosso lampeggiante
        sb.Append(PtlConstants.LedModePrefix);
        sb.Append('3');
        sb.Append('\u0011');
        sb.Append('\u0011');

        // m2 – after confirm: spento
        sb.Append(PtlConstants.AfterConfirmPrefix);
        sb.Append('1');
        sb.Append('\u0011');
        sb.Append('\u0011');

        sb.Append(moduleAddress);

        sb.Append(GetBlankDisplay());

        return sb.ToString();
    }

    public static string BuildArmedNoLight(string moduleAddress)
    {
        Validate(moduleAddress);

        return string.Concat(
            PtlConstants.Command,
            PtlConstants.FnDataAfterKey,
            PtlConstants.SignalLightCtrl,
            "m1",
            "1\u0011",    // LED OFF + DC1
            "\u0011",     // sensori attivi, nessuna luce
            moduleAddress,
            GetBlankDisplay()
        );
    }

    public static string BuildOffCommand(string moduleAddress)
    {
        return PtlConstants.TurnOffCommandPrefix + moduleAddress;
    }


    private static string GetBlankDisplay()
    {
        return new string(' ', PtlConstants.DisplayLength);
    }

    private static void Validate(
        string moduleAddress)
    {
        if (int.TryParse(moduleAddress, out var address) && (address < 0 || address > 9999))
            throw new ArgumentOutOfRangeException(
                nameof(moduleAddress),
                "Module address deve essere tra 0 e 9999");

    }
}
