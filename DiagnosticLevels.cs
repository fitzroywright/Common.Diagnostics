namespace Common.Diagnostics;

public enum DiagnosticLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4,
    Level5 = 5
}

public sealed record DiagnosticLevelDefinition(
    DiagnosticLevel Level,
    string Name,
    string Description,
    IReadOnlyCollection<string> Capabilities);

public static class DiagnosticLevels
{
    public static IReadOnlyDictionary<DiagnosticLevel, DiagnosticLevelDefinition> Definitions { get; }
        = new Dictionary<DiagnosticLevel, DiagnosticLevelDefinition>
        {
            [DiagnosticLevel.Level1] = new(
                DiagnosticLevel.Level1,
                "Quick Diagnostics",
                "Fast startup and heartbeat verification.",
                ["Basic health", "Service heartbeat", "Critical dependencies", "Configuration sanity"]),
            [DiagnosticLevel.Level2] = new(
                DiagnosticLevel.Level2,
                "Component Diagnostics",
                "Focused checks for individual components and resources.",
                ["Component tests", "API health", "Secret provider health", "Messaging queue health", "Resource utilization"]),
            [DiagnosticLevel.Level3] = new(
                DiagnosticLevel.Level3,
                "System Diagnostics",
                "Core application and service dependency validation.",
                ["Core system health", "Service dependencies", "Authentication", "Data stores", "Certificates"]),
            [DiagnosticLevel.Level4] = new(
                DiagnosticLevel.Level4,
                "Integration Diagnostics",
                "Cross-component and external integration verification.",
                ["Cross-component tests", "Messaging and Secrets", "External connectivity", "Telemetry pipeline", "End-to-end scenarios", "Failover simulation"]),
            [DiagnosticLevel.Level5] = new(
                DiagnosticLevel.Level5,
                "Full System Diagnostics",
                "Deep engineering sweep of the complete system.",
                ["All components", "Deep dependencies", "Security validation", "Performance analysis", "Disaster recovery readiness"])
        };

    public static bool Includes(DiagnosticLevel requested, DiagnosticLevel checkLevel)
    {
        return checkLevel <= requested;
    }
}

public interface ILeveledDiagnosticCheck : IDiagnosticCheck
{
    DiagnosticLevel Level { get; }
}

public interface ILeveledDiagnosticRunner
{
    Task<IReadOnlyList<DiagnosticResult>> RunAsync(
        DiagnosticLevel level,
        CancellationToken cancellationToken = default);
}

public sealed class LeveledDiagnosticRunner : ILeveledDiagnosticRunner
{
    private readonly IReadOnlyCollection<IDiagnosticCheck> checks;

    public LeveledDiagnosticRunner(IEnumerable<IDiagnosticCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        this.checks = checks.ToArray();
    }

    public async Task<IReadOnlyList<DiagnosticResult>> RunAsync(
        DiagnosticLevel level,
        CancellationToken cancellationToken = default)
    {
        List<DiagnosticResult> results = [];
        foreach (IDiagnosticCheck check in checks)
        {
            DiagnosticLevel checkLevel = check is ILeveledDiagnosticCheck leveled
                ? leveled.Level
                : DiagnosticLevel.Level3;
            if (!DiagnosticLevels.Includes(level, checkLevel))
            {
                continue;
            }

            results.Add(await check.RunAsync(cancellationToken).ConfigureAwait(false));
        }

        return results;
    }
}
