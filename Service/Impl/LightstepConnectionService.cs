using AioiSystems.Lightstep;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlController.Configuration;

namespace PtlController.Service;

public sealed class LightstepConnectionService(
    IOptions<LightstepOptions> options,
    ILogger<LightstepConnectionService> logger) : IDisposable
{
    private readonly LightstepOptions _options = options.Value;
    private readonly ILogger<LightstepConnectionService> _logger = logger;
    private EthernetController? _controller;


    public void Connect()
    {
        _logger.LogInformation(
            "Connessione al PTL controller {Ip}:{Port}",
            _options.ControllerIp,
            _options.ControllerPort);

        _controller = new EthernetController();
        _controller.SetLicense("XXXX-XXXX-XXXX-XXXX-XXXX");
        _controller.Connect(_options.ControllerIp, _options.ControllerPort);

        _logger.LogInformation("Connesso al PTL controller");
    }

    public void Disconnect()
    {
        _controller?.Close();
        _logger.LogInformation("Disconnesso dal PTL controller");
    }

    public void Dispose()
    {
        Disconnect();
    }

     // >>> NUOVO METODO <<<
    public void SendTestCommand()
    {
        if (_controller is null)
            throw new InvalidOperationException("Controller non connesso");

        var command = "TEST\r\n";

        _logger.LogInformation("Invio comando di test: {Command}", command.Trim());
        _controller.SendCommand(command);
    }
}
