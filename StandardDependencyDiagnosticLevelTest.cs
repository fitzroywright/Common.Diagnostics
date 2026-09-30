namespace Common.Diagnostics;

public sealed class StandardDependencyDiagnosticLevelTest(
    IEnumerable<IDependencyDiagnosticProbe> probes) : IDiagnosticLevelLocalTest
{
    private readonly IDependencyDiagnosticProbe[] probes =
        (probes ?? throw new ArgumentNullException(nameof(probes))).ToArray();

    public string TestId => "COMMON.DIAGNOSTICS.L3.DEPENDENCIES.VERIFIED";
    public string Name => "Standard dependency verification";
    public string Owner => "Common.Diagnostics";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level3Verification;
    public bool IsDestructive => false;

    public async Task<EngineeringDiagnosticCheckResult> RunAsync(
        CancellationToken cancellationToken)
    {
        if (probes.Length == 0)
        {
            return EngineeringDiagnosticPolicy.Warning(
                TestId,
                Name,
                "No standardized dependency diagnostic probes are registered.");
        }

        IReadOnlyList<EngineeringDiagnosticCheckResult> results =
            await DependencyDiagnosticRunner.RunAsync(
                probes,
                EngineeringDiagnosticLevel.Level3Verification,
                cancellationToken).ConfigureAwait(false);

        EngineeringDiagnosticCheckResult[] failed =
            results.Where(result => result.Status == EngineeringDiagnosticStatus.Failed).ToArray();
        EngineeringDiagnosticCheckResult[] warnings =
            results.Where(result => result.Status == EngineeringDiagnosticStatus.Warning).ToArray();
        string evidence = string.Join(
            " | ",
            results.Select(result =>
                $"{result.CheckId}: {result.Status}; {result.Evidence ?? result.Summary}"));

        if (failed.Length > 0)
        {
            return EngineeringDiagnosticPolicy.Failed(
                TestId,
                Name,
                $"{failed.Length} standardized dependency verification check(s) failed.",
                evidence);
        }

        if (warnings.Length > 0)
        {
            return EngineeringDiagnosticPolicy.Warning(
                TestId,
                Name,
                $"{warnings.Length} standardized dependency verification check(s) returned warnings.",
                evidence);
        }

        return EngineeringDiagnosticPolicy.Passed(
            TestId,
            Name,
            $"{results.Count} standardized dependency verification check(s) passed.",
            evidence);
    }
}
