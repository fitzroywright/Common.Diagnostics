using Common.Diagnostics;
using Xunit;

namespace Common.Diagnostics.UnitTests;

public sealed class EngineeringDiagnosticEngineTests
{
    [Fact]
    public async Task LevelFour_RunsLevelFiveAndLevelFourChecksOnly()
    {
        InMemoryRunStore store = new();
        EngineeringDiagnosticEngine engine = new(store);
        List<string> executed = [];
        EngineeringDiagnosticCheckDefinition[] checks =
        [
            Definition("level5", EngineeringDiagnosticLevel.Level5Scan, executed),
            Definition("level4", EngineeringDiagnosticLevel.Level4Analysis, executed),
            Definition("level3", EngineeringDiagnosticLevel.Level3Verification, executed)
        ];

        EngineeringDiagnosticRun run = await engine.RunAsync(
            EngineeringDiagnosticLevel.Level4Analysis,
            "TestApp",
            "Test",
            "tester",
            null,
            checks);

        Assert.Equal(["level5", "level4"], executed);
        Assert.Equal(2, run.Checks.Count);
        Assert.Equal(EngineeringDiagnosticStatus.Passed, run.Status);
        Assert.NotNull(store.Saved);
    }

    [Fact]
    public async Task CheckException_IsRecordedAsFailureWithoutAbortingRun()
    {
        InMemoryRunStore store = new();
        EngineeringDiagnosticEngine engine = new(store);
        EngineeringDiagnosticCheckDefinition[] checks =
        [
            new(
                "broken",
                "Broken check",
                EngineeringDiagnosticLevel.Level5Scan,
                _ => throw new InvalidOperationException("boom")),
            new(
                "healthy",
                "Healthy check",
                EngineeringDiagnosticLevel.Level5Scan,
                _ => Task.FromResult(EngineeringDiagnosticPolicy.Passed("healthy", "Healthy check", "ok")))
        ];

        EngineeringDiagnosticRun run = await engine.RunAsync(
            EngineeringDiagnosticLevel.Level5Scan,
            "TestApp",
            "Test",
            "tester",
            null,
            checks);

        Assert.Equal(2, run.Checks.Count);
        Assert.Equal(EngineeringDiagnosticStatus.Failed, run.Status);
        Assert.Contains(run.Checks, check => check.CheckId == "broken" && check.Status == EngineeringDiagnosticStatus.Failed);
        Assert.Contains(run.Checks, check => check.CheckId == "healthy" && check.Status == EngineeringDiagnosticStatus.Passed);
    }

    [Fact]
    public async Task LevelTwo_AddsInterventionGateAndPersistsRun()
    {
        InMemoryRunStore store = new();
        EngineeringDiagnosticEngine engine = new(store);

        EngineeringDiagnosticRun run = await engine.RunAsync(
            EngineeringDiagnosticLevel.Level2Repair,
            "TestApp",
            "Production",
            "engineer",
            "repair approved",
            [],
            CancellationToken.None);

        Assert.Equal(EngineeringDiagnosticStatus.InterventionRequired, run.Status);
        Assert.Single(run.Checks);
        Assert.Equal("repair-gate", run.Checks[0].CheckId);
        Assert.Equal(run.RunId, store.Saved?.RunId);
    }

    private static EngineeringDiagnosticCheckDefinition Definition(
        string id,
        EngineeringDiagnosticLevel introducedAt,
        List<string> executed)
        => new(
            id,
            id,
            introducedAt,
            _ =>
            {
                executed.Add(id);
                return Task.FromResult(EngineeringDiagnosticPolicy.Passed(id, id, "ok"));
            });

    private sealed class InMemoryRunStore : IEngineeringDiagnosticRunStore
    {
        public EngineeringDiagnosticRun? Saved { get; private set; }

        public Task<IReadOnlyList<EngineeringDiagnosticRun>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EngineeringDiagnosticRun>>(Saved is null ? [] : [Saved]);

        public Task<EngineeringDiagnosticRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult(Saved?.RunId == runId ? Saved : null);

        public Task SaveAsync(EngineeringDiagnosticRun run, CancellationToken cancellationToken = default)
        {
            Saved = run;
            return Task.CompletedTask;
        }

        public Task<EngineeringDiagnosticRun?> ResolveAsync(Guid runId, string resolvedBy, string resolution, CancellationToken cancellationToken = default)
            => Task.FromResult<EngineeringDiagnosticRun?>(null);
    }
}
