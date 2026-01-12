using System.Text;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Constant;
using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Builder;


public static class Pp505Builder
{
    /*  public static string Build(
         string moduleAddress,
         PtlActivation activation)
     {
         Validate(moduleAddress);

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
         GetBlankDisplay()
         );
     }
  */
    public static string Build(string moduleAddress, PtlActivation activation)

    {
        Validate(moduleAddress);

        if (activation.On.Color == PtlColor.Off)
            return PtlConstants.TurnOffCommandPrefix + moduleAddress;

        var sb = new StringBuilder();

        sb.Append(PtlConstants.Command);
        sb.Append(PtlConstants.FnDataAfterKey);
        sb.Append(PtlConstants.SignalLightCtrl);

        // m1 – stato iniziale
        sb.Append(PtlConstants.LedModePrefix);
        sb.Append(BuildLedCode(activation.On));

        // m2 – after confirm
        if (activation.AfterConfirm != null)
        {
            sb.Append(PtlConstants.AfterConfirmPrefix);
            sb.Append(BuildLedCodeAfterConfirm(activation.AfterConfirm));
        }

        // m3 – after FN
        /*   if (activation.AfterFn != null)
          {
              sb.Append(PtlConstants.AfterFnPrefix);
              sb.Append(BuildLedCode(activation.AfterFn));
          } */

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


    private static string GetBlankDisplay()
    {
        return new string(' ', PtlConstants.DisplayLength);
    }

    private static string BuildLedCode(PtlLightState ptlLightState)
    {
        return (ptlLightState.Color, ptlLightState.Blinking) switch
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
                nameof(ptlLightState),
                $"Combinazione LED non supportata: {ptlLightState.Color} / blink={ptlLightState.Blinking}")
        };
    }

    private static string BuildLedCodeAfterConfirm(PtlLightState state)
    {
        var led = state.Color switch
        {
            PtlColor.Green => state.Blinking ? "11!" : "1!",
            PtlColor.Red => state.Blinking ? "3!" : "2!",
            PtlColor.Off => "1!",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

        // DC2 = fine m2 + buzzer
        return led + "\u0012";
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
