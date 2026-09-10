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

    [Fact]
    public async Task FailingDestination_DoesNotBlockOtherDestinationsOrEscapeToApplication()
    {
        ThrowingDestination graylog = new("Graylog");
        RecordingDestination wazuh = new("Wazuh");
        DiagnosticOperationsRouter router = new(
            [graylog, wazuh],
            new InMemoryDiagnosticSuppressionStore(),
            new RecordingRetentionStore());

        await router.RouteAsync(new DiagnosticTelemetryEvent(
            DateTimeOffset.UtcNow,
            "Aegis.Cafeteria",
            "CAF500",
            DiagnosticSeverity.Warning,
            "Test warning."));

        Assert.Equal(1, graylog.Attempts);
        Assert.Single(wazuh.Events);
    }

    [Fact]
    public async Task RetentionFailure_DoesNotBlockLiveDelivery()
    {
        RecordingDestination graylog = new("Graylog");
        DiagnosticOperationsRouter router = new(
            [graylog],
            new InMemoryDiagnosticSuppressionStore(),
            new ThrowingRetentionStore());

        await router.RouteAsync(new DiagnosticTelemetryEvent(
            DateTimeOffset.UtcNow,
            "Aegis.Studio",
            "STUDIO500",
            DiagnosticSeverity.Information,
            "Test information."));

        Assert.Single(graylog.Events);
    }

    [Fact]
    public async Task SuppressionReadFailure_FailsOpenAndDeliversTelemetry()
    {
        RecordingDestination graylog = new("Graylog");
        DiagnosticOperationsRouter router = new(
            [graylog],
            new ThrowingSuppressionStore(throwOnRead: true, throwOnMark: false),
            new RecordingRetentionStore());

        await router.RouteAsync(new DiagnosticTelemetryEvent(
            DateTimeOffset.UtcNow,
            "RequestPortal",
            "PORTAL500",
            DiagnosticSeverity.Information,
            "Test information."));

        Assert.Single(graylog.Events);
    }

    [Fact]
    public async Task SuppressionMarkFailure_DoesNotEscapeAfterSuccessfulDelivery()
    {
        RecordingDestination graylog = new("Graylog");
        DiagnosticOperationsRouter router = new(
            [graylog],
            new ThrowingSuppressionStore(throwOnRead: false, throwOnMark: true),
            new RecordingRetentionStore());

        await router.RouteAsync(new DiagnosticTelemetryEvent(
            DateTimeOffset.UtcNow,
            "RequestPortal",
            "PORTAL501",
            DiagnosticSeverity.Information,
            "Test information."));

        Assert.Single(graylog.Events);
    }

    [Fact]
    public async Task CallerCancellation_IsNotSwallowed()
    {
        RecordingDestination graylog = new("Graylog");
        DiagnosticOperationsRouter router = new(
            [graylog],
            new InMemoryDiagnosticSuppressionStore(),
            new RecordingRetentionStore());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => router.RouteAsync(new DiagnosticTelemetryEvent(
            DateTimeOffset.UtcNow,
            "Common.Diagnostics",
            "DIAG-CANCEL",
            DiagnosticSeverity.Information,
            "Cancelled."), cancellation.Token));

        Assert.Empty(graylog.Events);
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

    private sealed class ThrowingDestination : IDiagnosticTelemetryDestination
    {
        public ThrowingDestination(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int Attempts { get; private set; }

        public Task WriteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("Destination unavailable.");
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

    private sealed class ThrowingRetentionStore : IDiagnosticRetentionStore
    {
        public Task AppendAsync(DiagnosticTelemetryEvent telemetryEvent, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
            => throw new IOException("Retention unavailable.");

        public Task PurgeExpiredAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingSuppressionStore(bool throwOnRead, bool throwOnMark) : IDiagnosticSuppressionStore
    {
        public Task<bool> ShouldSuppressAsync(DiagnosticTelemetryEvent telemetryEvent, TimeSpan suppressionWindow, CancellationToken cancellationToken = default)
        {
            if (throwOnRead) throw new IOException("Suppression store unavailable.");
            return Task.FromResult(false);
        }

        public Task MarkSentAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
        {
            if (throwOnMark) throw new IOException("Suppression store unavailable.");
            return Task.CompletedTask;
        }
    }
}
