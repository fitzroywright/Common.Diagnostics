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

    public Task<EngineeringDiagnosticRun> RunAsync(
        EngineeringDiagnosticLevel level,
        DiagnosticTarget target,
        string application,
        string environment,
        string requestedBy,
        string? reason,
        IEnumerable<EngineeringDiagnosticCheckDefinition> checkDefinitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return RunCoreAsync(
            level,
            target,
            application,
            environment,
            requestedBy,
            reason,
            checkDefinitions,
            cancellationToken);
    }

    public Task<EngineeringDiagnosticRun> RunAsync(
        EngineeringDiagnosticLevel level,
        string application,
        string environment,
        string requestedBy,
        string? reason,
        IEnumerable<EngineeringDiagnosticCheckDefinition> checkDefinitions,
        CancellationToken cancellationToken = default)
        => RunCoreAsync(
            level,
            DiagnosticTarget.EntireControlPlane(),
            application,
            environment,
            requestedBy,
            reason,
            checkDefinitions,
            cancellationToken);

    private async Task<EngineeringDiagnosticRun> RunCoreAsync(
        EngineeringDiagnosticLevel level,
        DiagnosticTarget target,
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

        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        DateTimeOffset startedAt = requestedAt;
        Guid runId = Guid.NewGuid();
        List<EngineeringDiagnosticCheckResult> checks = [];

        EngineeringDiagnosticCheckDefinition[] selectedDefinitions = checkDefinitions
            .Where(definition => (int)level <= (int)definition.IntroducedAt)
            .ToArray();

        bool addInterventionGate =
            level is EngineeringDiagnosticLevel.Level2Repair or EngineeringDiagnosticLevel.Level1CriticalIntervention;
        int totalStages = selectedDefinitions.Length + (addInterventionGate ? 1 : 0);
        int completedStages = 0;

        EngineeringDiagnosticRun running = new(
            runId,
            level,
            application.Trim(),
            environment.Trim(),
            requestedBy.Trim(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            startedAt,
            startedAt,
            EngineeringDiagnosticStatus.Passed,
            checks,
            CorrelationId: runId,
            TargetType: target.Type,
            TargetId: target.TargetId,
            ApplicationId: target.ApplicationId,
            InstanceId: target.InstanceId,
            RequestedAtUtc: requestedAt,
            RunState: EngineeringDiagnosticRunState.Running,
            CurrentStage: selectedDefinitions.FirstOrDefault()?.Name ?? (addInterventionGate ? "Intervention gate" : "Completing"),
            ProgressPercent: 0);

        await runStore.SaveAsync(running, cancellationToken);

        try
        {
            foreach (EngineeringDiagnosticCheckDefinition definition in selectedDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

                completedStages++;
                string? nextStage = selectedDefinitions
                    .Skip(completedStages)
                    .Select(x => x.Name)
                    .FirstOrDefault();

                running = running with
                {
                    CompletedAt = DateTimeOffset.UtcNow,
                    Status = EngineeringDiagnosticPolicy.CalculateStatus(checks),
                    Checks = checks.ToArray(),
                    CurrentStage = nextStage ?? (addInterventionGate ? "Intervention gate" : "Completing"),
                    ProgressPercent = totalStages == 0 ? 100 : Math.Clamp(completedStages * 100 / totalStages, 0, 99)
                };
                await runStore.SaveAsync(running, cancellationToken);
            }

            if (addInterventionGate)
            {
                checks.Add(EngineeringDiagnosticPolicy.CreateInterventionGate(level, reason));
                completedStages++;
            }

            EngineeringDiagnosticRun completed = running with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                Status = EngineeringDiagnosticPolicy.CalculateStatus(checks),
                Checks = checks.ToArray(),
                RunState = EngineeringDiagnosticRunState.Completed,
                CurrentStage = "Completed",
                ProgressPercent = 100
            };

            await runStore.SaveAsync(completed, cancellationToken);
            return completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            EngineeringDiagnosticRun cancelled = running with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                Status = EngineeringDiagnosticPolicy.CalculateStatus(checks),
                Checks = checks.ToArray(),
                RunState = EngineeringDiagnosticRunState.Cancelled,
                CurrentStage = "Cancelled",
                ProgressPercent = totalStages == 0 ? 0 : Math.Clamp(completedStages * 100 / totalStages, 0, 99)
            };

            await runStore.SaveAsync(cancelled, CancellationToken.None);
            throw;
        }
    }
}
