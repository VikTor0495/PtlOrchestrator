namespace PtlOrchestrator.Service;

/// <summary>
/// Sorgente dei codici letti (pistola seriale, tastiera, console...). Restituisce un codice
/// per elemento, già privo di terminatori.
/// </summary>
public interface IBarcodeSource
{
    IAsyncEnumerable<string> ReadAsync(CancellationToken cancellationToken);
}
