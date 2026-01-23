using System.Text;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Builder;


public static class Pp505Builder
{
    private const string Command = "PP505";
    private const string FnDataAfterKey = "00";
    private const string SignalLightCtrl = "00";
    private const string LedModePrefix = "m1";
    private const string AfterConfirmPrefix = "m2";
    private const string AfterFnPrefix = "m3";
    private const int ModuleAddressLength = 4;
    private const int DisplayLength = 5;
    private const string TurnOffCommandPrefix = "D";
    private const string BlankDisplay = "     ";


    public static string BuildFlashGreenCommandThenOff(string moduleAddress)
    {
        return "PP5050000m111m21!" + moduleAddress + BlankDisplay;
    }

    public static string BuildGreenCommand(string moduleAddress)
    {
        var sb = new StringBuilder();

        sb.Append(Command);
        sb.Append(FnDataAfterKey);
        sb.Append(SignalLightCtrl);

        // LED verde fisso
        sb.Append(LedModePrefix);
        sb.Append("1!"); // verde fisso
        sb.Append('\u0011'); // DC1

        sb.Append(moduleAddress);

        sb.Append(BlankDisplay);

        return sb.ToString();
    }

    public static string BuildFlashRedCommandThenOff(string moduleAddress)
    {
        var sb = new StringBuilder();

        sb.Append(Command);
        sb.Append(FnDataAfterKey);
        sb.Append(SignalLightCtrl);

        // m1 – stato iniziale: rosso lampeggiante
        sb.Append(LedModePrefix);
        sb.Append('3');
        sb.Append('\u0011');
        sb.Append('\u0011');

        // m2 – after confirm: spento
        sb.Append(AfterConfirmPrefix);
        sb.Append('1');
        sb.Append('\u0011');
        sb.Append('\u0011');

        sb.Append(moduleAddress);

        sb.Append(BlankDisplay);

        return sb.ToString();
    }

    public static string BuildRedBuzzer(string moduleAddress)
    {
        return "PP5050000m12m21" + moduleAddress + BlankDisplay;
    }

    public static string BuildArmedNoLight(string moduleAddress)
    {
        return string.Concat(
            Command,
            FnDataAfterKey,
            SignalLightCtrl,
            "m1",
            "1\u0011",    // LED OFF + DC1
            "\u0011",     // sensori attivi, nessuna luce
            moduleAddress,
            BlankDisplay
        );
    }

    public static string BuildArmedNoLightThenBlinkRed(string moduleAddress)
    {
        //return "PP5050000m11m23" + moduleAddress + BlankDisplay;
        return "PP5050000m11m23m31" + moduleAddress + BlankDisplay;
    }


    public static string BuildOffCommand(string moduleAddress)
    {
        return TurnOffCommandPrefix + moduleAddress;
    }

}
