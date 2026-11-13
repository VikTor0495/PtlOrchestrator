namespace PtlOrchestrator.Service;

/// <summary>
/// Ricorda quali carrelli la postazione ha attivi, così sopravvivono a una chiusura o a un riavvio
/// (ad esempio dopo il salvataggio delle impostazioni): un carrello si scollega solo se l'operatore lo fa.
/// </summary>
public interface IActiveCartStore
{
    /// <summary>Sostituisce l'elenco salvato con i carrelli attivi in questo momento.</summary>
    Task SaveAsync(IReadOnlyCollection<string> cartCodes, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken);
}
