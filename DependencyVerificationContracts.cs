namespace Common.Diagnostics;

public enum DependencyDiagnosticKind
{
    Database = 1,
    Secrets = 2,
    Messaging = 3,
    Storage = 4,
    Security = 5,
    ExternalIntegration = 6,
    Network = 7,
    Hardware = 8,
    Other = 99
}

public sealed record DependencyVerificationResult(
    string Component,
    string Dependency,
    DependencyDiagnosticKind Kind,
    bool Configured,
    bool Reachable,
    bool Functional,
    OperationalDiagnosticState State,
    DateTimeOffset ObservedAtUtc,
    TimeSpan Duration,
    string Summary,
    string? Evidence = null,
    string? Code = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public bool Passed =>
        Configured &&
        Reachable &&
        Functional &&
        State is OperationalDiagnosticState.Healthy;
}

public interface IDependencyDiagnosticProbe
{
    string Component { get; }

    string Dependency { get; }

    DependencyDiagnosticKind Kind { get; }

    EngineeringDiagnosticLevel Level { get; }

    Task<DependencyVerificationResult> VerifyAsync(
        CancellationToken cancellationToken = default);
}

public static class DependencyDiagnosticPolicy
{
    public static EngineeringDiagnosticCheckResult ToEngineeringResult(
        string checkId,
        string name,
        DependencyVerificationResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(result);

        string evidence = BuildEvidence(result);

        return result.State switch
        {
            OperationalDiagnosticState.Healthy when result.Passed =>
                EngineeringDiagnosticPolicy.Passed(
                    checkId,
                    name,
                    result.Summary,
                    evidence),

            OperationalDiagnosticState.Failed or OperationalDiagnosticState.Offline =>
                EngineeringDiagnosticPolicy.Failed(
                    checkId,
                    name,
                    result.Summary,
                    evidence),

            OperationalDiagnosticState.Degraded or OperationalDiagnosticState.Warning =>
                EngineeringDiagnosticPolicy.Warning(
                    checkId,
                    name,
                    result.Summary,
                    evidence),

            OperationalDiagnosticState.NotSupported =>
                EngineeringDiagnosticPolicy.Warning(
                    checkId,
                    name,
                    result.Summary,
                    evidence),

            _ when !result.Configured || !result.Reachable || !result.Functional =>
                EngineeringDiagnosticPolicy.Failed(
                    checkId,
                    name,
                    result.Summary,
                    evidence),

            _ =>
                EngineeringDiagnosticPolicy.Warning(
                    checkId,
                    name,
                    result.Summary,
                    evidence)
        };
    }

    public static DiagnosticObservation ToObservation(
        string applicationId,
        string instanceId,
        string environment,
        DependencyVerificationResult result,
        DiagnosticSeverity severity = DiagnosticSeverity.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentNullException.ThrowIfNull(result);

        return new DiagnosticObservation(
            applicationId,
            result.Component,
            instanceId,
            environment,
            DiagnosticCategory.Dependency,
            result.State,
            severity,
            result.Summary,
            result.ObservedAtUtc,
            result.ObservedAtUtc,
            result.Duration,
            result.Evidence,
            Dependency: result.Dependency,
            Metadata: result.Metadata);
    }

    private static string BuildEvidence(DependencyVerificationResult result)
    {
        List<string> parts =
        [
            $"Component={result.Component}",
            $"Dependency={result.Dependency}",
            $"Kind={result.Kind}",
            $"Configured={result.Configured}",
            $"Reachable={result.Reachable}",
            $"Functional={result.Functional}",
            $"State={result.State}",
            $"DurationMs={result.Duration.TotalMilliseconds:F0}"
        ];

        if (!string.IsNullOrWhiteSpace(result.Code))
        {
            parts.Add($"Code={result.Code}");
        }

        if (!string.IsNullOrWhiteSpace(result.Evidence))
        {
            parts.Add(result.Evidence);
        }

        if (result.Metadata is not null)
        {
            parts.AddRange(result.Metadata
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}={item.Value}"));
        }

        return string.Join("; ", parts);
    }
}
