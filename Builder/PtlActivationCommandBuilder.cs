using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Constant;
using PtlOrchestrator.Domain.Enum;

namespace PtlOrchestrator.Builder;


public static class PtlActivationCommandBuilder
{

   public static PtlActivation BuildGreenActivation(Basket basket)
    {
        return new PtlActivation
        {
            Color = PtlColor.Green,
            Blinking = true,
            DisplayText = GetNormalizedDisplayString(
                basket.CurrentQuantity,
                basket.MaxQuantity)
        };
    }

    public static PtlActivation BuildRedFixedActivation(Basket basket)
    {
        return new PtlActivation
        {
            Color = PtlColor.Red,
            Blinking = false,
            DisplayText = GetNormalizedDisplayString(
                basket.CurrentQuantity,
                basket.MaxQuantity)
        };
    }

    public static PtlActivation BuildOffActivation()
    {
        return new PtlActivation
        {
            Color = PtlColor.Off,
            Blinking = false,
            DisplayText = string.Empty
        };
    }

    
    private static string GetNormalizedDisplayString(int currentQuantity, int maxQuantity)
    {
        const int DisplayLength = 5;

        var displayText = $"{currentQuantity}/{maxQuantity}";

        if (displayText.Length > DisplayLength)
            return currentQuantity.ToString();

        return displayText.PadLeft(DisplayLength, ' ');
    }
}
