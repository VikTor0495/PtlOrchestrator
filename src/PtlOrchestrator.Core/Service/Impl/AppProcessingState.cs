using PtlOrchestrator.Service;
using Microsoft.Extensions.Logging;

namespace PtlOrchestrator.Service.Impl;

public sealed class AppProcessingState(ILogger<AppProcessingState> logger) : IAppProcessingState
{

    private readonly ILogger<AppProcessingState> _logger = logger;

    private int _busy = 0;

    public bool IsBusy => _busy == 1;

    public bool TryEnter()
    {

        bool entered = Interlocked.CompareExchange(ref _busy, 1, 0) == 0;
        _logger.LogDebug("Tentativo di acquisizione stato occupato: {Result}", entered ? "SUCCESSO" : "FALLITO - già occupato");
        return entered;
    }


    public void Exit()
    {
        int previousValue = Interlocked.Exchange(ref _busy, 0);
        _logger.LogDebug("Rilascio stato occupato: {Result}", previousValue == 1 ? "SUCCESSO - stato occupato rilasciato" : "ATTENZIONE - stato già libero");
    }
}
