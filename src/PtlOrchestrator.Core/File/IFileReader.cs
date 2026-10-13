
namespace PtlOrchestrator.File;

public interface IFileReader<T>
{
    IEnumerable<T> Read(string filePath);
}