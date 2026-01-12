using PtlOrchestrator.Service;
using Microsoft.Extensions.Logging;

namespace PtlOrchestrator.Service.Impl;

public sealed class AppProcessingState() : IAppProcessingState
{

    private int _busy = 0;

    public bool IsBusy => _busy == 1;

    public bool TryEnter()
        => Interlocked.CompareExchange(ref _busy, 1, 0) == 0;

    public void Exit()
        => Interlocked.Exchange(ref _busy, 0);
}
