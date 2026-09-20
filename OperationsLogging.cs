using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Common.Diagnostics;

public sealed record OperationsLogRecord(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string ApplicationId,
    string InstanceId,
    string Environment,
    string Level,
    string Category,
    string Message,
    string? ExceptionType = null,
    string? ExceptionMessage = null,
    string? CorrelationId = null,
    string? TraceId = null,
    string? SpanId = null,
    string? OperationId = null,
    string? Host = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record OperationsLogCredential(
    string ApplicationId,
    string InstanceId,
    string InstallationId,
    string Credential);

public interface IOperationsLogCredentialSource
{
    ValueTask<OperationsLogCredential?> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class OperationsLogPublisherOptions
{
    public required Uri Endpoint { get; init; }
    public required string ApplicationId { get; init; }
    public required string InstanceId { get; init; }
    public string Environment { get; init; } = "Unknown";
    public string Host { get; init; } = System.Environment.MachineName;
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
    public int QueueCapacity { get; init; } = 5000;
    public int BatchSize { get; init; } = 100;
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public ISet<string> ExcludedCategories { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Common.Diagnostics.OperationsLogLoggerProvider",
        "System.Net.Http.HttpClient.AegisOperationsLogs"
    };
}

public sealed class OperationsLogLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly OperationsLogPublisherOptions options;
    private readonly IOperationsLogCredentialSource credentials;
    private readonly Channel<OperationsLogRecord> queue;
    private readonly CancellationTokenSource stopping = new();
    private readonly HttpClient client;
    private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();
    private readonly Task worker;
    private int disposed;

    public OperationsLogLoggerProvider(
        OperationsLogPublisherOptions options,
        IOperationsLogCredentialSource credentials)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));

        if (!options.Endpoint.IsAbsoluteUri)
            throw new ArgumentException("Operations log endpoint must be absolute.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApplicationId))
            throw new ArgumentException("ApplicationId is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.InstanceId))
            throw new ArgumentException("InstanceId is required.", nameof(options));

        queue = Channel.CreateBounded<OperationsLogRecord>(new BoundedChannelOptions(Math.Clamp(options.QueueCapacity, 100, 100000))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        client = new HttpClient { Timeout = options.RequestTimeout };
        worker = Task.Run(() => RunAsync(stopping.Token));
    }

    public ILogger CreateLogger(string categoryName) =>
        new OperationsLogger(this, categoryName ?? string.Empty);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        this.scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();

    internal bool IsEnabled(string category, LogLevel level) =>
        level != LogLevel.None &&
        level >= options.MinimumLevel &&
        !options.ExcludedCategories.Any(x =>
            category.Equals(x, StringComparison.OrdinalIgnoreCase) ||
            category.StartsWith(x + ".", StringComparison.OrdinalIgnoreCase));

    internal void Write<TState>(
        string category,
        LogLevel level,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(category, level) || Volatile.Read(ref disposed) != 0)
            return;

        string message;
        try { message = formatter(state, exception); }
        catch { message = state?.ToString() ?? string.Empty; }

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? correlationId = null;
        string? operationId = null;

        if (state is IEnumerable<KeyValuePair<string, object?>> statePairs)
            Capture(statePairs, properties, ref correlationId, ref operationId);

        scopeProvider.ForEachScope((scope, bag) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                Capture(pairs, bag.Properties, ref bag.CorrelationId, ref bag.OperationId);
        }, new ScopeCapture(properties, correlationId, operationId));

        // ScopeCapture is a reference object so values updated by the callback are retained.
        var capture = new ScopeCapture(properties, correlationId, operationId);
        scopeProvider.ForEachScope((scope, bag) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                Capture(pairs, bag.Properties, ref bag.CorrelationId, ref bag.OperationId);
        }, capture);
        correlationId = capture.CorrelationId;
        operationId = capture.OperationId;

        Activity? activity = Activity.Current;
        correlationId ??= activity?.GetBaggageItem("CorrelationId");
        operationId ??= activity?.GetBaggageItem("OperationId");

        var record = new OperationsLogRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            options.ApplicationId,
            options.InstanceId,
            options.Environment,
            level.ToString(),
            category,
            Trim(message, 8192) ?? string.Empty,
            exception?.GetType().FullName,
            Trim(exception?.Message, 4096),
            Trim(correlationId, 256),
            activity?.TraceId.ToString(),
            activity?.SpanId.ToString(),
            Trim(operationId, 256),
            options.Host,
            properties.Count == 0 ? null : properties);

        queue.Writer.TryWrite(record);
    }

    private static void Capture(
        IEnumerable<KeyValuePair<string, object?>> values,
        IDictionary<string, string> destination,
        ref string? correlationId,
        ref string? operationId)
    {
        foreach ((string key, object? raw) in values)
        {
            if (string.Equals(key, "{OriginalFormat}", StringComparison.Ordinal))
                continue;
            if (LifecycleEvent.LooksSensitive(key))
                continue;

            string? value = Trim(raw?.ToString(), 1024);
            if (value is null) continue;

            if (key.Equals("CorrelationId", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("X-Correlation-Id", StringComparison.OrdinalIgnoreCase))
                correlationId ??= value;
            else if (key.Equals("OperationId", StringComparison.OrdinalIgnoreCase))
                operationId ??= value;

            destination[key] = value;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var batch = new List<OperationsLogRecord>(Math.Clamp(options.BatchSize, 1, 1000));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                OperationsLogRecord first = await queue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                batch.Add(first);

                DateTimeOffset deadline = DateTimeOffset.UtcNow + options.FlushInterval;
                while (batch.Count < options.BatchSize &&
                       DateTimeOffset.UtcNow < deadline &&
                       queue.Reader.TryRead(out OperationsLogRecord? next))
                    batch.Add(next);

                if (batch.Count < options.BatchSize && DateTimeOffset.UtcNow < deadline)
                {
                    TimeSpan delay = deadline - DateTimeOffset.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        Task delayTask = Task.Delay(delay, delayCts.Token);
                        Task<bool> waitTask = queue.Reader.WaitToReadAsync(delayCts.Token).AsTask();
                        Task completed = await Task.WhenAny(delayTask, waitTask).ConfigureAwait(false);
                        if (completed == waitTask && await waitTask.ConfigureAwait(false))
                        {
                            delayCts.Cancel();
                            while (batch.Count < options.BatchSize && queue.Reader.TryRead(out OperationsLogRecord? next))
                                batch.Add(next);
                        }
                    }
                }

                bool published = await PublishAsync(batch, cancellationToken).ConfigureAwait(false);
                if (!published)
                {
                    foreach (OperationsLogRecord record in batch)
                        queue.Writer.TryWrite(record);

                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Logging must never become an application availability dependency.
                // Requeue with the original EventId so Operations can safely deduplicate
                // if a prior request succeeded but its response was lost.
                foreach (OperationsLogRecord record in batch)
                    queue.Writer.TryWrite(record);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            finally
            {
                batch.Clear();
            }
        }
    }

    private async Task<bool> PublishAsync(
        IReadOnlyList<OperationsLogRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0) return true;

        OperationsLogCredential? credential =
            await credentials.GetAsync(cancellationToken).ConfigureAwait(false);
        if (credential is null || string.IsNullOrWhiteSpace(credential.Credential))
            return false;

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(new { events = records })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Credential);
        request.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", credential.ApplicationId);
        request.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", credential.InstanceId);
        request.Headers.TryAddWithoutValidation("X-Aegis-Installation-Id", credential.InstallationId);

        string? correlation = records.Select(x => x.CorrelationId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!string.IsNullOrWhiteSpace(correlation))
            request.Headers.TryAddWithoutValidation("X-Aegis-Correlation-Id", correlation);

        using HttpResponseMessage response =
            await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        queue.Writer.TryComplete();
        stopping.Cancel();
        try { worker.Wait(TimeSpan.FromSeconds(2)); } catch { }
        client.Dispose();
        stopping.Dispose();
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string text = value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private sealed class OperationsLogger(
        OperationsLogLoggerProvider provider,
        string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            provider.scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) =>
            provider.IsEnabled(category, logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            provider.Write(category, logLevel, eventId, state, exception, formatter);
        }
    }

    private sealed class ScopeCapture(
        IDictionary<string, string> properties,
        string? correlationId,
        string? operationId)
    {
        public IDictionary<string, string> Properties { get; } = properties;
        public string? CorrelationId = correlationId;
        public string? OperationId = operationId;
    }
}
