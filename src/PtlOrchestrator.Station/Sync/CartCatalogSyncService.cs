using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PtlOrchestrator.Station.Configuration;

namespace PtlOrchestrator.Station.Sync;

/// <summary>Tiene aggiornato il catalogo dei codici carrello: serve a riconoscere un carrello alla scansione.</summary>
public sealed class CartCatalogSyncService(
    CentralCartAssignmentClient client,
    Microsoft.Extensions.Options.IOptions<CentralOptions> options,
    ILogger<CartCatalogSyncService> logger) : BackgroundService
{
    private readonly CentralCartAssignmentClient _client = client;
    private readonly CentralOptions _options = options.Value;
    private readonly ILogger<CartCatalogSyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.CartCatalogSyncIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _client.RefreshCatalogAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // il catalogo precedente resta valido: un server momentaneamente spento non blocca il turno
                _logger.LogWarning("Aggiornamento del catalogo carrelli non riuscito: {Message}", ex.Message);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
