using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Storage;

namespace PtlOrchestrator.Station.Sync;

/// <summary>
/// Tiene aggiornata la cache locale degli operatori scaricando dal server solo le modifiche
/// successive all'ultima sincronizzazione. Senza rete la cache resta quella dell'ultimo sync riuscito.
/// </summary>
public sealed class OperatorSyncService(
    IOperatorCache operatorCache,
    ICentralClient centralClient,
    IOptions<CentralOptions> options,
    TimeProvider timeProvider,
    ILogger<OperatorSyncService> logger) : BackgroundService
{
    private readonly IOperatorCache _operatorCache = operatorCache;
    private readonly ICentralClient _centralClient = centralClient;
    private readonly CentralOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<OperatorSyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.OperatorSyncIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore imprevisto nella sincronizzazione degli operatori");
            }

            try
            {
                await Task.Delay(interval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Esegue una sincronizzazione; restituisce quanti operatori sono stati aggiornati, o null se il server non ha risposto.</summary>
    public async Task<int?> SyncOnceAsync(CancellationToken cancellationToken)
    {
        var since = await _operatorCache.GetLastSyncAsync(cancellationToken);

        var result = await _centralClient.GetOperatorsSnapshotAsync(since, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Sincronizzazione operatori non riuscita, resta la cache locale: {Reason}", result.Message);
            return null;
        }

        var snapshot = result.Value!;

        // prima gli operatori, poi il marcatore: se ci si ferma a metà, al giro dopo si riscarica lo stesso intervallo
        await _operatorCache.UpsertAsync(snapshot.Operators.ToList(), cancellationToken);
        await _operatorCache.SetLastSyncAsync(snapshot.ServerTime, cancellationToken);

        if (snapshot.Operators.Count > 0)
            _logger.LogInformation("Operatori aggiornati dal server: {Count}", snapshot.Operators.Count);

        return snapshot.Operators.Count;
    }
}
