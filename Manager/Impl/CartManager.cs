using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Domain.Enum;
using PtlOrchestrator.Service;
using PtlOrchestrator.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Builder;
using PtlOrchestrator.Domain.File;

namespace PtlOrchestrator.Manager.Impl;

public sealed class CartManager(
    CartContainer cartContainer,
    IPtlCommandService ptlCommandService,
    ILogger<CartManager> logger) : ICartManager
{

    private readonly CartContainer _cartContainer = cartContainer;

    private readonly IPtlCommandService _ptlCommandService = ptlCommandService;

    private readonly List<WorkedProduct> _workedProducts = [];

    private readonly ILogger<CartManager> _logger = logger;

    private readonly object _lock = new();


    public CartAssignmentResult ProcessBarcode(string barcode)
    {
        CartAssignmentResult result;

        lock (_lock)
        {
            result = _cartContainer.AssignItem(barcode);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "RIFIUTATO barcode {Barcode}: {Reason}",
                    barcode,
                    result.Reason);
                return result;

            }

            _logger.LogInformation(
                  "ACCETTATO barcode {Barcode} → Cart {CartId} Basket {BasketId} ({Type})",
                  barcode,
                  result.Cart!.CartId,
                  result.Basket!.BasketId,
                  result.Type);

            try
            {
                SendAddCommandAndWaitConfirm(result);

                if (result.Basket.IsFull)
                {
                    SendFullBasketCommand(result);
                }

                AddCsvRecord(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Errore PTL su Cart {CartId}, Basket {BasketId} → rollback",
                    result.Cart.CartId,
                    result.Basket.BasketId);

                Rollback(result);

                throw;
            }


            return result;
        }
    }

    private void AddCsvRecord(CartAssignmentResult result)
    {
        if (result != null && result.Basket != null)
        {
            _workedProducts.Add(new WorkedProduct(
            DateTime.UtcNow,
            result.Cart!.CartId.ToString(),
            result.Basket.BasketId,
            result.Basket.Barcode!,
            result.Basket.CurrentQuantity
            ));
        }
    }

    private void SendFullBasketCommand(CartAssignmentResult result)
    {
        if (result != null && result.Basket != null)
        {
            _ptlCommandService.SendAsync(
                result.Basket.BasketId,
                PtlActivationCommandBuilder.BuildRedFixedActivation(result.Basket),
                CancellationToken.None
            ).GetAwaiter().GetResult();
        }
    }

    private void SendAddCommandAndWaitConfirm(CartAssignmentResult result)
    {
        if (result != null && result.Basket != null)
        {
            _ptlCommandService.SendAsync(result.Basket.BasketId,
                PtlActivationCommandBuilder.BuildGreenActivation(result.Basket),
                CancellationToken.None)
            .GetAwaiter().GetResult();

            _ptlCommandService.WaitForButtonAsync(
                result.Basket.BasketId,
                CancellationToken.None
            ).GetAwaiter().GetResult();

            _logger.LogInformation(
                "Conferma ricevuta per Cart {CartId}, Basket {BasketId}",
                result.Cart!.CartId,
                result.Basket.BasketId);
        }
    }

    public void ResetAll()
    {
        foreach (var basket in _cartContainer.GetCarts().SelectMany(c => c.Baskets))
        {
            _ptlCommandService.SendAsync(
                basket.BasketId,
                PtlActivationCommandBuilder.BuildOffActivation(),
                CancellationToken.None
            ).GetAwaiter().GetResult();
        }

        _cartContainer.ResetAll();

        _workedProducts.Clear();

        _logger.LogInformation("Reset completo di tutti i carrelli");
    }

    public void ShowStatus()
    {
        _cartContainer.ShowStatus();
    }

    private void Rollback(CartAssignmentResult assignment)
    {
        _cartContainer.Rollback(assignment);
    }

    public void WriteCsvReport()
    {
        if (_workedProducts.Count == 0)
            return;
        try
        {
            _logger .LogInformation("Generazione report CSV...");

            var baseDir = AppContext.BaseDirectory;
            var reportDir = Path.Combine(baseDir, "report");

            Directory.CreateDirectory(reportDir);

            var fileName = $"ptl-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var filePath = Path.Combine(reportDir, fileName);
            using var writer = new StreamWriter(new FileStream(filePath, FileMode.Create), System.Text.Encoding.UTF8);

            writer.WriteLine("Timestamp,CartId,BasketId,Barcode,Quantity");

            foreach (var r in _workedProducts)
            {
                writer.WriteLine(
                    $"{r.Timestamp:O}," +
                    $"{r.CartId}," +
                    $"{r.BasketId}," +
                    $"{r.Barcode}," +
                    $"{r.Quantity}");
            }
        } catch (Exception)
        {
            _logger.LogError("Errore durante la generazione del report CSV");
        }


    }
}