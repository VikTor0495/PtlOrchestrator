using PtlOrchestrator.Service;

namespace PtlOrchestrator.Tests.Support;

internal sealed class StubBasketLimitService(IDictionary<string, int> limits) : IBasketLimitService
{
    private readonly IDictionary<string, int> _limits = limits;

    public void Load(string csvPath, CancellationToken cancellationToken)
    {
    }

    public int GetMaxFor(string barcode)
        => _limits.TryGetValue(barcode, out var max) ? max : 0;

    public bool HasMaxLimit(string barcode)
        => _limits.ContainsKey(barcode);
}
