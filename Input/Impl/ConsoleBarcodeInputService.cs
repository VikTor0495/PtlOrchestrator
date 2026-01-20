using PtlOrchestrator.Service;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Enum;
using Microsoft.Extensions.Logging;

namespace PtlOrchestrator.Input.Impl;

public class ConsoleBarcodeInputService(ILogger<ConsoleBarcodeInputService> logger) : IBarcodeInputService
{
    private readonly ILogger<ConsoleBarcodeInputService> _logger = logger;


    public Task<string> ReadInputAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Barcode > ");
        var input = Console.ReadLine();

        return Task.FromResult(input?.Trim() ?? "");
    }
}
