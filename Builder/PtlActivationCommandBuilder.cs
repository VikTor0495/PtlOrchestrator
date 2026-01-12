using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Constant;
using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Builder;


public static class PtlActivationCommandBuilder
{
    public static PtlActivation BuildPickFlowCommand()
    {
        return new PtlActivation
        {
            On = new PtlLightState
            {
                Color = PtlColor.Green,
                Blinking = true,
                Buzzer = false
            },
            AfterConfirm = new PtlLightState
            {
                Color = PtlColor.Green,
                Blinking = false,
                Buzzer = true
            }
        };
    }

    public static PtlActivation BuildErrorCommand()
    {
        return new PtlActivation
        {
            On = new PtlLightState
            {
                Color = PtlColor.Red,
                Blinking = true,
                Buzzer = false
            },
            AfterConfirm = new PtlLightState
            {
                Color = PtlColor.Off,
                Blinking = false,
                Buzzer = false
            }
        };
    }

    public static PtlActivation BuildOffCommand()
    {
        return new PtlActivation
        {
            On = new PtlLightState
            {
                Color = PtlColor.Off,
                Blinking = false,
                Buzzer = false
            }
        };
    }

}
