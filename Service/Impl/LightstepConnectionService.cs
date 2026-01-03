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

    private readonly object _sync = new();
    
    private EthernetController? _controller;

    public bool IsConnected => _controller?.IsConnected == true;

    public async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (IsConnected)
            return;

        await Task.Run(() =>
        {
            lock (_sync)
            {
                if (IsConnected)
                    return;

                ct.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "Connessione Lightstep a {Ip}:{Port}",
                    _options.ControllerIp,
                    _options.ControllerPort);

                var controller = new EthernetController();
                controller.SetLicense(_options.LicenzeKey);
                controller.Connect(
                    _options.ControllerIp,
                    _options.ControllerPort);

                _controller = controller;

                _logger.LogInformation("Lightstep connesso correttamente");
            }
        }, ct);
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
        lock (_sync)
        {
            if (_controller == null)
                return;

            _logger.LogInformation("Disconnessione Lightstep");
            _controller.Close();
            _controller = null;
        }
    }
}
