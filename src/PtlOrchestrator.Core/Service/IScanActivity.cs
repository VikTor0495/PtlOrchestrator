using PtlOrchestrator.Domain.Operations;

namespace PtlOrchestrator.Service;

public interface IScanActivity
{
    /// <summary>Sollevato per ogni scansione elaborata, anche se scartata perché il sistema è occupato.</summary>
    event EventHandler<ScanProcessedEventArgs>? ScanProcessed;
}

public sealed class ScanProcessedEventArgs(string scannedCode, ScanOutcome outcome) : EventArgs
{
    public string ScannedCode { get; } = scannedCode;

    public ScanOutcome Outcome { get; } = outcome;
}
