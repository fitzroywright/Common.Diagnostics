using Xunit;

namespace Common.Diagnostics.UnitTests;

public sealed class DiagnosticContractV2Tests
{
    [Fact]
    public void Missing_or_unknown_state_is_not_healthy()
        => Assert.NotEqual(OperationalDiagnosticState.Healthy, OperationalDiagnosticState.Unknown);

    [Fact]
    public void Stale_observation_becomes_unknown()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var observation = new DiagnosticObservation(
            "App", "Component", "Instance", "Production",
            DiagnosticCategory.Health, OperationalDiagnosticState.Healthy,
            DiagnosticSeverity.Information, "ok", now.AddMinutes(-10), now.AddMinutes(-10));

        Assert.Equal(
            OperationalDiagnosticState.Unknown,
            observation.EffectiveState(now, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Active_stage_crossing_warning_threshold_becomes_warning()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var stage = new DiagnosticFlowStageObservation(
            "Processing", FlowStageState.Active,
            StartedAtUtc: now.AddSeconds(-10),
            WarningAfter: TimeSpan.FromSeconds(5),
            CriticalAfter: TimeSpan.FromSeconds(30));

        Assert.Equal(FlowStageState.Warning, stage.EffectiveState(now));
    }

    [Fact]
    public void Active_stage_crossing_critical_threshold_times_out()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var stage = new DiagnosticFlowStageObservation(
            "Processing", FlowStageState.Active,
            StartedAtUtc: now.AddSeconds(-60),
            WarningAfter: TimeSpan.FromSeconds(5),
            CriticalAfter: TimeSpan.FromSeconds(30));

        Assert.Equal(FlowStageState.TimedOut, stage.EffectiveState(now));
    }

    [Fact]
    public void Branches_are_independent()
    {
        var email = new DiagnosticFlowStageObservation("Email", FlowStageState.Completed, Branch: "Email");
        var whatsapp = new DiagnosticFlowStageObservation("WhatsApp", FlowStageState.Failed, Branch: "WhatsApp");
        Assert.Equal(FlowStageState.Completed, email.State);
        Assert.Equal(FlowStageState.Failed, whatsapp.State);
    }

    [Fact]
    public void Offline_sync_is_not_failed()
    {
        var sync = new SynchronizationDiagnostic(
            "App", "POS-01", "Instance", false,
            OperationalDiagnosticState.Offline, 12, TimeSpan.FromMinutes(5),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(-10), 0, DateTimeOffset.UtcNow);
        Assert.Equal(OperationalDiagnosticState.Offline, sync.State);
        Assert.NotEqual(OperationalDiagnosticState.Failed, sync.State);
    }

    [Fact]
    public void Queue_diagnostic_retains_dead_letter_distinction()
    {
        var queue = new QueueDiagnostic(
            "App", "outbound", 5, TimeSpan.FromMinutes(2),
            1, 2, 1, 1, OperationalDiagnosticState.Warning, DateTimeOffset.UtcNow);
        Assert.Equal(1, queue.DeadLetterCount);
        Assert.Equal(OperationalDiagnosticState.Warning, queue.State);
    }

    [Fact]
    public void Unsupported_is_not_failed()
        => Assert.NotEqual(OperationalDiagnosticState.Failed, OperationalDiagnosticState.NotSupported);

    [Fact]
    public void Valid_negative_decision_need_not_be_failure()
    {
        var decision = new DecisionDiagnostic(
            "Ebolito", "Router", "UnknownCommand", "Rejected",
            true, OperationalDiagnosticState.Healthy,
            DiagnosticSeverity.Information, DateTimeOffset.UtcNow, "corr-1");
        Assert.True(decision.IsValidBusinessOutcome);
        Assert.Equal(OperationalDiagnosticState.Healthy, decision.State);
    }

    [Fact]
    public void Correlation_is_preserved_on_flow_instance()
    {
        var flow = new DiagnosticFlowInstance(
            "corr-123", "App", "Test", "Instance",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            [new DiagnosticFlowStageObservation("Start", FlowStageState.Completed)]);
        Assert.Equal("corr-123", flow.CorrelationId);
    }
}
