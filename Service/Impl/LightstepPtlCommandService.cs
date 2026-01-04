using AioiSystems.Lightstep;
using Microsoft.Extensions.Logging;
using PtlOrchestrator.Domain;
using PtlOrchestrator.Service;
using PtlOrchestrator.Builders;

namespace PtlOrchestrator.Service.Impl;

public sealed class LightstepPtlCommandService(
    ILightstepConnectionService connectionService,
    ILogger<LightstepPtlCommandService> logger)
    : IPtlCommandService
{
    private readonly ILightstepConnectionService _connectionService = connectionService;
    private readonly ILogger<LightstepPtlCommandService> _logger = logger;

    public async Task SendAsync(string moduleAddress, PtlActivation activation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await _connectionService.EnsureConnectedAsync(ct);


        var command = Pp505Builder.Build(moduleAddress, activation);

        _logger.LogInformation(
            "PTL → modulo {Module}, colore {Color}, blink {Blink}, testo '{Text}'",
            moduleAddress,
            activation.Color,
            activation.Blinking,
            activation.DisplayText ?? string.Empty);

        TryToSendCommand(command);

        _logger.LogInformation(
            "PTL → modulo {Module}, colore {Color}, blink {Blink}, testo '{Text}'",
            moduleAddress,
            activation.Color,
            activation.Blinking,
            activation.DisplayText ?? string.Empty);

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
