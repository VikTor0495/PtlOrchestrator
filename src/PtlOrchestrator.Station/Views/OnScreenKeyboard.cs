using System.Diagnostics;

namespace PtlOrchestrator.Station.Views;

/// <summary>Apre la tastiera su schermo di Windows, utile sui monitor touch o con la pistola assente.</summary>
public static class OnScreenKeyboard
{
    public static void Open()
    {
        try
        {
            Process.Start(new ProcessStartInfo("osk.exe") { UseShellExecute = true });
        }
        catch (Exception)
        {
            // tastiera non disponibile su questa macchina: l'operatore usa quella fisica
        }
    }
}
