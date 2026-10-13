using Xunit;

namespace Common.Diagnostics.UnitTests;

public sealed class AcceptanceEvidenceTests
{
    private static AcceptanceEvidence Evidence(AcceptanceProofState state = AcceptanceProofState.Passed, bool isolated = true)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new(new(Guid.NewGuid(), EngineeringDiagnosticLevel.Level1CriticalIntervention, "Cafeteria", "Acceptance",
            "Engineer", "Qualification", now, now, EngineeringDiagnosticStatus.Passed, []), "1", "build-sha", "schema-sha", isolated,
            [new("CAF-01", "Policy", state, "100", "100", "Observed", now)]);
    }
    private static readonly AcceptanceProofDefinition[] Required = [new("CAF-01", "Policy", 2)];

    [Theory]
    [InlineData(AcceptanceProofState.Failed)]
    [InlineData(AcceptanceProofState.Blocked)]
    [InlineData(AcceptanceProofState.Unexecuted)]
    public void Incomplete_evidence_never_proves_integration(AcceptanceProofState state) => Assert.False(Evidence(state).IntegrationProven(Required));
    [Fact]
    public void Isolation_is_required_even_when_assertions_pass() => Assert.False(Evidence(isolated: false).IntegrationProven(Required));
    [Fact]
    public void Level_2_success_never_qualifies_full_integration()
    {
        var e = Evidence(); e = e with { Diagnostic = e.Diagnostic with { Level = EngineeringDiagnosticLevel.Level2Repair } };
        Assert.False(e.IntegrationProven(Required));
    }
    [Fact]
    public void Empty_diagnostic_selection_cannot_manufacture_a_pass()
    {
        var e = Evidence();
        Assert.Equal(EngineeringDiagnosticStatus.Failed, (e with { Proofs = [] }).ToDiagnosticRun().Status);
        Assert.Equal(EngineeringDiagnosticStatus.Failed, (e with { SelectedProofIds = [] }).ToDiagnosticRun().Status);
        Assert.Equal(EngineeringDiagnosticStatus.Failed, (e with { SelectedProofIds = ["missing"] }).ToDiagnosticRun().Status);
    }
    [Fact]
    public void Duplicate_or_missing_proofs_cannot_qualify()
    {
        var e = Evidence();
        Assert.False((e with { Proofs = [e.Proofs[0], e.Proofs[0]] }).IntegrationProven(Required));
        Assert.False(e.IntegrationProven([.. Required, new("CAF-02", "Employees", 2)]));
    }
    [Fact]
    public void Tests_do_not_automatically_grant_development_or_release_status()
    {
        Assert.True(Evidence().IntegrationProven(Required));
        var status = AcceptanceReleaseStatus.Evaluate(false, false, true, false, 0);
        Assert.False(status.DevelopmentComplete); Assert.True(status.IntegrationProven); Assert.False(status.ReadyForUserAcceptance);
    }
    [Fact]
    public void Blocked_values_survive_existing_diagnostic_store_contract()
    {
        var run = Evidence(AcceptanceProofState.Blocked).ToDiagnosticRun();
        Assert.Equal(EngineeringDiagnosticStatus.Failed, run.Status);
        Assert.Equal("Blocked", run.Checks.Single().Code);
        Assert.Equal("100", run.Checks.Single().Expected); Assert.Equal("100", run.Checks.Single().Actual);
    }
}
