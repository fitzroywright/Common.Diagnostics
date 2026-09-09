namespace Common.Diagnostics.UnitTests;

using Xunit;

public sealed class DiagnosticLevelTests
{
    [Theory]
    [InlineData(DiagnosticLevel.Level1, 1)]
    [InlineData(DiagnosticLevel.Level2, 2)]
    [InlineData(DiagnosticLevel.Level3, 3)]
    [InlineData(DiagnosticLevel.Level4, 4)]
    [InlineData(DiagnosticLevel.Level5, 5)]
    public async Task RequestedLevelIncludesAllLowerLevels(DiagnosticLevel level, int expected)
    {
        IDiagnosticCheck[] checks =
        [
            new TestCheck("L1", DiagnosticLevel.Level1),
            new TestCheck("L2", DiagnosticLevel.Level2),
            new TestCheck("L3", DiagnosticLevel.Level3),
            new TestCheck("L4", DiagnosticLevel.Level4),
            new TestCheck("L5", DiagnosticLevel.Level5)
        ];
        LeveledDiagnosticRunner runner = new(checks);

        IReadOnlyList<DiagnosticResult> results = await runner.RunAsync(level);

        Assert.Equal(expected, results.Count);
    }

    [Fact]
    public void DefinitionsRunFromLevel5ToLevel1AndHaveOperationalMeaning()
    {
        Assert.Equal("Full System Diagnostics", DiagnosticLevels.Definitions[DiagnosticLevel.Level5].Name);
        Assert.Contains("Disaster recovery readiness", DiagnosticLevels.Definitions[DiagnosticLevel.Level5].Capabilities);
        Assert.Equal("Integration Diagnostics", DiagnosticLevels.Definitions[DiagnosticLevel.Level4].Name);
        Assert.Equal("System Diagnostics", DiagnosticLevels.Definitions[DiagnosticLevel.Level3].Name);
        Assert.Equal("Component Diagnostics", DiagnosticLevels.Definitions[DiagnosticLevel.Level2].Name);
        Assert.Equal("Quick Diagnostics", DiagnosticLevels.Definitions[DiagnosticLevel.Level1].Name);
    }

    private sealed class TestCheck : ILeveledDiagnosticCheck
    {
        public TestCheck(string name, DiagnosticLevel level)
        {
            Name = name;
            Level = level;
        }

        public string Name { get; }
        public DiagnosticLevel Level { get; }

        public Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DiagnosticResult(
                Name,
                DiagnosticStatus.Healthy,
                "Nominal.",
                TimeSpan.Zero));
        }
    }
}
