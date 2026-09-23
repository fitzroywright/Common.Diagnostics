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

public enum EngineeringDiagnosticRunState
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Cancelled = 4
}

public sealed record EngineeringDiagnosticCheckResult(
    string CheckId,
    string Name,
    EngineeringDiagnosticStatus Status,
    string Summary,
    string? Evidence = null,
    string? Expected = null,
    string? Actual = null,
    string? Code = null);

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
    string? Resolution = null,
    Guid? CorrelationId = null,
    DiagnosticTargetType TargetType = DiagnosticTargetType.ControlPlane,
    string? TargetId = null,
    string? ApplicationId = null,
    string? InstanceId = null,
    DateTimeOffset? RequestedAtUtc = null,
    EngineeringDiagnosticRunState RunState = EngineeringDiagnosticRunState.Completed,
    string? CurrentStage = null,
    int ProgressPercent = 100);

public sealed record EngineeringDiagnosticRunRequest(
    EngineeringDiagnosticLevel Level,
    string? Reason,
    DiagnosticTargetType TargetType = DiagnosticTargetType.ControlPlane,
    string? TargetId = null,
    string? ApplicationId = null,
    string? InstanceId = null,
    bool AcknowledgeDisruption = false);

public sealed record EngineeringDiagnosticResolutionRequest(string Resolution);

public interface IEngineeringDiagnosticRunStore
{
    Task<IReadOnlyList<EngineeringDiagnosticRun>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
    Task<EngineeringDiagnosticRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
    Task SaveAsync(EngineeringDiagnosticRun run, CancellationToken cancellationToken = default);
    Task<EngineeringDiagnosticRun?> ResolveAsync(Guid runId, string resolvedBy, string resolution, CancellationToken cancellationToken = default);
}
