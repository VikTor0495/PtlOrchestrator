using System.Text;
using Microsoft.Extensions.Logging;

namespace PtlOrchestrator.Input.Impl;

public class ConsoleBarcodeInputService(ILogger<ConsoleBarcodeInputService> logger) : IBarcodeInputService
{
    private readonly ILogger<ConsoleBarcodeInputService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));


    public Task<string?> ReadInputAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Console.Write("Barcode > ");

        var input = Console.ReadLine();

        return Task.FromResult(input?.Trim());
    } 
}
