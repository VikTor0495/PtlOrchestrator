using AioiSystems.Lightstep;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;

namespace PtlOrchestrator.Service;

public sealed class LightstepConnectionService(
    IOptions<LightstepOptions> options,
    ILogger<LightstepConnectionService> logger
) : ILightstepConnectionService
{
    private readonly LightstepOptions _options = options.Value;
    private readonly ILogger<LightstepConnectionService> _logger = logger;
    /* 
        private readonly object _sync = new(); */

    private EthernetController? _controller;

    public bool IsConnected => _controller?.IsConnected == true;

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
            return;

        EthernetController controller;

        while (!IsConnected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {

                if (IsConnected)
                    return;

                _logger.LogInformation(
                    "Tentativo connessione al Controller PTL {Ip}:{Port}",
                    _options.ControllerIp,
                    _options.ControllerPort);

                controller = new EthernetController();
                controller.SetLicense(_options.LicenzeKey);

                controller.Connect(
                    _options.ControllerIp,
                    _options.ControllerPort);

                _controller = controller;

                _logger.LogInformation("Controller PTL connesso correttamente");
                return;
            }
            catch (Exception)
            {
                _logger.LogWarning(
                    "Connessione al Controller PTL fallita. Nuovo tentativo tra 10 secondi...");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }

    public EthernetController GetController()
    {
        if (!IsConnected || _controller == null)
            throw new InvalidOperationException(
                "Controller PTL non connesso");

        return _controller;
    }

    public void Disconnect()
    {
        var controller = _controller;
        if (controller == null)
            return;

        _logger.LogInformation("Disconnessione Controller PTL");

        _controller = null;

        Task.Run(() =>
        {
            try
            {
                controller.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Errore durante disconnessione Controller PTL");
            }
        });
    }

}
