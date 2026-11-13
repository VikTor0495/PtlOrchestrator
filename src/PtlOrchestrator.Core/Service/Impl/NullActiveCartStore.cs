namespace PtlOrchestrator.Service.Impl;

/// <summary>Implementazione senza memoria: i carrelli attivi non sopravvivono alla chiusura.</summary>
public sealed class NullActiveCartStore : IActiveCartStore
{
    public Task SaveAsync(IReadOnlyCollection<string> cartCodes, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>([]);
}
