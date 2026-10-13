using PtlOrchestrator.Service;

namespace PtlOrchestrator.Core.Tests.Service;

/// <summary>
/// AioiSystems.Lightstep è un assembly .NET Framework 4 (AnyCPU): questo test fa da guardia
/// sui porting di runtime, perché LightstepConnectionService inghiotte qualunque errore
/// di connessione e un problema di caricamento passerebbe inosservato fino al campo.
/// </summary>
public class LightstepLibraryCompatibilityTests
{
    private static Type ControllerType
        => typeof(ILightstepConnectionService)
            .GetMethod(nameof(ILightstepConnectionService.GetController))!
            .ReturnType;

    [Fact]
    public void EthernetController_CanBeLoadedAndInstantiatedWithoutHardware()
    {
        var controller = Activator.CreateInstance(ControllerType);

        Assert.NotNull(controller);
    }

    [Fact]
    public void EthernetController_ExposesTheMembersUsedByTheApplication()
    {
        var members = ControllerType.GetMembers().Select(m => m.Name).ToHashSet();

        Assert.Superset(
            new HashSet<string> { "SetLicense", "Connect", "SendCommand", "GetCommand", "Close", "IsConnected" },
            members);
    }
}
