using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PtlOrchestrator.Domain.Operations;

namespace PtlOrchestrator.Service.Impl;

/// <summary>
/// Legge continuamente la sorgente dei codici e li passa al dispatcher uno alla volta.
/// Mantiene la semantica del vecchio Worker: mentre un'operazione è in corso le nuove
/// scansioni non vengono accodate ma scartate come "sistema occupato".
/// </summary>
public sealed class ScanListener(
    IBarcodeSource barcodeSource,
    IScanDispatcher scanDispatcher,
    IAppProcessingState appProcessingState,
    ILogger<ScanListener> logger) : BackgroundService, IScanActivity
{
    private readonly IBarcodeSource _barcodeSource = barcodeSource;
    private readonly IScanDispatcher _scanDispatcher = scanDispatcher;
    private readonly IAppProcessingState _appProcessingState = appProcessingState;
    private readonly ILogger<ScanListener> _logger = logger;

    private readonly Channel<string> _pendingScans =
        Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });

    public event EventHandler<ScanProcessedEventArgs>? ScanProcessed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(
            Task.Run(() => ReadScansAsync(stoppingToken), stoppingToken),
            Task.Run(() => ProcessScansAsync(stoppingToken), stoppingToken));
    }

    private async Task ReadScansAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var scannedCode in _barcodeSource.ReadAsync(cancellationToken))
            {
                if (_appProcessingState.IsBusy || !_pendingScans.Writer.TryWrite(scannedCode))
                {
                    _logger.LogWarning("Scansione '{Code}' ignorata: sistema occupato", scannedCode);
                    Publish(scannedCode, ScanOutcome.SystemBusy());
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Lettura scansioni interrotta");
        }
        finally
        {
            _pendingScans.Writer.TryComplete();
        }
    }

    private async Task ProcessScansAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var scannedCode in _pendingScans.Reader.ReadAllAsync(cancellationToken))
            {
                var outcome = await _scanDispatcher.DispatchAsync(scannedCode, cancellationToken);
                Publish(scannedCode, outcome);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Elaborazione scansioni interrotta");
        }
    }

    private void Publish(string scannedCode, ScanOutcome outcome)
    {
        try
        {
            ScanProcessed?.Invoke(this, new ScanProcessedEventArgs(scannedCode, outcome));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Un osservatore delle scansioni ha sollevato un'eccezione");
        }
    }
}
