namespace PtlController.Output;

/// <summary>
/// Interface per il controller delle luci TCP
/// </summary>
public interface ITcpLightController
{
    /// <summary>
    /// Invia il comando per accendere la luce al carrello specificato
    /// </summary>
    Task<bool> SendLightOnCommandAsync(int cartIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invia un comando custom a un carrello specifico
    /// </summary>
    Task<bool> SendCustomCommandAsync(int cartIndex, string customCommand, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test di connettività: verifica se un carrello è raggiungibile
    /// </summary>
    Task<bool> TestConnectionAsync(int cartIndex, CancellationToken cancellationToken = default);
}
