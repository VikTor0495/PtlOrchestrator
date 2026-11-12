using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace PtlOrchestrator.Station.Identity;

/// <summary>
/// Identificativo della postazione. Se in configurazione c'è un <c>Station:StationId</c> esplicito vince;
/// altrimenti è derivato dal <c>MachineGuid</c> di Windows, generato una volta all'installazione del sistema:
/// resta lo stesso se si reinstalla l'applicazione e non richiede un censimento preventivo.
/// La postazione si registra sul server alla prima chiamata (scansione, check-in o evento).
/// </summary>
public static class StationIdentity
{
    private const string Prefix = "ST-";
    private const int HashLength = 8;

    public static string Resolve(string? configuredId)
        => string.IsNullOrWhiteSpace(configuredId) ? FromMachine() : configuredId.Trim();

    public static string FromMachine()
    {
        var source = ReadMachineGuid() ?? Environment.MachineName;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source.ToLowerInvariant()));

        return Prefix + Convert.ToHexString(hash)[..HashLength];
    }

    private static string? ReadMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
