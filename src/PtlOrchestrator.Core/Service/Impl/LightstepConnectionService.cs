using AioiSystems.Lightstep;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Configuration;
using PtlOrchestrator.Domain;

namespace PtlOrchestrator.Service;

public sealed class LightstepConnectionService(
    IOptions<LightstepOptions> options,
    ILogger<LightstepConnectionService> logger
) : ILightstepConnectionService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly LightstepOptions _options = options.Value;
    private readonly ILogger<LightstepConnectionService> _logger = logger;
    private readonly Dictionary<PtlEndpoint, EthernetController> _pool = [];
    private readonly object _gate = new();

    public bool IsConnected
    {
        get
        {
            lock (_gate)
                return _pool.Count > 0 && _pool.Values.All(c => c.IsConnected);
        }
    }

    public bool IsConnectedTo(PtlEndpoint endpoint)
    {
        lock (_gate)
            return _pool.TryGetValue(endpoint, out var controller) && controller.IsConnected;
    }

    public Task EnsureConnectedAsync(CancellationToken cancellationToken)
        => EnsureConnectedAsync(DefaultEndpoint(), cancellationToken);

    public async Task EnsureConnectedAsync(PtlEndpoint endpoint, CancellationToken cancellationToken)
    {
        while (!IsConnectedTo(endpoint))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryConnectAsync(endpoint, cancellationToken))
                return;

            _logger.LogWarning("Connessione al Controller PTL {Endpoint} fallita. Nuovo tentativo tra {Seconds} secondi...",
                endpoint, RetryDelay.TotalSeconds);
            await Task.Delay(RetryDelay, cancellationToken);
        }
    }

    public Task<bool> TryConnectAsync(PtlEndpoint endpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsConnectedTo(endpoint))
            return Task.FromResult(true);

        _logger.LogInformation("Tentativo connessione al Controller PTL {Endpoint}", endpoint);

        try
        {
            var controller = new EthernetController();
            controller.SetLicense(_options.LicenzeKey);
            controller.Connect(endpoint.Ip, endpoint.Port);

            lock (_gate)
                _pool[endpoint] = controller;

            _logger.LogInformation("Controller PTL {Endpoint} connesso", endpoint);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Controller PTL {Endpoint} non raggiungibile", endpoint);
            return Task.FromResult(false);
        }
    }

    public EthernetController GetController(PtlEndpoint? endpoint = null)
    {
        var target = endpoint ?? DefaultEndpoint();

        lock (_gate)
        {
            if (_pool.TryGetValue(target, out var controller) && controller.IsConnected)
                return controller;
        }

        throw new InvalidOperationException($"Controller PTL {target} non connesso");
    }

    public void Disconnect()
    {
        List<PtlEndpoint> endpoints;
        lock (_gate)
            endpoints = [.. _pool.Keys];

        foreach (var endpoint in endpoints)
            Disconnect(endpoint);
    }

    public void Disconnect(PtlEndpoint endpoint)
    {
        EthernetController? controller;
        lock (_gate)
        {
            if (!_pool.Remove(endpoint, out controller))
                return;
        }

        _logger.LogInformation("Disconnessione Controller PTL {Endpoint}", endpoint);

        _ = Task.Run(() =>
        {
            try
            {
                controller.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Errore durante disconnessione Controller PTL {Endpoint}", endpoint);
            }
        });
    }

    private PtlEndpoint DefaultEndpoint()
    {
        if (string.IsNullOrWhiteSpace(_options.ControllerIp) || _options.ControllerPort <= 0)
            throw new InvalidOperationException("Nessun controller PTL predefinito configurato");

        return new PtlEndpoint(_options.ControllerIp, _options.ControllerPort);
    }
}
