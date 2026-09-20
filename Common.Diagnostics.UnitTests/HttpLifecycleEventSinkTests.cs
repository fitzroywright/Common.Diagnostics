namespace Common.Diagnostics.UnitTests;

public sealed class HttpLifecycleEventSinkTests
{
    [Fact]
    public async Task EmitAsync_PostsStructuredEventAndCorrelationHeader()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
        });
        var client = new HttpClient(handler);
        var sink = new HttpLifecycleEventSink(
            client,
            new HttpLifecycleEventSinkOptions(new Uri("https://operations.example/api/operations/lifecycle/events")),
            (request, item, _) =>
            {
                request.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", item.ApplicationId);
                return Task.CompletedTask;
            });

        LifecycleEvent item = LifecycleEvent.Create(
            "Aegis.Hello", "hello-01", "Registration", "Registered",
            LifecycleEventOutcome.Succeeded, "corr-123");

        await sink.EmitAsync(item);

        Assert.NotNull(captured);
        Assert.Equal("corr-123", captured!.Headers.GetValues("X-Aegis-Correlation-Id").Single());
        Assert.Equal("Aegis.Hello", captured.Headers.GetValues("X-Aegis-Application-Id").Single());
    }

    [Fact]
    public async Task EmitAsync_ThrowsForFailedTransportResponse()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
        var sink = new HttpLifecycleEventSink(
            new HttpClient(handler),
            new HttpLifecycleEventSinkOptions(new Uri("https://operations.example/api/operations/lifecycle/events")));

        LifecycleEvent item = LifecycleEvent.Create(
            "app", "instance", "flow", "stage", LifecycleEventOutcome.Warning, "corr");

        await Assert.ThrowsAsync<HttpRequestException>(() => sink.EmitAsync(item));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
