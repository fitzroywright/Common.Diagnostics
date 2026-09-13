namespace Common.Diagnostics;

/// <summary>Stable deployment identity shared with Aegis.Configuration.</summary>
public sealed record DiagnosticApplicationIdentity(
    string ApplicationId,
    string DisplayName,
    string Version,
    string? SiteId = null,
    string? InstanceId = null);

/// <summary>Normalized operational health. Configuration completeness does not belong here.</summary>
public enum OperationalHealth
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3
}

public sealed record DiagnosticDependencyState(
    string Id,
    string DisplayName,
    OperationalHealth Health,
    string Summary,
    TimeSpan? Duration = null,
    DateTimeOffset? ObservedAt = null,
    string? Evidence = null);

public sealed record DiagnosticApplicationState(
    DiagnosticApplicationIdentity Identity,
    OperationalHealth Health,
    IReadOnlyList<DiagnosticDependencyState> Dependencies,
    DateTimeOffset ObservedAt,
    string? Summary = null);

public static class OperationalHealthMapping
{
    public static OperationalHealth From(DiagnosticStatus status) => status switch
    {
        DiagnosticStatus.Healthy => OperationalHealth.Healthy,
        DiagnosticStatus.Warning => OperationalHealth.Degraded,
        DiagnosticStatus.Unhealthy => OperationalHealth.Unhealthy,
        _ => OperationalHealth.Unknown
    };

    public static OperationalHealth From(EngineeringDiagnosticStatus status) => status switch
    {
        EngineeringDiagnosticStatus.Passed => OperationalHealth.Healthy,
        EngineeringDiagnosticStatus.Warning => OperationalHealth.Degraded,
        EngineeringDiagnosticStatus.Failed or EngineeringDiagnosticStatus.InterventionRequired => OperationalHealth.Unhealthy,
        _ => OperationalHealth.Unknown
    };
}
