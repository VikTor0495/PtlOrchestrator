namespace PtlOrchestrator.Configuration;

public sealed class LightstepOptions
{
    public string LicenzeKey { get; init; } = string.Empty;
    public string ControllerIp { get; init; } = string.Empty;
    public int ControllerPort { get; init; }
    public bool AutoReconnect { get; init; }
    public int ConnectionTimeout { get; init; }
}
