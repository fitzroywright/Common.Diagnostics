using System.Net.Http.Json;

namespace Common.Diagnostics;

public sealed record HttpLifecycleEventSinkOptions(
    Uri Endpoint,
    TimeSpan? Timeout = null);

public sealed class HttpLifecycleEventSink : ILifecycleEventSink
{
    private readonly HttpClient client;
    private readonly HttpLifecycleEventSinkOptions options;
    private readonly Func<HttpRequestMessage, LifecycleEvent, CancellationToken, Task>? authorize;

    public HttpLifecycleEventSink(
        HttpClient client,
        HttpLifecycleEventSinkOptions options,
        Func<HttpRequestMessage, LifecycleEvent, CancellationToken, Task>? authorize = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.authorize = authorize;

        if (!options.Endpoint.IsAbsoluteUri)
            throw new ArgumentException("Lifecycle telemetry endpoint must be absolute.", nameof(options));

        if (options.Timeout is { } timeout)
            this.client.Timeout = timeout;
    }

    public async Task EmitAsync(
        LifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        using HttpRequestMessage request = new(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(lifecycleEvent)
        };

        request.Headers.TryAddWithoutValidation("X-Aegis-Correlation-Id", lifecycleEvent.CorrelationId);

        if (authorize is not null)
            await authorize(request, lifecycleEvent, cancellationToken).ConfigureAwait(false);

        using HttpResponseMessage response = await client
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
