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
                "Exhaustive Certification",
                "Deepest diagnostic certification and controlled intervention.",
                ["Complete certification", "Destructive-isolated scenarios", "Security validation", "Recovery validation", "Disaster recovery readiness"]),
            [DiagnosticLevel.Level2] = new(
                DiagnosticLevel.Level2,
                "Environment / Recovery Diagnostics",
                "Deep environment-resource, failure and recovery verification against safe diagnostic targets.",
                ["Environment resources", "Failure injection", "Lease recovery", "Authority outage recovery", "Telemetry recovery", "Failover simulation"]),
            [DiagnosticLevel.Level3] = new(
                DiagnosticLevel.Level3,
                "Functional Diagnostics",
                "Functional workflow validation without production-destructive behavior.",
                ["Functional workflows", "Registration lifecycle", "Authentication", "Data stores", "Configuration contracts"]),
            [DiagnosticLevel.Level4] = new(
                DiagnosticLevel.Level4,
                "Integration Diagnostics",
                "Cross-service and dependency agreement validation.",
                ["Cross-service tests", "API health", "Control Plane handoffs", "Telemetry agreement", "Configuration propagation"]),
            [DiagnosticLevel.Level5] = new(
                DiagnosticLevel.Level5,
                "Routine Diagnostics",
                "Fast, safe state consistency and freshness verification.",
                ["Basic health", "Registration freshness", "Telemetry freshness", "State consistency", "Configuration sanity"])
        };

    public static bool Includes(DiagnosticLevel requested, DiagnosticLevel checkLevel)
    {
        return checkLevel >= requested;
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
