namespace Common.Diagnostics;

public enum DiagnosticSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

public sealed record DiagnosticTelemetryEvent(
    DateTimeOffset Timestamp,
    string Source,
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record DiagnosticEscalationRule(
    DiagnosticSeverity MinimumSeverity,
    TimeSpan SuppressionWindow,
    TimeSpan Retention,
    IReadOnlyCollection<string> Destinations);

public sealed class DiagnosticOperationsOptions
{
    public IReadOnlyDictionary<DiagnosticSeverity, DiagnosticEscalationRule> Rules { get; init; }
        = CreateDefaults();

    public static IReadOnlyDictionary<DiagnosticSeverity, DiagnosticEscalationRule> CreateDefaults()
    {
        return new Dictionary<DiagnosticSeverity, DiagnosticEscalationRule>
        {
            [DiagnosticSeverity.Information] = new(
                DiagnosticSeverity.Information,
                TimeSpan.FromMinutes(15),
                TimeSpan.FromDays(14),
                ["Graylog"]),
            [DiagnosticSeverity.Warning] = new(
                DiagnosticSeverity.Warning,
                TimeSpan.FromMinutes(10),
                TimeSpan.FromDays(30),
                ["Graylog", "Wazuh"]),
            [DiagnosticSeverity.Error] = new(
                DiagnosticSeverity.Error,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromDays(90),
                ["Graylog", "Wazuh", "Slack"]),
            [DiagnosticSeverity.Critical] = new(
                DiagnosticSeverity.Critical,
                TimeSpan.FromMinutes(2),
                TimeSpan.FromDays(365),
                ["Graylog", "Wazuh", "Slack", "Email"])
        };
    }
}

public interface IDiagnosticTelemetryDestination
{
    string Name { get; }

    Task WriteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default);
}

public interface IDiagnosticSuppressionStore
{
    Task<bool> ShouldSuppressAsync(DiagnosticTelemetryEvent telemetryEvent, TimeSpan suppressionWindow, CancellationToken cancellationToken = default);
    Task MarkSentAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default);
}

public interface IDiagnosticRetentionStore
{
    Task AppendAsync(DiagnosticTelemetryEvent telemetryEvent, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task PurgeExpiredAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryDiagnosticSuppressionStore : IDiagnosticSuppressionStore
{
    private readonly Dictionary<string, DateTimeOffset> lastSent = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public Task<bool> ShouldSuppressAsync(DiagnosticTelemetryEvent telemetryEvent, TimeSpan suppressionWindow, CancellationToken cancellationToken = default)
    {
        string key = GetKey(telemetryEvent);
        lock (gate)
        {
            return Task.FromResult(lastSent.TryGetValue(key, out DateTimeOffset sentAt) && DateTimeOffset.UtcNow - sentAt < suppressionWindow);
        }
    }

    public Task MarkSentAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            lastSent[GetKey(telemetryEvent)] = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    private static string GetKey(DiagnosticTelemetryEvent telemetryEvent)
    {
        return $"{telemetryEvent.Source}|{telemetryEvent.Code}|{telemetryEvent.Severity}";
    }
}

public sealed class NullDiagnosticRetentionStore : IDiagnosticRetentionStore
{
    public Task AppendAsync(DiagnosticTelemetryEvent telemetryEvent, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public interface IDiagnosticOperationsRouter
{
    Task RouteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default);
}

public sealed class DiagnosticOperationsRouter : IDiagnosticOperationsRouter
{
    private readonly IReadOnlyDictionary<string, IDiagnosticTelemetryDestination> destinations;
    private readonly IDiagnosticSuppressionStore suppressionStore;
    private readonly IDiagnosticRetentionStore retentionStore;
    private readonly DiagnosticOperationsOptions options;

    public DiagnosticOperationsRouter(
        IEnumerable<IDiagnosticTelemetryDestination> destinations,
        IDiagnosticSuppressionStore suppressionStore,
        IDiagnosticRetentionStore retentionStore,
        DiagnosticOperationsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(destinations);
        this.destinations = destinations.ToDictionary(destination => destination.Name, StringComparer.OrdinalIgnoreCase);
        this.suppressionStore = suppressionStore ?? throw new ArgumentNullException(nameof(suppressionStore));
        this.retentionStore = retentionStore ?? throw new ArgumentNullException(nameof(retentionStore));
        this.options = options ?? new DiagnosticOperationsOptions();
    }

    public async Task RouteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
    {
        if (!options.Rules.TryGetValue(telemetryEvent.Severity, out DiagnosticEscalationRule? rule))
        {
            return;
        }

        await retentionStore.AppendAsync(telemetryEvent, telemetryEvent.Timestamp.Add(rule.Retention), cancellationToken).ConfigureAwait(false);

        if (await suppressionStore.ShouldSuppressAsync(telemetryEvent, rule.SuppressionWindow, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (string destinationName in rule.Destinations)
        {
            if (destinations.TryGetValue(destinationName, out IDiagnosticTelemetryDestination? destination))
            {
                await destination.WriteAsync(telemetryEvent, cancellationToken).ConfigureAwait(false);
            }
        }

        await suppressionStore.MarkSentAsync(telemetryEvent, cancellationToken).ConfigureAwait(false);
    }
}
