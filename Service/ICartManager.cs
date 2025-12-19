namespace PtlController.Service;

/// <summary>
/// Interface per il gestore dei carrelli
/// </summary>
public interface ICartManager
{
    /// <summary>
    /// Processa un barcode: assegna al carrello e accende la luce
    /// </summary>
    Task ProcessBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mostra lo stato di tutti i carrelli
    /// </summary>
    void ShowStatus();

    /// <summary>
    /// Reset di tutti i carrelli (cancella tutti i dati in memoria)
    /// </summary>
    void ResetAll();

    /// <summary>
    /// Ottiene il numero di carrello per un barcode specifico (1-based)
    /// </summary>
    int? GetCartNumberForBarcode(string barcode);

    /// <summary>
    /// Verifica se la configurazione è valida
    /// </summary>
    bool IsConfigurationValid();
}
