namespace PtlController.Configuration;

/// <summary>
/// Configurazione dei carrelli e parametri TCP
/// </summary>
public class CartsOptions
{
    /// <summary>
    /// Numero totale di carrelli gestiti
    /// </summary>
    public int NumberOfCarts { get; set; }

    /// <summary>
    /// Array degli indirizzi IP dei carrelli
    /// L'indice dell'array corrisponde all'ID del carrello (0-based)
    /// </summary>
    public string[] CartIpAddresses { get; set; } = [];

    /// <summary>
    /// Porta TCP su cui i carrelli sono in ascolto
    /// </summary>
    public int TcpPort { get; set; }

    /// <summary>
    /// Comando da inviare per accendere la luce
    /// </summary>
    public string LightOnCommand { get; set; } = "LIGHT_ON";

    /// <summary>
    /// Timeout in millisecondi per la connessione TCP
    /// </summary>
    public int TcpTimeoutMs { get; set; } = 3000;
}
