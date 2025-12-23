using AioiSystems.Lightstep;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlController.Configuration;

namespace PtlController.Service;

public sealed class LightstepConnectionService(
    ILogger<LightstepConnectionService> logger,
    IOptions<LightstepOptions> options)
{
    private readonly ILogger<LightstepConnectionService> _logger = logger;
    private readonly LightstepOptions _options = options.Value;

    private EthernetController? _controller;
    private TaskCompletionSource<bool>? _connectedTcs;

    public async Task EnsureConnectedAsync(
        TimeSpan timeout,
        CancellationToken ct)
    {
        if (_controller != null)
        {
            _logger.LogDebug("Lightstep già connesso");
            return;
        }

        _connectedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _logger.LogInformation(
            "Connessione Lightstep a {Ip}:{Port}",
            _options.ControllerIp,
            _options.ControllerPort);

        try
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                _controller = new EthernetController();
                _controller.SetLicense(_options.LicenzeKey);
                _controller.Connect(
                    _options.ControllerIp,
                    _options.ControllerPort);

                _connectedTcs.TrySetResult(true);
            }, ct);

            await _connectedTcs.Task.WaitAsync(timeout, ct);

            _logger.LogInformation("Lightstep connesso correttamente");
        }
        catch
        {
            _connectedTcs.TrySetException(
                new TimeoutException("Connessione Lightstep fallita"));

            _controller = null;
            throw;
        }
    }

    public void Disconnect()
    {
        if (_controller == null)
            return;

        _logger.LogInformation("Disconnessione Lightstep");
        _controller.Close();
        _controller = null;
    }
}
