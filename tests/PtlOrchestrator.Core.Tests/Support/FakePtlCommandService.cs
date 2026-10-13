using PtlOrchestrator.Service;

namespace PtlOrchestrator.Core.Tests.Support;

/// <summary>
/// Simula il controller PTL: registra i comandi inviati e restituisce, nell'ordine,
/// i moduli "premuti" dall'operatore. Se non ci sono pressioni in coda resta in attesa
/// finché non viene cancellato (utile per simulare il timeout).
/// </summary>
internal sealed class FakePtlCommandService : IPtlCommandService
{
    private readonly Queue<string> _buttonPresses = new();

    public List<string> SentCommands { get; } = [];

    public List<string> WaitedModules { get; } = [];

    public Exception? SendFailure { get; set; }

    public void EnqueueButtonPresses(params string[] modules)
    {
        foreach (var module in modules)
            _buttonPresses.Enqueue(module);
    }

    public Task SendRawAsync(string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SendFailure is not null)
            throw SendFailure;

        SentCommands.Add(command);
        return Task.CompletedTask;
    }

    public async Task<string> WaitForButtonAsync(string expectedModule, CancellationToken cancellationToken)
    {
        WaitedModules.Add(expectedModule);

        if (_buttonPresses.Count == 0)
            await Task.Delay(Timeout.Infinite, cancellationToken);

        return _buttonPresses.Dequeue();
    }
}
