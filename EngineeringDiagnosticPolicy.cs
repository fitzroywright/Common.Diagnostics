namespace Common.Diagnostics;

public static class EngineeringDiagnosticPolicy
{
    public static void ValidateLevel(EngineeringDiagnosticLevel level)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Diagnostic level must be Level 5 through Level 1.");
        }
    }

    public static void ValidateReason(EngineeringDiagnosticLevel level, string? reason)
    {
        if ((level == EngineeringDiagnosticLevel.Level2Repair || level == EngineeringDiagnosticLevel.Level1CriticalIntervention)
            && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Level 2 and Level 1 diagnostics require an engineering reason.", nameof(reason));
        }
    }

    public static EngineeringDiagnosticStatus CalculateStatus(IEnumerable<EngineeringDiagnosticCheckResult> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        EngineeringDiagnosticCheckResult[] results = checks.ToArray();

        if (results.Any(check => check.Status == EngineeringDiagnosticStatus.Failed))
        {
            return EngineeringDiagnosticStatus.Failed;
        }

        if (results.Any(check => check.Status == EngineeringDiagnosticStatus.InterventionRequired))
        {
            return EngineeringDiagnosticStatus.InterventionRequired;
        }

        if (results.Any(check => check.Status == EngineeringDiagnosticStatus.Warning))
        {
            return EngineeringDiagnosticStatus.Warning;
        }

        return EngineeringDiagnosticStatus.Passed;
    }

    public static EngineeringDiagnosticCheckResult Passed(string checkId, string name, string summary, string? evidence = null)
        => new(checkId, name, EngineeringDiagnosticStatus.Passed, summary, evidence);

    public static EngineeringDiagnosticCheckResult Warning(string checkId, string name, string summary, string? evidence = null)
        => new(checkId, name, EngineeringDiagnosticStatus.Warning, summary, evidence);

    public static EngineeringDiagnosticCheckResult Failed(string checkId, string name, string summary, string? evidence = null)
        => new(checkId, name, EngineeringDiagnosticStatus.Failed, summary, evidence);

    public static EngineeringDiagnosticCheckResult CreateInterventionGate(EngineeringDiagnosticLevel level, string? reason)
    {
        return level switch
        {
            EngineeringDiagnosticLevel.Level2Repair => new EngineeringDiagnosticCheckResult(
                "repair-gate",
                "Repair authorization gate",
                EngineeringDiagnosticStatus.InterventionRequired,
                "Evidence collection is complete. Automated destructive repair is disabled; an engineer must explicitly select and approve a repair action.",
                reason),
            EngineeringDiagnosticLevel.Level1CriticalIntervention => new EngineeringDiagnosticCheckResult(
                "critical-intervention-gate",
                "Critical intervention gate",
                EngineeringDiagnosticStatus.InterventionRequired,
                "Critical evidence collection is complete. Level 1 intervention requires explicit engineering approval and is never executed automatically by a diagnostic run.",
                reason),
            _ => throw new ArgumentOutOfRangeException(nameof(level), "Only Level 2 and Level 1 have intervention gates.")
        };
    }
}
