namespace PtlOrchestrator.Domain.Constant;

public static class PtlConstants
{
     public const string Command = "PP505";
    public const string FnDataAfterKey = "00";
    public const string SignalLightCtrl = "00";
    public const string LedModePrefix = "m1";
    public const string AfterConfirmPrefix = "m2";
    public const int ModuleAddressLength = 4;
    public const int DisplayLength = 5; 

    // Spegni modulo
    public const string TurnOffCommandPrefix = "D";
}
