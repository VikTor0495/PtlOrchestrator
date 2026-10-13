namespace PtlOrchestrator.Api.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";
    public const int MinimumKeyLength = 16;

    /// <summary>Chiave condivisa dalle postazioni: può inviare eventi e leggere gli operatori.</summary>
    public string StationKey { get; set; } = string.Empty;

    /// <summary>Chiave amministrativa: gestione operatori e consultazione dei report.</summary>
    public string AdminKey { get; set; } = string.Empty;
}
