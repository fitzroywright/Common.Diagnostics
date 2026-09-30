namespace Common.Diagnostics;

public static class DependencyDiagnosticRunner
{
    public static async Task<IReadOnlyList<EngineeringDiagnosticCheckResult>> RunAsync(
        IEnumerable<IDependencyDiagnosticProbe> probes,
        EngineeringDiagnosticLevel requestedLevel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probes);
        EngineeringDiagnosticPolicy.ValidateLevel(requestedLevel);

        List<EngineeringDiagnosticCheckResult> results = [];
        foreach (IDependencyDiagnosticProbe probe in probes
            .Where(probe => (int)probe.Level >= (int)requestedLevel)
            .OrderByDescending(probe => (int)probe.Level)
            .ThenBy(probe => probe.Component, StringComparer.Ordinal)
            .ThenBy(probe => probe.Dependency, StringComparer.Ordinal))
        {
            string checkId = BuildCheckId(probe);
            string name = $"{probe.Component} - {probe.Dependency}";
            try
            {
                DependencyVerificationResult result =
                    await probe.VerifyAsync(cancellationToken).ConfigureAwait(false);
                results.Add(DependencyDiagnosticPolicy.ToEngineeringResult(checkId, name, result));
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException ||
                !cancellationToken.IsCancellationRequested)
            {
                results.Add(EngineeringDiagnosticPolicy.Failed(
                    checkId,
                    name,
                    "Dependency verification threw an exception.",
                    exception.GetType().Name));
            }
        }

        return results;
    }

    public static string BuildCheckId(IDependencyDiagnosticProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        string component = Sanitize(probe.Component);
        string dependency = Sanitize(probe.Dependency);
        return $"{component}.{dependency}.L{(int)probe.Level}".ToUpperInvariant();
    }

    private static string Sanitize(string value)
    {
        char[] chars = value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? character : '.')
            .ToArray();
        return string.Join(
            ".",
            new string(chars)
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
