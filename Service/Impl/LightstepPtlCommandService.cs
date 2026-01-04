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

    public async Task SendAsync(string moduleAddress, PtlActivation activation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _connectionService.EnsureConnectedAsync(cancellationToken);

        var command = Pp505Builder.Build(moduleAddress, activation);

        _logger.LogInformation(
            "PTL → modulo {Module}, colore {Color}, blink {Blink}, testo '{Text}' => Comando: {Command}",
            moduleAddress,
            activation.Color,
            activation.Blinking,
            activation.DisplayText ?? string.Empty,
            command);

        TryToSendCommand(command);

    }

    public async Task WaitForButtonAsync(string expectedModule, CancellationToken cancellationToken)
    {
        var controller = ConnectAndGetController(cancellationToken);

        _logger.LogInformation("In attesa conferma da modulo {Module}", expectedModule);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cmd = controller.GetCommand();

            if (cmd != null)
            {
                if (IsConfirmFromModule(cmd, expectedModule))
                {
                    return; 
                }
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private static bool IsConfirmFromModule(CommandInfo cmd, string expectedModule)
    {
        var addresses = AddressInfo.SplitCommand(cmd.GetCommandText());

        foreach (var addr in addresses)
        {
            if (addr.Address == expectedModule.ToString())
            {
                return true;
            }
        }

        return false;
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
