using System.IO.Ports;

namespace PtlOrchestrator.Station.Configuration;

public sealed class LocalDataOptions
{
    /// <summary>Percorso del database locale; se relativo parte dalla cartella dell'applicazione.</summary>
    public string DatabasePath { get; set; } = Path.Combine("data", "station.db");
}

public sealed class CentralOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
    public int EventSyncIntervalSeconds { get; set; } = 5;
    public int OperatorSyncIntervalSeconds { get; set; } = 60;
    public int EventBatchSize { get; set; } = 200;
}

public sealed class SerialScannerOptions
{
    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public int ReconnectSeconds { get; set; } = 5;
    public int MaxCodeLength { get; set; } = 128;
}
