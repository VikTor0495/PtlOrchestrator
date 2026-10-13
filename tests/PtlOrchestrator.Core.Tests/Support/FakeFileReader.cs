using PtlOrchestrator.File;

namespace PtlOrchestrator.Core.Tests.Support;

internal sealed class FakeFileReader<T>(params T[] items) : IFileReader<T>
{
    private IReadOnlyList<T> _items = items;

    public int ReadCalls { get; private set; }

    public void SetItems(params T[] items) => _items = items;

    public IEnumerable<T> Read(string filePath)
    {
        ReadCalls++;
        return _items;
    }
}
