namespace Common.Diagnostics;

public sealed record EngineeringDiagnosticCheckDefinition(
    string CheckId,
    string Name,
    EngineeringDiagnosticLevel IntroducedAt,
    Func<CancellationToken, Task<EngineeringDiagnosticCheckResult>> RunAsync);

public sealed class EngineeringDiagnosticEngine
{
    private readonly IEngineeringDiagnosticRunStore runStore;

    public EngineeringDiagnosticEngine(IEngineeringDiagnosticRunStore runStore)
    {
        this.runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
    }

    public async Task<EngineeringDiagnosticRun> RunAsync(
        EngineeringDiagnosticLevel level,
        string application,
        string environment,
        string requestedBy,
        string? reason,
        IEnumerable<EngineeringDiagnosticCheckDefinition> checkDefinitions,
        CancellationToken cancellationToken = default)
    {
        EngineeringDiagnosticPolicy.ValidateLevel(level);
        EngineeringDiagnosticPolicy.ValidateReason(level, reason);

        if (string.IsNullOrWhiteSpace(application))
        {
            throw new ArgumentException("Application is required.", nameof(application));
        }

        if (string.IsNullOrWhiteSpace(environment))
        {
            throw new ArgumentException("Environment is required.", nameof(environment));
        }

        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            throw new ArgumentException("RequestedBy is required.", nameof(requestedBy));
        }

        ArgumentNullException.ThrowIfNull(checkDefinitions);

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        List<EngineeringDiagnosticCheckResult> checks = [];

        foreach (EngineeringDiagnosticCheckDefinition definition in checkDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((int)level > (int)definition.IntroducedAt)
            {
                continue;
            }

            try
            {
                EngineeringDiagnosticCheckResult result = await definition.RunAsync(cancellationToken);
                checks.Add(result with
                {
                    CheckId = string.IsNullOrWhiteSpace(result.CheckId) ? definition.CheckId : result.CheckId,
                    Name = string.IsNullOrWhiteSpace(result.Name) ? definition.Name : result.Name
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                checks.Add(EngineeringDiagnosticPolicy.Failed(
                    definition.CheckId,
                    definition.Name,
                    exception.Message,
                    exception.GetType().FullName));
            }
        }

        if (level is EngineeringDiagnosticLevel.Level2Repair or EngineeringDiagnosticLevel.Level1CriticalIntervention)
        {
            checks.Add(EngineeringDiagnosticPolicy.CreateInterventionGate(level, reason));
        }

        EngineeringDiagnosticRun run = new(
            Guid.NewGuid(),
            level,
            application.Trim(),
            environment.Trim(),
            requestedBy.Trim(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            startedAt,
            DateTimeOffset.UtcNow,
            EngineeringDiagnosticPolicy.CalculateStatus(checks),
            checks);

        await runStore.SaveAsync(run, cancellationToken);
        return run;
    }
}
