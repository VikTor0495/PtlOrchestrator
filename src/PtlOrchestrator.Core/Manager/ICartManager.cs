namespace PtlOrchestrator.Manager;

using PtlOrchestrator.Domain;


public interface ICartManager
{
    Task<CartAssignmentResult> ProcessBarcode(string barcode, CancellationToken cancellationToken);

    /// <summary>True se il codice letto è un carrello del catalogo.</summary>
    bool IsKnownCart(string code);

    /// <summary>Occupa un carrello sul server e lo attiva nel turno.</summary>
    Task<CartCheckInOutcome> CheckInAsync(string cartCode, CancellationToken cancellationToken);

    /// <summary>Libera un carrello vuoto: lo rimuove dal turno e dal server.</summary>
    Task<CartReleaseOutcome> ReleaseCartAsync(string cartCode, CancellationToken cancellationToken);

    IReadOnlyList<string> ActiveCartIds();

    bool HasItems();

    /// <summary>Chiude lo sventagliamento: genera il CSV e azzera i carrelli, che restano attivi.</summary>
    Task CloseWorkAsync(CancellationToken cancellationToken);

    Task ResetAll(CancellationToken cancellationToken);

    void ShowStatus();

    void WriteCsvReport();
}
