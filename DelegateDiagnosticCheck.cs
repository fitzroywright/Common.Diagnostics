namespace Common.Diagnostics;

using System.Diagnostics;

public sealed class DelegateDiagnosticCheck : IDiagnosticCheck
{
    private readonly Func<CancellationToken, Task<(DiagnosticStatus Status, string Message)>> check;

    public DelegateDiagnosticCheck(
        string name,
        Func<CancellationToken, Task<(DiagnosticStatus Status, string Message)>> check)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Diagnostic name is required.", nameof(name));
        this.check = check ?? throw new ArgumentNullException(nameof(check));
    }

    public string Name { get; }

    public async Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            (DiagnosticStatus status, string message) = await check(cancellationToken);
            stopwatch.Stop();
            return new DiagnosticResult(Name, status, message, stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DiagnosticResult(Name, DiagnosticStatus.Unhealthy, exception.Message, stopwatch.Elapsed, exception);
        }
    }
}
