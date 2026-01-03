namespace PtlOrchestrator.Domain.Constant;

public static class PtlConstants
{
    public const string Command = "PP5";
    public const string Mode05 = "05";

    // Parte fissa usata dal PTL Tester
    public const string DefaultOptions = "0000";

    // Prefix modalità LED
    public const string LedModePrefix = "m";

    // Separatori
    public const char AddressSeparator = '#';
    public const char DisplaySeparator = ' ';

    // Formattazione indirizzi
    public const int ModuleAddressLength = 4;
}
