using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlController.Configuration;
using System.Net.Sockets;
using System.Text;

namespace PtlController.Output.Impl;

/// <summary>
/// Gestisce la comunicazione TCP con le lampadine dei carrelli
/// </summary>
public class TcpLightController : ITcpLightController
{
    private readonly ILogger<TcpLightController> _logger;
    private readonly CartsOptions _config;

    public TcpLightController(
        ILogger<TcpLightController> logger,
        IOptions<CartsOptions> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (config?.Value == null)
            throw new ArgumentNullException(nameof(config));
        
        _config = config.Value;
    }

    /// <inheritdoc/>
    public async Task<bool> SendLightOnCommandAsync(int cartIndex, CancellationToken cancellationToken = default)
    {
        if (cartIndex < 0 || cartIndex >= _config.CartIpAddresses.Length)
        {
            _logger.LogError("Indice carrello non valido: {CartIndex}. Deve essere tra 0 e {MaxIndex}", 
                cartIndex, _config.CartIpAddresses.Length - 1);
            throw new ArgumentOutOfRangeException(nameof(cartIndex));
        }

        var ipAddress = _config.CartIpAddresses[cartIndex];
        var port = _config.TcpPort;
        var command = _config.LightOnCommand;

        return await SendTcpCommandAsync(ipAddress, port, command, cartIndex, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SendCustomCommandAsync(int cartIndex, string customCommand, CancellationToken cancellationToken = default)
    {
        if (cartIndex < 0 || cartIndex >= _config.CartIpAddresses.Length)
        {
            _logger.LogError("Indice carrello non valido: {CartIndex}", cartIndex);
            throw new ArgumentOutOfRangeException(nameof(cartIndex));
        }

        if (string.IsNullOrWhiteSpace(customCommand))
        {
            _logger.LogError("Comando custom vuoto");
            throw new ArgumentException("Comando non valido", nameof(customCommand));
        }

        var ipAddress = _config.CartIpAddresses[cartIndex];
        var port = _config.TcpPort;

        return await SendTcpCommandAsync(ipAddress, port, customCommand, cartIndex, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(int cartIndex, CancellationToken cancellationToken = default)
    {
        if (cartIndex < 0 || cartIndex >= _config.CartIpAddresses.Length)
        {
            return false;
        }

        var ipAddress = _config.CartIpAddresses[cartIndex];
        var port = _config.TcpPort;

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.TcpTimeoutMs);

            await client.ConnectAsync(ipAddress, port, cts.Token);
            
            _logger.LogDebug("Test connessione riuscito a {IpAddress}:{Port}", ipAddress, port);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Test connessione timeout a {IpAddress}:{Port}", ipAddress, port);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test connessione fallito a {IpAddress}:{Port}", ipAddress, port);
            return false;
        }
    }

    /// <summary>
    /// Metodo privato per inviare comandi TCP
    /// </summary>
    private async Task<bool> SendTcpCommandAsync(
        string ipAddress, 
        int port, 
        string command, 
        int cartIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.TcpTimeoutMs);

            // Connessione con timeout
            _logger.LogDebug("Connessione a {IpAddress}:{Port}...", ipAddress, port);
            await client.ConnectAsync(ipAddress, port, cts.Token);

            // Invia il comando
            using var stream = client.GetStream();
            var commandBytes = Encoding.UTF8.GetBytes(command);
            await stream.WriteAsync(commandBytes, 0, commandBytes.Length, cts.Token);

            _logger.LogDebug("→ TCP inviato a Carrello {CartNumber} ({IpAddress}:{Port}) - Comando: '{Command}'", 
                cartIndex + 1, ipAddress, port, command);

            // Opzionale: leggi risposta se il dispositivo risponde
            // var buffer = new byte[1024];
            // var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
            // var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            // _logger.LogDebug("← Risposta da Carrello {CartNumber}: {Response}", cartIndex + 1, response);

            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("⚠ Timeout connessione a Carrello {CartNumber} ({IpAddress}:{Port})", 
                cartIndex + 1, ipAddress, port);
            return false;
        }
        catch (SocketException ex)
        {
            _logger.LogWarning("⚠ Errore socket verso Carrello {CartNumber} ({IpAddress}:{Port}) - {Message}", 
                cartIndex + 1, ipAddress, port, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Errore TCP verso Carrello {CartNumber} ({IpAddress}:{Port})", 
                cartIndex + 1, ipAddress, port);
            return false;
        }
    }
}
