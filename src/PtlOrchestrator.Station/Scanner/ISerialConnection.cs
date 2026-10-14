using PtlOrchestrator.Station.Configuration;

namespace PtlOrchestrator.Station.Scanner;

/// <summary>Una porta seriale aperta. Si usa un'astrazione per poter provare lo scanner senza hardware.</summary>
public interface ISerialConnection : IDisposable
{
    /// <summary>Legge i byte disponibili; restituisce 0 se la porta è stata chiusa o scollegata.</summary>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}

public interface ISerialConnectionFactory
{
    /// <summary>Apre la porta; solleva un'eccezione se non è disponibile.</summary>
    ISerialConnection Open(SerialScannerOptions options);
}
