namespace Common.Diagnostics;

public enum LifecycleEventOutcome { Started = 0, Succeeded = 1, Warning = 2, Failed = 3, Skipped = 4 }

public sealed record LifecycleEvent(
    Guid EventId, string ApplicationId, string InstanceId, string Flow, string Stage,
    LifecycleEventOutcome Outcome, DateTimeOffset OccurredAtUtc, string CorrelationId,
    string? OperationId = null, string? RelatedBusinessId = null, string? Code = null,
    string? Detail = null, IReadOnlyDictionary<string, string>? Properties = null)
{
    public static LifecycleEvent Create(
        string applicationId, string instanceId, string flow, string stage,
        LifecycleEventOutcome outcome, string correlationId, string? operationId = null,
        string? relatedBusinessId = null, string? code = null, string? detail = null,
        IReadOnlyDictionary<string, string>? properties = null, DateTimeOffset? occurredAtUtc = null)
    {
        Require(applicationId, nameof(applicationId)); Require(instanceId, nameof(instanceId));
        Require(flow, nameof(flow)); Require(stage, nameof(stage)); Require(correlationId, nameof(correlationId));
        ValidateProperties(properties);
        return new(Guid.NewGuid(), applicationId.Trim(), instanceId.Trim(), flow.Trim(), stage.Trim(),
            outcome, occurredAtUtc ?? DateTimeOffset.UtcNow, correlationId.Trim(), Trim(operationId),
            Trim(relatedBusinessId), Trim(code), Trim(detail), properties);
    }

    private static void ValidateProperties(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null) return;
        foreach (KeyValuePair<string, string> item in properties)
        {
            Require(item.Key, nameof(properties));
            if (LooksSensitive(item.Key))
                throw new ArgumentException($"Lifecycle property '{item.Key}' is not permitted because it may contain sensitive data.", nameof(properties));
            if (item.Value?.Length > 1024)
                throw new ArgumentException("Lifecycle property values must be 1024 characters or fewer.", nameof(properties));
        }
    }

    public static bool LooksSensitive(string name)
    {
        string value = name.Replace("-", string.Empty).Replace("_", string.Empty);
        return value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || value.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || value.Contains("connectionstring", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Require(string? value, string parameter)
    { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Lifecycle telemetry identity fields are required.", parameter); }
}

public interface ILifecycleEventSink
{
    Task EmitAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken = default);
}

public sealed class NullLifecycleEventSink : ILifecycleEventSink
{
    public Task EmitAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class LifecycleTelemetry
{
    private readonly ILifecycleEventSink sink;
    public LifecycleTelemetry(ILifecycleEventSink sink) => this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
    public Task EmitAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(lifecycleEvent); return sink.EmitAsync(lifecycleEvent, cancellationToken); }
}
