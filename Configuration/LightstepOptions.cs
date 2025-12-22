namespace PtlController.Configuration;

public sealed class LightstepOptions
{
    public string ControllerIp { get; init; } = string.Empty;
    public int ControllerPort { get; init; }
    public bool AutoReconnect { get; init; }
    public int ReconnectDelayMs { get; init; }
}
