using Common.Diagnostics;
using Xunit;

namespace Common.Diagnostics.UnitTests;

public sealed class EngineeringDiagnosticPolicyTests
{
    [Fact]
    public void CalculateStatus_PrefersFailedOverOtherStatuses()
    {
        EngineeringDiagnosticCheckResult[] checks =
        [
            EngineeringDiagnosticPolicy.Passed("a", "A", "ok"),
            EngineeringDiagnosticPolicy.Warning("b", "B", "warn"),
            EngineeringDiagnosticPolicy.Failed("c", "C", "bad"),
            EngineeringDiagnosticPolicy.CreateInterventionGate(EngineeringDiagnosticLevel.Level2Repair, "repair")
        ];

        Assert.Equal(EngineeringDiagnosticStatus.Failed, EngineeringDiagnosticPolicy.CalculateStatus(checks));
    }

    [Fact]
    public void ValidateReason_RequiresReasonForLevelTwoAndOne()
    {
        Assert.Throws<ArgumentException>(() => EngineeringDiagnosticPolicy.ValidateReason(EngineeringDiagnosticLevel.Level2Repair, null));
        Assert.Throws<ArgumentException>(() => EngineeringDiagnosticPolicy.ValidateReason(EngineeringDiagnosticLevel.Level1CriticalIntervention, " "));
        EngineeringDiagnosticPolicy.ValidateReason(EngineeringDiagnosticLevel.Level3Verification, null);
    }

    [Fact]
    public void CreateInterventionGate_ReturnsExpectedGate()
    {
        EngineeringDiagnosticCheckResult result = EngineeringDiagnosticPolicy.CreateInterventionGate(
            EngineeringDiagnosticLevel.Level1CriticalIntervention,
            "incident");

        Assert.Equal(EngineeringDiagnosticStatus.InterventionRequired, result.Status);
        Assert.Equal("critical-intervention-gate", result.CheckId);
        Assert.Equal("incident", result.Evidence);
    }
}
