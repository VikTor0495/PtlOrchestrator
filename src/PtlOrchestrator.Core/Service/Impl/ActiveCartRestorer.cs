using Microsoft.Extensions.Logging;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Manager;

namespace PtlOrchestrator.Service.Impl;

/// <summary>
/// Riprende i carrelli che la postazione aveva attivi prima della chiusura. Per il server il carrello è ancora
/// suo, quindi il check-in dello stesso carrello dalla stessa postazione ha successo senza altre verifiche.
/// </summary>
public sealed class ActiveCartRestorer(
    ICartManager cartManager,
    IActiveCartStore store,
    ILogger<ActiveCartRestorer> logger)
{
    private readonly ICartManager _cartManager = cartManager;
    private readonly IActiveCartStore _store = store;
    private readonly ILogger<ActiveCartRestorer> _logger = logger;

    /// <summary>Prova a riprendere i carrelli salvati. Restituisce quanti non è ancora riuscito a riprendere.</summary>
    public async Task<int> RestoreAsync(CancellationToken cancellationToken)
    {
        var saved = await _store.LoadAsync(cancellationToken);
        var active = _cartManager.ActiveCartIds();
        var pending = 0;

        foreach (var cartCode in saved.Where(code => !active.Contains(code)))
        {
            var outcome = await _cartManager.CheckInAsync(cartCode, cancellationToken);

            if (outcome.Success || outcome.Result == CartCheckInResult.AlreadyActive)
            {
                _logger.LogInformation("Carrello {CartCode} ripreso dopo la riapertura", cartCode);
                continue;
            }

            pending++;
            _logger.LogWarning("Carrello {CartCode} non ancora ripreso: {Message}", cartCode, outcome.Message);
        }

        return pending;
    }

    /// <summary>Dopo troppi tentativi si dimenticano i carrelli non ripresi (ad esempio ora sono di un'altra postazione).</summary>
    public Task AbandonPendingAsync(CancellationToken cancellationToken)
        => _store.SaveAsync(_cartManager.ActiveCartIds(), cancellationToken);
}
