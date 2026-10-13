namespace PtlOrchestrator.Tests.Support;

/// <summary>
/// Cartella temporanea eliminata al Dispose. Usa System.IO.File esplicitamente perché
/// il namespace PtlOrchestrator.File nasconderebbe il tipo System.IO.File.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PtlOrchestratorTests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string fileName, string content)
    {
        var filePath = System.IO.Path.Combine(Path, fileName);
        System.IO.File.WriteAllText(filePath, content);
        return filePath;
    }

    public string PathOf(string fileName)
        => System.IO.Path.Combine(Path, fileName);

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
