namespace Common.Diagnostics;

public enum DiagnosticStatus
{
    Healthy = 0,
    Warning = 1,
    Unhealthy = 2
}

public sealed record DiagnosticResult(
    string Name,
    DiagnosticStatus Status,
    string Message,
    TimeSpan Duration,
    Exception? Exception = null)
{
    public bool IsHealthy => Status == DiagnosticStatus.Healthy;
}

public interface IDiagnosticCheck
{
    string Name { get; }

    Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default);
}

public interface IDiagnosticRunner
{
    Task<IReadOnlyList<DiagnosticResult>> RunAsync(CancellationToken cancellationToken = default);
}
