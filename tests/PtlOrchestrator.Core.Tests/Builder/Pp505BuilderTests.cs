using PtlOrchestrator.Builder;

namespace PtlOrchestrator.Core.Tests.Builder;

/// <summary>
/// Fissano il protocollo PP505 carattere per carattere: una modifica involontaria alle stringhe
/// cambierebbe il comportamento fisico delle lampade sul campo.
/// Nel sorgente del builder DC1/DC2 sono caratteri di controllo grezzi (invisibili),
/// qui sono resi espliciti con costanti.
/// </summary>
public class Pp505BuilderTests
{
    private const string Blank = "     ";
    private const string Dc1 = "\u0011";
    private const string Dc2 = "\u0012";

    [Fact]
    public void BuildFlashGreenCommandThenFix_TargetsSingleModule()
    {
        var command = Pp505Builder.BuildFlashGreenCommandThenFix("0001");

        Assert.Equal("PP5050000m111" + Dc1 + "m21!" + Dc2 + "m4D0001" + Blank, command);
    }

    [Fact]
    public void BuildGreenCommand_ForSingleModule_UsesFixedGreenWithDc1()
    {
        var command = Pp505Builder.BuildGreenCommand("0001");

        Assert.Equal("PP5050000m11!" + Dc1 + "0001" + Blank, command);
    }

    [Fact]
    public void BuildGreenCommand_ForMultipleModules_SeparatesAddressesWithBlankDisplay()
    {
        var command = Pp505Builder.BuildGreenCommand(["0001", "0002"]);

        Assert.Equal("PP5050000m11!" + Dc1 + "0001" + Blank + "0002" + Blank, command);
    }

    [Fact]
    public void BuildFlashRedCommandThenOff_TargetsSingleModule()
    {
        var command = Pp505Builder.BuildFlashRedCommandThenOff("0001");

        Assert.Equal("PP5050000m13" + Dc1 + Dc1 + "m21" + Dc1 + Dc1 + "0001" + Blank, command);
    }

    [Fact]
    public void BuildRedBuzzer_TargetsSingleModule()
    {
        var command = Pp505Builder.BuildRedBuzzer("0002");

        Assert.Equal("PP5050000m12" + Dc1 + Dc2 + "m21" + Dc1 + Dc1 + "m4D0002" + Blank, command);
    }

    [Fact]
    public void BuildArmedNoLightEnabledButton_ForMultipleModules_JoinsAddresses()
    {
        var command = Pp505Builder.BuildArmedNoLightEnabledButton(["0002", "0003"]);

        Assert.Equal(
            "PP5050000m11" + Dc1 + Dc1 + "m21" + Dc1 + Dc1 + "m4D0002" + Blank + "0003" + Blank,
            command);
    }

    [Fact]
    public void BuildArmedNoLightEnabledButton_WithoutModules_ContainsOnlyTrailingBlank()
    {
        var command = Pp505Builder.BuildArmedNoLightEnabledButton([]);

        Assert.Equal("PP5050000m11" + Dc1 + Dc1 + "m21" + Dc1 + Dc1 + "m4D" + Blank, command);
    }

    [Fact]
    public void BuildArmedNoLightButtonDisabled_UsesDisabledButtonFlag()
    {
        var command = Pp505Builder.BuildArmedNoLightButtonDisabled(["0001", "0002"]);

        Assert.Equal(
            "PP5050000m11" + Dc1 + Dc1 + "m21" + Dc1 + Dc1 + "m4E0001" + Blank + "0002" + Blank,
            command);
    }

    [Fact]
    public void BuildArmedNoLightThenBlinkRed_TargetsSingleModule()
    {
        var command = Pp505Builder.BuildArmedNoLightThenBlinkRed("0001");

        Assert.Equal(
            "PP5050000m11" + Dc1 + Dc1 + "m23" + Dc1 + Dc1 + "m31" + Dc1 + Dc1 + "0001" + Blank,
            command);
    }

    [Fact]
    public void BuildOffCommand_PrefixesTurnOffAndJoinsAddresses()
    {
        var command = Pp505Builder.BuildOffCommand(["0001", "0002"]);

        Assert.Equal("D0001" + Blank + "0002" + Blank, command);
    }
}
