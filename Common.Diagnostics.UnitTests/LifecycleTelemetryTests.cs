namespace Common.Diagnostics.UnitTests;

public sealed class LifecycleTelemetryTests
{
    [Fact]
    public void Create_ProducesStructuredEvent()
    {
        DateTimeOffset at = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        LifecycleEvent item = LifecycleEvent.Create(
            "Aegis.Hello", "hello-01", "Registration", "Request",
            LifecycleEventOutcome.Started, "corr-1", operationId: "op-1", occurredAtUtc: at);

        Assert.Equal("Aegis.Hello", item.ApplicationId);
        Assert.Equal("Registration", item.Flow);
        Assert.Equal("Request", item.Stage);
        Assert.Equal("corr-1", item.CorrelationId);
        Assert.Equal(at, item.OccurredAtUtc);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("SecretValue")]
    [InlineData("claim_token")]
    [InlineData("Authorization")]
    [InlineData("connection-string")]
    public void Create_RejectsSensitivePropertyNames(string key)
    {
        Assert.Throws<ArgumentException>(() => LifecycleEvent.Create(
            "app", "instance", "flow", "stage", LifecycleEventOutcome.Started, "corr",
            properties: new Dictionary<string, string> { [key] = "must-not-be-logged" }));
    }

    [Fact]
    public async Task Telemetry_ForwardsToSink()
    {
        RecordingSink sink = new();
        LifecycleTelemetry telemetry = new(sink);
        LifecycleEvent item = LifecycleEvent.Create("app", "instance", "flow", "stage", LifecycleEventOutcome.Succeeded, "corr");
        await telemetry.EmitAsync(item);
        Assert.Same(item, Assert.Single(sink.Items));
    }

    private sealed class RecordingSink : ILifecycleEventSink
    {
        public List<LifecycleEvent> Items { get; } = [];
        public Task EmitAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken = default)
        { Items.Add(lifecycleEvent); return Task.CompletedTask; }
    }
}
