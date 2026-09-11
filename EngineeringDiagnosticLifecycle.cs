namespace Common.Diagnostics;

public sealed record EngineeringDiagnosticLevelDefinition(
    EngineeringDiagnosticLevel Level,
    string Name,
    string Description,
    bool RequiresReason,
    bool RequiresExplicitAuthorization,
    bool AllowsAutomaticDestructiveAction);

public static class EngineeringDiagnosticLifecycle
{
    public static IReadOnlyDictionary<EngineeringDiagnosticLevel, EngineeringDiagnosticLevelDefinition> Definitions { get; }
        = new Dictionary<EngineeringDiagnosticLevel, EngineeringDiagnosticLevelDefinition>
        {
            [EngineeringDiagnosticLevel.Level5Scan] = new(
                EngineeringDiagnosticLevel.Level5Scan,
                "Scan",
                "Fast baseline checks covering service availability, dependency reachability, basic configuration, storage or database reachability, queues, and recent heartbeat state.",
                false,
                false,
                false),
            [EngineeringDiagnosticLevel.Level4Analysis] = new(
                EngineeringDiagnosticLevel.Level4Analysis,
                "Analysis",
                "Deeper dependency and data-flow analysis covering integration queues, external systems, configuration consistency, performance signals, and error history.",
                false,
                false,
                false),
            [EngineeringDiagnosticLevel.Level3Verification] = new(
                EngineeringDiagnosticLevel.Level3Verification,
                "Verification",
                "Verify the suspected fault or recovery by confirming symptoms, comparing expected and observed state, checking component health, and capturing supporting evidence.",
                false,
                false,
                false),
            [EngineeringDiagnosticLevel.Level2Repair] = new(
                EngineeringDiagnosticLevel.Level2Repair,
                "Repair",
                "Controlled repair stage. Requires an engineering reason, explicit authorization, and a registered safe action. A diagnostic run must not perform destructive repair automatically.",
                true,
                true,
                false),
            [EngineeringDiagnosticLevel.Level1CriticalIntervention] = new(
                EngineeringDiagnosticLevel.Level1CriticalIntervention,
                "Critical Intervention",
                "Emergency intervention for service-threatening incidents. Requires an engineering reason, explicit high-trust approval, full audit evidence, and a narrowly scoped intervention playbook.",
                true,
                true,
                false)
        };

    public static EngineeringDiagnosticLevelDefinition GetDefinition(EngineeringDiagnosticLevel level)
    {
        EngineeringDiagnosticPolicy.ValidateLevel(level);
        return Definitions[level];
    }

    public static bool IsEscalation(
        EngineeringDiagnosticLevel current,
        EngineeringDiagnosticLevel requested)
    {
        EngineeringDiagnosticPolicy.ValidateLevel(current);
        EngineeringDiagnosticPolicy.ValidateLevel(requested);
        return (int)requested < (int)current;
    }

    public static bool IsDeEscalation(
        EngineeringDiagnosticLevel current,
        EngineeringDiagnosticLevel requested)
    {
        EngineeringDiagnosticPolicy.ValidateLevel(current);
        EngineeringDiagnosticPolicy.ValidateLevel(requested);
        return (int)requested > (int)current;
    }
}
