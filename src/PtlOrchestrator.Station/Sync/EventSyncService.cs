using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Contracts.Operations;
using PtlOrchestrator.Station.Configuration;
using PtlOrchestrator.Station.Storage;

namespace PtlOrchestrator.Station.Sync;

/// <summary>
/// Svuota periodicamente l'outbox verso il server centrale. La consegna è at-least-once: un evento
/// rimane in coda finché il server non lo conferma (il server ignora i duplicati per Id).
/// </summary>
public sealed class EventSyncService(
    IOutbox outbox,
    ICentralClient centralClient,
    IOptions<CentralOptions> options,
    TimeProvider timeProvider,
    ILogger<EventSyncService> logger) : BackgroundService
{
    private readonly IOutbox _outbox = outbox;
    private readonly ICentralClient _centralClient = centralClient;
    private readonly CentralOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<EventSyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.EventSyncIntervalSeconds));

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
                // un guasto imprevisto (es. database locale) non deve fermare per sempre la sincronizzazione
                _logger.LogError(ex, "Errore imprevisto nella sincronizzazione degli eventi");
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

    /// <summary>
    /// Invia gli eventi in coda a batch, finché la coda è vuota o il server non è raggiungibile.
    /// Restituisce quanti eventi sono stati consegnati.
    /// </summary>
    public async Task<int> SyncOnceAsync(CancellationToken cancellationToken)
    {
        var batchSize = Math.Max(1, _options.EventBatchSize);
        var delivered = 0;

        while (true)
        {
            var batch = await _outbox.PeekBatchAsync(batchSize, cancellationToken);
            if (batch.Count == 0)
                return delivered;

            var result = await _centralClient.SendEventsAsync(batch, cancellationToken);

            if (result.IsSuccess)
            {
                await _outbox.RemoveAsync(batch.Select(e => e.Id).ToList(), cancellationToken);
                delivered += batch.Count;
                continue;
            }

            if (result.Failure == CentralFailure.Transient)
            {
                _logger.LogWarning("Server centrale non disponibile, {Count} eventi restano in coda: {Reason}",
                    batch.Count, result.Message);
                await _outbox.RecordFailureAsync(batch.Select(e => e.Id).ToList(), result.Message ?? "errore", cancellationToken);
                return delivered;
            }

            // rifiuto definitivo: il server non dice quale evento è il colpevole, quindi si isolano uno a uno
            var outcome = await IsolateRejectedAsync(batch, cancellationToken);
            delivered += outcome.Delivered;

            if (outcome.ServerUnavailable)
                return delivered;
        }
    }

    private async Task<(int Delivered, bool ServerUnavailable)> IsolateRejectedAsync(
        IReadOnlyList<OperationEvent> batch, CancellationToken cancellationToken)
    {
        var delivered = 0;

        foreach (var operationEvent in batch)
        {
            var result = await _centralClient.SendEventsAsync([operationEvent], cancellationToken);

            if (result.IsSuccess)
            {
                await _outbox.RemoveAsync([operationEvent.Id], cancellationToken);
                delivered++;
            }
            else if (result.Failure == CentralFailure.Rejected)
            {
                _logger.LogError("Evento {EventId} rifiutato dal server e messo in quarantena: {Reason}",
                    operationEvent.Id, result.Message);
                await _outbox.QuarantineAsync([operationEvent.Id], result.Message ?? "rifiutato", cancellationToken);
            }
            else
            {
                await _outbox.RecordFailureAsync([operationEvent.Id], result.Message ?? "errore", cancellationToken);
                return (delivered, true);
            }
        }

        return (delivered, false);
    }
}
