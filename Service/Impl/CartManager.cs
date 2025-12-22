using PtlController.Configuration;
using PtlController.Output;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PtlController.Service.Impl;


public class CartManager : ICartManager
{
    private readonly ILogger<CartManager> _logger;
    private readonly CartsOptions _config;
    private readonly ITcpLightController _tcpController;
    
    // Dizionario: barcode -> indice carrello (0-based)
    private readonly Dictionary<string, int> _barcodeToCart = [];
    
    // Contatore prodotti per carrello (per bilanciamento)
    private readonly int[] _cartItemCount;
    
    // Round-robin: prossimo carrello da assegnare
    private int _nextCartIndex = 0;
    
    // Lock per thread safety
    private readonly object _lock = new();

    public CartManager(
        ILogger<CartManager> logger,
        IOptions<CartsOptions> config,
        ITcpLightController tcpController)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tcpController = tcpController ?? throw new ArgumentNullException(nameof(tcpController));
        
        if (config?.Value == null)
            throw new ArgumentNullException(nameof(config));
        
        _config = config.Value;
        _cartItemCount = new int[_config.CartIpAddresses?.Length ?? 0];

        _logger.LogInformation("CartManager inizializzato con {CartCount} carrelli", 
            _config.CartIpAddresses?.Length ?? 0);
    }

    /// <inheritdoc/>
    public async Task ProcessBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            _logger.LogWarning("Barcode vuoto o non valido ricevuto");
            throw new ArgumentException("Barcode non valido", nameof(barcode));
        }

        int cartIndex;
        bool isNewBarcode;

        lock (_lock)
        {
            // Controlla se il barcode è già assegnato
            if (_barcodeToCart.TryGetValue(barcode, out cartIndex))
            {
                isNewBarcode = false;
                _logger.LogDebug("Barcode esistente: {Barcode} -> Carrello {CartNumber}", 
                    barcode, cartIndex + 1);
            }
            else
            {
                // Nuovo barcode: assegna al prossimo carrello (round-robin)
                cartIndex = _nextCartIndex;
                _barcodeToCart[barcode] = cartIndex;
                _cartItemCount[cartIndex]++;
                
                // Avanza al prossimo carrello
                _nextCartIndex = (_nextCartIndex + 1) % _config.CartIpAddresses.Length;
                isNewBarcode = true;
                
                _logger.LogInformation("Nuovo barcode registrato: {Barcode} -> Carrello {CartNumber}", 
                    barcode, cartIndex + 1);
            }
        }

        // Invia comando TCP per accendere la luce
        var success = await _tcpController.SendLightOnCommandAsync(cartIndex, cancellationToken);

        // Log risultato
        if (success)
        {
            if (isNewBarcode)
            {
                _logger.LogInformation("✓ NUOVO prodotto '{Barcode}' → Carrello {CartNumber} ({IpAddress})", 
                    barcode, cartIndex + 1, _config.CartIpAddresses[cartIndex]);
            }
            else
            {
                _logger.LogInformation("✓ Prodotto ESISTENTE '{Barcode}' → Carrello {CartNumber} ({IpAddress})", 
                    barcode, cartIndex + 1, _config.CartIpAddresses[cartIndex]);
            }
        }
        else
        {
            _logger.LogError("❌ ERRORE nell'invio comando al Carrello {CartNumber} ({IpAddress})", 
                cartIndex + 1, _config.CartIpAddresses[cartIndex]);
        }
    }

    /// <inheritdoc/>
    public void ShowStatus()
    {
        lock (_lock)
        {
            _logger.LogInformation("");
            _logger.LogInformation("═══════════════════ STATO CARRELLI ═══════════════════");
            _logger.LogInformation("");

            for (int i = 0; i < _config.CartIpAddresses.Length; i++)
            {
                var itemCount = _cartItemCount[i];
                _logger.LogInformation("Carrello {CartNumber} ({IpAddress}): {ItemCount} prodotti", 
                    i + 1, _config.CartIpAddresses[i], itemCount);

                // Mostra i barcode di questo carrello
                var barcodesInCart = _barcodeToCart
                    .Where(kvp => kvp.Value == i)
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (barcodesInCart.Any())
                {
                    _logger.LogInformation("  └─ Barcode: {Barcodes}", 
                        string.Join(", ", barcodesInCart));
                }
                _logger.LogInformation("");
            }

            _logger.LogInformation("Totale barcode unici registrati: {TotalBarcodes}", _barcodeToCart.Count);
            _logger.LogInformation("Prossimo carrello da assegnare: {NextCart}", _nextCartIndex + 1);
            _logger.LogInformation("");
            _logger.LogInformation("═══════════════════════════════════════════════════════");
            _logger.LogInformation("");
        }
    }

    /// <inheritdoc/>
    public void ResetAll()
    {
        lock (_lock)
        {
            var previousCount = _barcodeToCart.Count;
            _barcodeToCart.Clear();
            Array.Clear(_cartItemCount, 0, _cartItemCount.Length);
            _nextCartIndex = 0;
            
            _logger.LogWarning("Reset completato: {BarcodeCount} barcode cancellati", previousCount);
        }
    }

    /// <inheritdoc/>
    public int? GetCartNumberForBarcode(string barcode)
    {
        lock (_lock)
        {
            if (_barcodeToCart.TryGetValue(barcode, out int cartIndex))
            {
                return cartIndex + 1; // Ritorna numero 1-based
            }
            return null;
        }
    }

    /// <inheritdoc/>
    public bool IsConfigurationValid()
    {
        var isValid = _config.NumberOfCarts > 0 &&
               _config.CartIpAddresses != null &&
               _config.CartIpAddresses.Length == _config.NumberOfCarts &&
               _config.TcpPort > 0 &&
               !string.IsNullOrWhiteSpace(_config.LightOnCommand);

        if (!isValid)
        {
            _logger.LogError("Configurazione non valida: NumberOfCarts={NumberOfCarts}, " +
                "CartIpAddresses.Length={IpCount}, TcpPort={TcpPort}, LightOnCommand={Command}",
                _config.NumberOfCarts,
                _config.CartIpAddresses?.Length ?? 0,
                _config.TcpPort,
                _config.LightOnCommand);
        }

        return isValid;
    }
}
