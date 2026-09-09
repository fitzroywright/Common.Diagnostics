namespace Common.Diagnostics.UnitTests;

using Xunit;

public sealed class TelemetryOperationsTests
{
    [Fact]
    public async Task CriticalRoutesToAllDefaultDestinations()
    {
        RecordingDestination graylog = new("Graylog");
        RecordingDestination wazuh = new("Wazuh");
        RecordingDestination slack = new("Slack");
        RecordingDestination email = new("Email");
        RecordingRetentionStore retention = new();
        DiagnosticOperationsRouter router = new(
            [graylog, wazuh, slack, email],
            new InMemoryDiagnosticSuppressionStore(),
            retention);
        DiagnosticTelemetryEvent telemetryEvent = new(
            DateTimeOffset.UtcNow,
            "Common.Secrets",
            "SECRETS031",
            DiagnosticSeverity.Critical,
            "Secret provider unavailable.");

        await router.RouteAsync(telemetryEvent);

        Assert.Single(graylog.Events);
        Assert.Single(wazuh.Events);
        Assert.Single(slack.Events);
        Assert.Single(email.Events);
        Assert.Single(retention.Events);
        Assert.True(retention.Events[0].ExpiresAt - telemetryEvent.Timestamp >= TimeSpan.FromDays(364));
    }

    [Fact]
    public async Task WarningRoutesOnlyToGraylogAndWazuh()
    {
        RecordingDestination graylog = new("Graylog");
        RecordingDestination wazuh = new("Wazuh");
        RecordingDestination slack = new("Slack");
        DiagnosticOperationsRouter router = new(
            [graylog, wazuh, slack],
            new InMemoryDiagnosticSuppressionStore(),
            new RecordingRetentionStore());

        await router.RouteAsync(new DiagnosticTelemetryEvent(
            DateTimeOffset.UtcNow,
            "Common.Messaging",
            "MSG201",
            DiagnosticSeverity.Warning,
            "Delivery retry scheduled."));

        Assert.Single(graylog.Events);
        Assert.Single(wazuh.Events);
        Assert.Empty(slack.Events);
    }

    [Fact]
    public async Task DuplicateEventIsSuppressedInsideWindow()
    {
        RecordingDestination graylog = new("Graylog");
        DiagnosticOperationsRouter router = new(
            [graylog],
            new InMemoryDiagnosticSuppressionStore(),
            new RecordingRetentionStore());
        DiagnosticTelemetryEvent telemetryEvent = new(
            DateTimeOffset.UtcNow,
            "Common.Diagnostics",
            "DIAG500",
            DiagnosticSeverity.Information,
            "Repeated event.");

        await router.RouteAsync(telemetryEvent);
        await router.RouteAsync(telemetryEvent);

        Assert.Single(graylog.Events);
    }

    private sealed class RecordingDestination : IDiagnosticTelemetryDestination
    {
        public RecordingDestination(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public List<DiagnosticTelemetryEvent> Events { get; } = [];

        public Task WriteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(telemetryEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRetentionStore : IDiagnosticRetentionStore
    {
        public List<(DiagnosticTelemetryEvent Event, DateTimeOffset ExpiresAt)> Events { get; } = [];

        public Task AppendAsync(DiagnosticTelemetryEvent telemetryEvent, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
        {
            Events.Add((telemetryEvent, expiresAt));
            return Task.CompletedTask;
        }

        public Task PurgeExpiredAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
