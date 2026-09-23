using Common.Diagnostics;
using Xunit;

namespace Common.Diagnostics.UnitTests;

public sealed class LevelXContractsTests
{
    [Fact]
    public void StarfleetOrder_IsFiveThroughOne_WithIncreasingDepth()
    {
        Assert.Equal(
            [
                EngineeringDiagnosticLevel.Level5Scan,
                EngineeringDiagnosticLevel.Level4Analysis,
                EngineeringDiagnosticLevel.Level3Verification,
                EngineeringDiagnosticLevel.Level2Repair,
                EngineeringDiagnosticLevel.Level1CriticalIntervention
            ],
            EngineeringDiagnosticLevelSemantics.StarfleetOrder);

        int[] depths = EngineeringDiagnosticLevelSemantics.StarfleetOrder
            .Select(EngineeringDiagnosticLevelSemantics.Depth)
            .ToArray();

        Assert.Equal([1, 2, 3, 4, 5], depths);
    }

    [Theory]
    [InlineData(EngineeringDiagnosticLevel.Level5Scan, false)]
    [InlineData(EngineeringDiagnosticLevel.Level4Analysis, false)]
    [InlineData(EngineeringDiagnosticLevel.Level3Verification, false)]
    [InlineData(EngineeringDiagnosticLevel.Level2Repair, true)]
    [InlineData(EngineeringDiagnosticLevel.Level1CriticalIntervention, true)]
    public void DisruptionRule_MatchesStarfleetDepth(EngineeringDiagnosticLevel level, bool disruptive)
        => Assert.Equal(disruptive, EngineeringDiagnosticLevelSemantics.IsDisruptive(level));

    [Fact]
    public void RegisteredApplicationTarget_UsesApplicationAndInstance()
    {
        DiagnosticTarget target = DiagnosticTarget.RegisteredApplication("Aegis.Hello", "Production");

        Assert.Equal(DiagnosticTargetType.RegisteredApplication, target.Type);
        Assert.Equal("Aegis.Hello/Production", target.TargetId);
        Assert.Equal("Aegis.Hello", target.ApplicationId);
        Assert.Equal("Production", target.InstanceId);
    }

    [Theory]
    [InlineData("operations")]
    [InlineData("configuration")]
    [InlineData("diagnostics")]
    [InlineData("registration")]
    public void ControlPlaneComponents_AreValidTargets(string component)
    {
        DiagnosticTarget target = DiagnosticTarget.ComponentTarget(component);
        Assert.Equal(DiagnosticTargetType.ControlPlaneComponent, target.Type);
        Assert.Equal(component, target.TargetId, ignoreCase: true);
    }

    [Theory]
    [InlineData("Common.Diagnostics")]
    [InlineData("Common.Registration")]
    [InlineData("Common.Security")]
    [InlineData("Common.Secrets")]
    [InlineData("Common.Messaging")]
    [InlineData("Common.Storage")]
    public void CommonComponents_AreValidTargets(string component)
    {
        DiagnosticTarget target = DiagnosticTarget.CommonComponent(component);
        Assert.Equal(DiagnosticTargetType.CommonComponent, target.Type);
        Assert.Equal(component, target.TargetId, ignoreCase: true);
        Assert.Equal(component, target.Component, ignoreCase: true);
    }

    [Fact]
    public async Task TargetAwareRun_PreservesTargetAndCorrelation()
    {
        TestRunStore store = new();
        EngineeringDiagnosticEngine engine = new(store);

        EngineeringDiagnosticRun run = await engine.RunAsync(
            EngineeringDiagnosticLevel.Level5Scan,
            DiagnosticTarget.RegisteredApplication("Aegis.Hello", "Production"),
            "Aegis.Diagnostics",
            "Production",
            "tester",
            null,
            [],
            CancellationToken.None);

        Assert.Equal(DiagnosticTargetType.RegisteredApplication, run.TargetType);
        Assert.Equal("Aegis.Hello/Production", run.TargetId);
        Assert.Equal("Aegis.Hello", run.ApplicationId);
        Assert.Equal("Production", run.InstanceId);
        Assert.Equal(run.RunId, run.CorrelationId);
    }


    [Theory]
    [InlineData(EngineeringDiagnosticLevel.Level5Scan, 5)]
    [InlineData(EngineeringDiagnosticLevel.Level4Analysis, 5, 4)]
    [InlineData(EngineeringDiagnosticLevel.Level3Verification, 5, 4, 3)]
    [InlineData(EngineeringDiagnosticLevel.Level2Repair, 5, 4, 3, 2)]
    [InlineData(EngineeringDiagnosticLevel.Level1CriticalIntervention, 5, 4, 3, 2, 1)]
    public async Task RequestedLevel_RunsOnlyThatDepthAndAllLighterLevels(
        EngineeringDiagnosticLevel requested,
        params int[] expectedLevels)
    {
        TestRunStore store = new();
        EngineeringDiagnosticEngine engine = new(store);
        var executed = new List<int>();

        EngineeringDiagnosticCheckDefinition[] definitions =
        [
            new("l5", "Level 5", EngineeringDiagnosticLevel.Level5Scan, _ => RecordAsync(5)),
            new("l4", "Level 4", EngineeringDiagnosticLevel.Level4Analysis, _ => RecordAsync(4)),
            new("l3", "Level 3", EngineeringDiagnosticLevel.Level3Verification, _ => RecordAsync(3)),
            new("l2", "Level 2", EngineeringDiagnosticLevel.Level2Repair, _ => RecordAsync(2)),
            new("l1", "Level 1", EngineeringDiagnosticLevel.Level1CriticalIntervention, _ => RecordAsync(1))
        ];

        async Task<EngineeringDiagnosticCheckResult> RecordAsync(int level)
        {
            executed.Add(level);
            await Task.CompletedTask;
            return EngineeringDiagnosticPolicy.Passed($"l{level}", $"Level {level}", "PASS");
        }

        string? reason = requested is EngineeringDiagnosticLevel.Level2Repair or EngineeringDiagnosticLevel.Level1CriticalIntervention
            ? "unit-test"
            : null;

        await engine.RunAsync(
            requested,
            DiagnosticTarget.EntireControlPlane(),
            "control-plane",
            "Test",
            "tester",
            reason,
            definitions,
            CancellationToken.None);

        Assert.Equal(expectedLevels, executed);
    }

    private sealed class TestRunStore : IEngineeringDiagnosticRunStore
    {
        public Task<IReadOnlyList<EngineeringDiagnosticRun>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EngineeringDiagnosticRun>>([]);

        public Task<EngineeringDiagnosticRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult<EngineeringDiagnosticRun?>(null);

        public Task SaveAsync(EngineeringDiagnosticRun run, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<EngineeringDiagnosticRun?> ResolveAsync(Guid runId, string resolvedBy, string resolution, CancellationToken cancellationToken = default)
            => Task.FromResult<EngineeringDiagnosticRun?>(null);
    }
}
