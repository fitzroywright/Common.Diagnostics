namespace Common.Diagnostics;

/// <summary>Acceptance evidence uses the existing diagnostic run and store contracts.</summary>
public enum AcceptanceProofState { Passed, Failed, Blocked, Unexecuted }

public sealed record AcceptanceProofDefinition(string Id, string Name, int Level, string? LegacyFunction = null);
public sealed record AcceptanceProofResult(string Id, string Name, AcceptanceProofState State,
    string Expected, string Actual, string Detail, DateTimeOffset AtUtc);

public sealed record AcceptanceEvidence(
    EngineeringDiagnosticRun Diagnostic,
    string SuiteVersion,
    string ApplicationVersion,
    string SchemaVersion,
    bool IsolationVerified,
    IReadOnlyList<AcceptanceProofResult> Proofs,
    IReadOnlyList<string>? SelectedProofIds = null)
{
    public bool IntegrationProven(IReadOnlyCollection<AcceptanceProofDefinition> required) =>
        IsolationVerified && Diagnostic.Level == EngineeringDiagnosticLevel.Level1CriticalIntervention
        && Diagnostic.RunState == EngineeringDiagnosticRunState.Completed
        && required.Count > 0 && Proofs.Count == required.Count
        && Proofs.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == required.Count
        && required.All(x => Proofs.Any(p => p.Id == x.Id && p.State == AcceptanceProofState.Passed));

    public EngineeringDiagnosticRun ToDiagnosticRun() => Diagnostic with
    {
        Status = Proofs.Count > 0 && (SelectedProofIds is null || SelectedProofIds.Count > 0
            && SelectedProofIds.All(id => Proofs.Any(p => p.Id == id)))
            && Proofs.Where(p => SelectedProofIds is null || SelectedProofIds.Contains(p.Id))
            .All(p => p.State == AcceptanceProofState.Passed) && IsolationVerified
            ? EngineeringDiagnosticStatus.Passed : EngineeringDiagnosticStatus.Failed,
        Checks = Proofs.Where(p => SelectedProofIds is null || SelectedProofIds.Contains(p.Id)).Select(p => new EngineeringDiagnosticCheckResult(p.Id, p.Name,
            p.State == AcceptanceProofState.Passed ? EngineeringDiagnosticStatus.Passed
                : p.State == AcceptanceProofState.Failed ? EngineeringDiagnosticStatus.Failed
                : EngineeringDiagnosticStatus.InterventionRequired,
            p.Detail, $"State={p.State}; At={p.AtUtc:O}; Suite={SuiteVersion}; Application={ApplicationVersion}; Schema={SchemaVersion}",
            p.Expected, p.Actual, p.State.ToString())).ToArray()
    };
}

public sealed record AcceptanceReleaseStatus(bool DevelopmentComplete, bool IntegrationProven,
    bool ReadyForUserAcceptance)
{
    public static AcceptanceReleaseStatus Evaluate(bool implementationReviewed, bool buildAndTestsPassed,
        bool integrationProven, bool deploymentVerified, int releaseBlockers) => new(
            implementationReviewed && buildAndTestsPassed && releaseBlockers == 0,
            integrationProven,
            implementationReviewed && buildAndTestsPassed && integrationProven && deploymentVerified && releaseBlockers == 0);
}

/// <summary>Explicit engineering sign-off; never inferred from test implementation.</summary>
public sealed record AcceptanceQualification(string ApplicationVersion, string ReviewedBy,
    DateTimeOffset ReviewedAtUtc, bool BuildAndTestsPassed, bool DeploymentVerified,
    IReadOnlyList<string> ReleaseBlockingDefects);
