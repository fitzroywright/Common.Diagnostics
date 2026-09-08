namespace Common.Diagnostics;

using System.Diagnostics;

public sealed class FileSystemDiagnosticCheck : IDiagnosticCheck
{
    private readonly string path;
    private readonly bool requireWrite;

    public FileSystemDiagnosticCheck(string name, string path, bool requireWrite = false)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Diagnostic name is required.", nameof(name));
        this.path = !string.IsNullOrWhiteSpace(path) ? path : throw new ArgumentException("Path is required.", nameof(path));
        this.requireWrite = requireWrite;
    }

    public string Name { get; }

    public async Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(path);
            _ = Directory.EnumerateFileSystemEntries(path).Take(1).ToArray();

            if (requireWrite)
            {
                string probePath = Path.Combine(path, $".diagnostic-{Guid.NewGuid():N}.tmp");
                await File.WriteAllTextAsync(probePath, "Common.Diagnostics storage probe", cancellationToken);
                File.Delete(probePath);
            }

            stopwatch.Stop();
            string capability = requireWrite ? "read/write" : "read";
            return new DiagnosticResult(Name, DiagnosticStatus.Healthy, $"Storage path is available for {capability} access.", stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DiagnosticResult(Name, DiagnosticStatus.Unhealthy, "Storage path is unavailable. Check path, mount/share availability, permissions, capacity, and network connectivity.", stopwatch.Elapsed, exception);
        }
    }
}
