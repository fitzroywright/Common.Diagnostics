namespace Common.Diagnostics;

public enum EngineeringDiagnosticLevel
{
    Level5Scan = 5,
    Level4Analysis = 4,
    Level3Verification = 3,
    Level2Repair = 2,
    Level1CriticalIntervention = 1
}

public enum EngineeringDiagnosticStatus
{
    Passed = 1,
    Warning = 2,
    Failed = 3,
    InterventionRequired = 4
}

public sealed record EngineeringDiagnosticCheckResult(
    string CheckId,
    string Name,
    EngineeringDiagnosticStatus Status,
    string Summary,
    string? Evidence = null);

public sealed record EngineeringDiagnosticRun(
    Guid RunId,
    EngineeringDiagnosticLevel Level,
    string Application,
    string Environment,
    string RequestedBy,
    string? Reason,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    EngineeringDiagnosticStatus Status,
    IReadOnlyList<EngineeringDiagnosticCheckResult> Checks,
    DateTimeOffset? ResolvedAt = null,
    string? ResolvedBy = null,
    string? Resolution = null);

public sealed record EngineeringDiagnosticRunRequest(
    EngineeringDiagnosticLevel Level,
    string? Reason);

public sealed record EngineeringDiagnosticResolutionRequest(string Resolution);

public interface IEngineeringDiagnosticRunStore
{
    Task<IReadOnlyList<EngineeringDiagnosticRun>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<EngineeringDiagnosticRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
    Task SaveAsync(EngineeringDiagnosticRun run, CancellationToken cancellationToken = default);
    Task<EngineeringDiagnosticRun?> ResolveAsync(Guid runId, string resolvedBy, string resolution, CancellationToken cancellationToken = default);
}
