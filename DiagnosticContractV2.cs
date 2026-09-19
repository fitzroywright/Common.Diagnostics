namespace Common.Diagnostics;

public enum DiagnosticCategory
{
    Health,
    Configuration,
    Dependency,
    Flow,
    Synchronization,
    Queue,
    Error,
    Performance,
    Security,
    Decision,
    BusinessProbe,
    OperationalEvent
}

public enum OperationalDiagnosticState
{
    Healthy,
    Degraded,
    Warning,
    Failed,
    Offline,
    Unknown,
    Maintenance,
    NotSupported
}

public enum FlowStageState
{
    Pending,
    Active,
    Completed,
    Warning,
    Failed,
    Skipped,
    TimedOut,
    Unknown
}

public sealed record DiagnosticObservation(
    string ApplicationId,
    string Component,
    string InstanceId,
    string Environment,
    DiagnosticCategory Category,
    OperationalDiagnosticState State,
    DiagnosticSeverity Severity,
    string Summary,
    DateTimeOffset TimestampUtc,
    DateTimeOffset ObservedAtUtc,
    TimeSpan? Duration = null,
    string? Detail = null,
    string? CorrelationId = null,
    string? Dependency = null,
    string? Remediation = null,
    string? RunbookReference = null,
    IReadOnlyDictionary<string,string>? Metadata = null)
{
    public bool IsStale(DateTimeOffset nowUtc, TimeSpan staleAfter) =>
        staleAfter >= TimeSpan.Zero && nowUtc - ObservedAtUtc > staleAfter;

    public OperationalDiagnosticState EffectiveState(DateTimeOffset nowUtc, TimeSpan staleAfter) =>
        IsStale(nowUtc, staleAfter) ? OperationalDiagnosticState.Unknown : State;
}

public sealed record DiagnosticFlowStageDefinition(
    string Name,
    string? ParentStage = null,
    string? Branch = null,
    TimeSpan? ExpectedDuration = null,
    TimeSpan? WarningAfter = null,
    TimeSpan? CriticalAfter = null);

public sealed record DiagnosticFlowDefinition(
    string ApplicationId,
    string FlowType,
    string DisplayName,
    IReadOnlyList<DiagnosticFlowStageDefinition> Stages,
    string? RunbookReference = null);

public sealed record DiagnosticFlowStageObservation(
    string Name,
    FlowStageState State,
    DateTimeOffset? StartedAtUtc = null,
    DateTimeOffset? CompletedAtUtc = null,
    int RetryCount = 0,
    string? Failure = null,
    string? ParentStage = null,
    string? Branch = null,
    TimeSpan? ExpectedDuration = null,
    TimeSpan? WarningAfter = null,
    TimeSpan? CriticalAfter = null)
{
    public FlowStageState EffectiveState(DateTimeOffset nowUtc)
    {
        if (State != FlowStageState.Active || StartedAtUtc is null) return State;
        TimeSpan elapsed = nowUtc - StartedAtUtc.Value;
        if (CriticalAfter is { } critical && elapsed > critical) return FlowStageState.TimedOut;
        if (WarningAfter is { } warning && elapsed > warning) return FlowStageState.Warning;
        return State;
    }
}

public sealed record DiagnosticFlowInstance(
    string CorrelationId,
    string ApplicationId,
    string FlowType,
    string InstanceId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<DiagnosticFlowStageObservation> Stages,
    string? CurrentStage = null,
    string? RelatedBusinessId = null,
    string? RunbookReference = null);

public sealed record SynchronizationDiagnostic(
    string ApplicationId,
    string EndpointId,
    string InstanceId,
    bool Online,
    OperationalDiagnosticState State,
    int PendingRecords,
    TimeSpan? OldestPendingAge,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? LastSuccessfulAtUtc,
    int FailedRecords,
    DateTimeOffset ObservedAtUtc,
    string? Reason = null)
{
    public OperationalDiagnosticState EffectiveState(DateTimeOffset nowUtc, TimeSpan staleAfter) =>
        nowUtc - ObservedAtUtc > staleAfter ? OperationalDiagnosticState.Unknown : State;
}

public sealed record QueueDiagnostic(
    string ApplicationId,
    string QueueName,
    int QueueDepth,
    TimeSpan? OldestItemAge,
    int ProcessingCount,
    int RetryCount,
    int FailedCount,
    int DeadLetterCount,
    OperationalDiagnosticState State,
    DateTimeOffset ObservedAtUtc);

public sealed record DecisionDiagnostic(
    string ApplicationId,
    string Component,
    string Decision,
    string Outcome,
    bool IsValidBusinessOutcome,
    OperationalDiagnosticState State,
    DiagnosticSeverity Severity,
    DateTimeOffset ObservedAtUtc,
    string? CorrelationId = null,
    string? Detail = null);

public static class DiagnosticStateMapping
{
    public static OperationalDiagnosticState FromLegacy(DiagnosticStatus status) => status switch
    {
        DiagnosticStatus.Healthy => OperationalDiagnosticState.Healthy,
        DiagnosticStatus.Warning => OperationalDiagnosticState.Warning,
        DiagnosticStatus.Unhealthy => OperationalDiagnosticState.Failed,
        _ => OperationalDiagnosticState.Unknown
    };
}
