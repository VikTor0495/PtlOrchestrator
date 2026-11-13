using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PtlOrchestrator.Service.Impl;

namespace PtlOrchestrator.Station.Sync;

/// <summary>
/// All'avvio riprende i carrelli che la postazione aveva prima della chiusura. Se il server o un controller
/// non sono ancora raggiungibili riprova per un po', poi lascia perdere quelli non ripresi.
/// </summary>
public sealed class CartRestoreService(
    ActiveCartRestorer restorer,
    ILogger<CartRestoreService> logger) : BackgroundService
{
    private const int MaxAttempts = 12;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly ActiveCartRestorer _restorer = restorer;
    private readonly ILogger<CartRestoreService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    if (await _restorer.RestoreAsync(stoppingToken) == 0)
                        return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Ripresa dei carrelli non riuscita (tentativo {Attempt}): {Message}", attempt, ex.Message);
                }

                await Task.Delay(RetryDelay, stoppingToken);
            }

            _logger.LogWarning("Alcuni carrelli non sono stati ripresi dopo {Attempts} tentativi: vanno rifatti con il check-in", MaxAttempts);
            await _restorer.AbandonPendingAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // chiusura della postazione durante l'attesa
        }
    }
}
