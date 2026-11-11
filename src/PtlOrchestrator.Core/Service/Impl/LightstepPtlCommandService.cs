using AioiSystems.Lightstep;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Service;
using PtlOrchestrator.Builder;

namespace PtlOrchestrator.Service.Impl;

public sealed class LightstepPtlCommandService(
    ILightstepConnectionService connectionService,
    ILogger<LightstepPtlCommandService> logger)
    : IPtlCommandService
{
    private readonly ILightstepConnectionService _connectionService = connectionService;
    private readonly ILogger<LightstepPtlCommandService> _logger = logger;

    public async Task SendRawAsync(string command, PtlEndpoint? endpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (endpoint is null)
            await _connectionService.EnsureConnectedAsync(cancellationToken);
        else
            await _connectionService.EnsureConnectedAsync(endpoint, cancellationToken);

        _logger.LogDebug(
            "Invio comando -> '{Command}' a {Endpoint}",
            command,
            endpoint?.ToString() ?? "predefinito");

        TryToSendCommand(command, endpoint);
    }

    public async Task<string> WaitForButtonAsync(string expectedModule, PtlEndpoint? endpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (endpoint is null)
            await _connectionService.EnsureConnectedAsync(cancellationToken);
        else
            await _connectionService.EnsureConnectedAsync(endpoint, cancellationToken);

        var controller = _connectionService.GetController(endpoint);

        _logger.LogDebug("In attesa conferma da modulo {Module}", expectedModule);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cmd = controller.GetCommand();

            if (cmd != null)
                return ExtractSourceModule(cmd) ?? string.Empty;

            await Task.Delay(50, cancellationToken);
        }
    }

    private static string? ExtractSourceModule(CommandInfo cmd)
    {
        var addresses = AddressInfo.SplitCommand(cmd.GetCommandText());

        return addresses.FirstOrDefault()?.Address;
    }

    private void TryToSendCommand(string command, PtlEndpoint? endpoint)
    {
        var controller = _connectionService.GetController(endpoint);

        CommandInfo response;

        var sendSw = Stopwatch.StartNew();
        try
        {
            response = controller.SendCommand(command);
        }
        catch (NakReceivedException ex)
        {
            throw new InvalidOperationException("PTL ha risposto con NAK", ex);
        }
        catch (CheckSumErrorException ex)
        {
            throw new InvalidOperationException("Errore checksum PTL", ex);
        }
        catch (DuplicatedPacketException ex)
        {
            throw new InvalidOperationException("Pacchetto duplicato PTL", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Errore generico comunicazione PTL", ex);
        }

        ValidateResponse(response);
    }

    private static void ValidateResponse(CommandInfo response)
    {
        if (response == null)
            throw new InvalidOperationException("Risposta NULL dal controller PTL");
    }
}
