namespace Common.Diagnostics;

public enum DiagnosticTargetType
{
    ControlPlane = 1,
    ControlPlaneComponent = 2,
    RegisteredApplication = 3
}

public static class ControlPlaneDiagnosticTargets
{
    public const string EntireControlPlane = "control-plane";
    public const string Operations = "operations";
    public const string Configuration = "configuration";
    public const string Diagnostics = "diagnostics";
    public const string Registration = "registration";

    public static readonly IReadOnlySet<string> Components =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Operations,
            Configuration,
            Diagnostics,
            Registration
        };
}

public sealed record DiagnosticTarget(
    DiagnosticTargetType Type,
    string TargetId,
    string? ApplicationId = null,
    string? InstanceId = null,
    string? Component = null)
{
    public static DiagnosticTarget EntireControlPlane() =>
        new(DiagnosticTargetType.ControlPlane, ControlPlaneDiagnosticTargets.EntireControlPlane);

    public static DiagnosticTarget ComponentTarget(string component)
    {
        if (!ControlPlaneDiagnosticTargets.Components.Contains(component))
            throw new ArgumentOutOfRangeException(nameof(component), $"Unknown Control Plane component '{component}'.");
        return new(DiagnosticTargetType.ControlPlaneComponent, component, Component: component);
    }

    public static DiagnosticTarget RegisteredApplication(string applicationId, string instanceId)
    {
        if (string.IsNullOrWhiteSpace(applicationId)) throw new ArgumentException("ApplicationId is required.", nameof(applicationId));
        if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("InstanceId is required.", nameof(instanceId));
        return new(
            DiagnosticTargetType.RegisteredApplication,
            $"{applicationId.Trim()}/{instanceId.Trim()}",
            applicationId.Trim(),
            instanceId.Trim());
    }
}

public static class EngineeringDiagnosticLevelSemantics
{
    public static IReadOnlyList<EngineeringDiagnosticLevel> StarfleetOrder { get; } =
    [
        EngineeringDiagnosticLevel.Level5Scan,
        EngineeringDiagnosticLevel.Level4Analysis,
        EngineeringDiagnosticLevel.Level3Verification,
        EngineeringDiagnosticLevel.Level2Repair,
        EngineeringDiagnosticLevel.Level1CriticalIntervention
    ];

    public static string DisplayName(EngineeringDiagnosticLevel level) => level switch
    {
        EngineeringDiagnosticLevel.Level5Scan => "Level 5 — Routine",
        EngineeringDiagnosticLevel.Level4Analysis => "Level 4 — Integration",
        EngineeringDiagnosticLevel.Level3Verification => "Level 3 — Functional",
        EngineeringDiagnosticLevel.Level2Repair => "Level 2 — Failure / Recovery",
        EngineeringDiagnosticLevel.Level1CriticalIntervention => "Level 1 — Exhaustive",
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    public static bool IsDisruptive(EngineeringDiagnosticLevel level) =>
        level is EngineeringDiagnosticLevel.Level2Repair or EngineeringDiagnosticLevel.Level1CriticalIntervention;

    public static int Depth(EngineeringDiagnosticLevel level) => level switch
    {
        EngineeringDiagnosticLevel.Level5Scan => 1,
        EngineeringDiagnosticLevel.Level4Analysis => 2,
        EngineeringDiagnosticLevel.Level3Verification => 3,
        EngineeringDiagnosticLevel.Level2Repair => 4,
        EngineeringDiagnosticLevel.Level1CriticalIntervention => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };
}

public static class LevelXDiagnosticCodes
{
    public const string StateDerivationMismatch = "STATE_DERIVATION_MISMATCH";
    public const string StaleTelemetry = "STALE_TELEMETRY";
    public const string StaleAuthority = "STALE_AUTHORITY";
    public const string HealthMismatch = "HEALTH_MISMATCH";
    public const string AuthorityUnavailable = "AUTHORITY_UNAVAILABLE";
    public const string ControlPlaneStateDivergence = "CONTROL_PLANE_STATE_DIVERGENCE";
}

public sealed record DiagnosticFreshnessEvidence(
    DateTimeOffset NowUtc,
    DateTimeOffset? LastAuthenticatedAtUtc,
    DateTimeOffset? LastTelemetryAtUtc,
    TimeSpan StaleAfter,
    bool RegistrationFresh,
    bool TelemetryFresh,
    string EffectiveState,
    string? DisplayedState = null)
{
    public TimeSpan? AuthenticationAge => LastAuthenticatedAtUtc.HasValue ? NowUtc - LastAuthenticatedAtUtc.Value : null;
    public TimeSpan? TelemetryAge => LastTelemetryAtUtc.HasValue ? NowUtc - LastTelemetryAtUtc.Value : null;
}
