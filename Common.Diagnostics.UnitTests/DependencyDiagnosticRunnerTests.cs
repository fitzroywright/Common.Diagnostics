using Common.Diagnostics;
using Xunit;

namespace Common.Diagnostics.UnitTests;

public sealed class DependencyDiagnosticRunnerTests
{
    [Fact]
    public async Task RunnerUsesStandardDependencyResultShape()
    {
        IDependencyDiagnosticProbe probe = new StubProbe(
            EngineeringDiagnosticLevel.Level3Verification,
            new DependencyVerificationResult(
                "Common.Messaging",
                "Microsoft Graph Email",
                DependencyDiagnosticKind.Messaging,
                true,
                true,
                true,
                OperationalDiagnosticState.Healthy,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(12),
                "Provider verified.",
                "Mail.Send granted."));

        IReadOnlyList<EngineeringDiagnosticCheckResult> results =
            await DependencyDiagnosticRunner.RunAsync(
                [probe],
                EngineeringDiagnosticLevel.Level3Verification);

        EngineeringDiagnosticCheckResult result = Assert.Single(results);
        Assert.Equal(EngineeringDiagnosticStatus.Passed, result.Status);
        Assert.Contains("Configured=True", result.Evidence);
        Assert.Contains("Reachable=True", result.Evidence);
        Assert.Contains("Functional=True", result.Evidence);
        Assert.DoesNotContain("secret", result.Evidence ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunnerHonorsCumulativeDiagnosticLevel()
    {
        IDependencyDiagnosticProbe level4 = new StubProbe(
            EngineeringDiagnosticLevel.Level4Analysis,
            Healthy("Common.Storage", "SharePoint"));
        IDependencyDiagnosticProbe level3 = new StubProbe(
            EngineeringDiagnosticLevel.Level3Verification,
            Healthy("Common.Messaging", "Microsoft Graph Email"));

        IReadOnlyList<EngineeringDiagnosticCheckResult> level4Results =
            await DependencyDiagnosticRunner.RunAsync(
                [level4, level3],
                EngineeringDiagnosticLevel.Level4Analysis);

        Assert.Single(level4Results);

        IReadOnlyList<EngineeringDiagnosticCheckResult> level3Results =
            await DependencyDiagnosticRunner.RunAsync(
                [level4, level3],
                EngineeringDiagnosticLevel.Level3Verification);

        Assert.Equal(2, level3Results.Count);
    }

    private static DependencyVerificationResult Healthy(string component, string dependency) =>
        new(
            component,
            dependency,
            DependencyDiagnosticKind.Other,
            true,
            true,
            true,
            OperationalDiagnosticState.Healthy,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            "Healthy.");

    private sealed class StubProbe(
        EngineeringDiagnosticLevel level,
        DependencyVerificationResult result) : IDependencyDiagnosticProbe
    {
        public string Component => result.Component;
        public string Dependency => result.Dependency;
        public DependencyDiagnosticKind Kind => result.Kind;
        public EngineeringDiagnosticLevel Level => level;

        public Task<DependencyVerificationResult> VerifyAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
