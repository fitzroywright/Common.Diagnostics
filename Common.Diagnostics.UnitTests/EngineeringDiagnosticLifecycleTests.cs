using Common.Diagnostics;

namespace Common.Diagnostics.UnitTests;

public sealed class EngineeringDiagnosticLifecycleTests
{
    [Fact]
    public void Definitions_cover_every_engineering_level()
    {
        foreach (EngineeringDiagnosticLevel level in Enum.GetValues<EngineeringDiagnosticLevel>())
        {
            EngineeringDiagnosticLevelDefinition definition = EngineeringDiagnosticLifecycle.GetDefinition(level);

            Assert.Equal(level, definition.Level);
            Assert.False(string.IsNullOrWhiteSpace(definition.Name));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            Assert.False(definition.AllowsAutomaticDestructiveAction);
        }
    }

    [Theory]
    [InlineData(EngineeringDiagnosticLevel.Level2Repair)]
    [InlineData(EngineeringDiagnosticLevel.Level1CriticalIntervention)]
    public void Repair_and_critical_levels_require_reason_and_authorization(EngineeringDiagnosticLevel level)
    {
        EngineeringDiagnosticLevelDefinition definition = EngineeringDiagnosticLifecycle.GetDefinition(level);

        Assert.True(definition.RequiresReason);
        Assert.True(definition.RequiresExplicitAuthorization);
    }

    [Theory]
    [InlineData(EngineeringDiagnosticLevel.Level5Scan, EngineeringDiagnosticLevel.Level4Analysis)]
    [InlineData(EngineeringDiagnosticLevel.Level4Analysis, EngineeringDiagnosticLevel.Level3Verification)]
    [InlineData(EngineeringDiagnosticLevel.Level3Verification, EngineeringDiagnosticLevel.Level2Repair)]
    [InlineData(EngineeringDiagnosticLevel.Level2Repair, EngineeringDiagnosticLevel.Level1CriticalIntervention)]
    public void Moving_toward_level_one_is_escalation(
        EngineeringDiagnosticLevel current,
        EngineeringDiagnosticLevel requested)
    {
        Assert.True(EngineeringDiagnosticLifecycle.IsEscalation(current, requested));
        Assert.False(EngineeringDiagnosticLifecycle.IsDeEscalation(current, requested));
    }

    [Fact]
    public void Lifecycle_does_not_require_sequential_escalation()
    {
        Assert.True(EngineeringDiagnosticLifecycle.IsEscalation(
            EngineeringDiagnosticLevel.Level5Scan,
            EngineeringDiagnosticLevel.Level1CriticalIntervention));
    }
}
