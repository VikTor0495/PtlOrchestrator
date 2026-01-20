using AioiSystems.Lightstep;
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


    public async Task SendRawAsync(string moduleAddress, string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _connectionService.EnsureConnectedAsync(cancellationToken);

        _logger.LogDebug(
            "PTL → modulo {Module} => Comando: '{Command}'",
            moduleAddress,
            command);

        TryToSendCommand(command);
    }

    public async Task<string> WaitForButtonAsync(string expectedModule, CancellationToken cancellationToken)
    {
        var controller = ConnectAndGetController(cancellationToken);

        _logger.LogDebug( "In attesa conferma da modulo {Module}", expectedModule);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cmd = controller.GetCommand();

            if (cmd != null)
            {
                return ExtractSourceModule(cmd) ?? string.Empty;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private static string? ExtractSourceModule(CommandInfo cmd)
    {
        var addresses = AddressInfo.SplitCommand(cmd.GetCommandText());

        return addresses.FirstOrDefault()?.Address;
    }

    private EthernetController ConnectAndGetController(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _connectionService.EnsureConnectedAsync(cancellationToken).GetAwaiter().GetResult();

        return _connectionService.GetController();
    }

    private void TryToSendCommand(string command)
    {
        var controller = _connectionService.GetController();

        CommandInfo response;

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
