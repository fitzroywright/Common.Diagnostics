namespace Common.Diagnostics;

using System.Diagnostics;

using Microsoft.Extensions.Logging;

public sealed class DiagnosticRunner : IDiagnosticRunner
{
    private readonly IReadOnlyList<IDiagnosticCheck> checks;
    private readonly ILogger<DiagnosticRunner> logger;

    public DiagnosticRunner(IEnumerable<IDiagnosticCheck> checks, ILogger<DiagnosticRunner> logger)
    {
        this.checks = checks?.ToArray() ?? throw new ArgumentNullException(nameof(checks));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<DiagnosticResult>> RunAsync(CancellationToken cancellationToken = default)
    {
        List<DiagnosticResult> results = new(checks.Count);

        foreach (IDiagnosticCheck check in checks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                DiagnosticResult result = await check.RunAsync(cancellationToken);
                stopwatch.Stop();

                DiagnosticResult normalized = result with
                {
                    Name = string.IsNullOrWhiteSpace(result.Name) ? check.Name : result.Name,
                    Duration = result.Duration == TimeSpan.Zero ? stopwatch.Elapsed : result.Duration
                };

                results.Add(normalized);
                logger.LogInformation(
                    "Diagnostic {DiagnosticName} completed with {DiagnosticStatus} in {DurationMs} ms. {Message}",
                    normalized.Name,
                    normalized.Status,
                    normalized.Duration.TotalMilliseconds,
                    normalized.Message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                DiagnosticResult failure = new(
                    check.Name,
                    DiagnosticStatus.Unhealthy,
                    exception.Message,
                    stopwatch.Elapsed,
                    exception);

                results.Add(failure);
                logger.LogError(exception, "Diagnostic {DiagnosticName} failed.", check.Name);
            }
        }

        return results;
    }
}
