namespace PtlOrchestrator.Api.Security;

public static class ApiKeyDefaults
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public const string StationRole = "station";
    public const string AdminRole = "admin";

    public const string StationPolicy = "Station";
    public const string AdminPolicy = "Admin";
}
