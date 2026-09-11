using System.Net;
using Common.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Common.Diagnostics.UnitTests;

public sealed class FaultInjectionTests
{
    [Fact]
    public async Task RunnerContainsThrownCheckAndContinuesWithRemainingChecks()
    {
        IDiagnosticCheck[] checks =
        [
            new ThrowingCheck("broken"),
            new FixedCheck("healthy", DiagnosticStatus.Healthy)
        ];
        DiagnosticRunner runner = new(checks, NullLogger<DiagnosticRunner>.Instance);

        IReadOnlyList<DiagnosticResult> results = await runner.RunAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(DiagnosticStatus.Unhealthy, results[0].Status);
        Assert.IsType<InvalidOperationException>(results[0].Exception);
        Assert.Equal(DiagnosticStatus.Healthy, results[1].Status);
    }

    [Fact]
    public async Task RunnerPropagatesCallerCancellationInsteadOfConvertingItToFailure()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        DiagnosticRunner runner = new([new FixedCheck("never-runs", DiagnosticStatus.Healthy)], NullLogger<DiagnosticRunner>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(cancellation.Token));
    }

    [Fact]
    public async Task HttpCheckReportsServerFailureAsUnhealthyWithoutThrowing()
    {
        HttpClient client = new(new FixedResponseHandler(HttpStatusCode.ServiceUnavailable));
        HttpEndpointDiagnosticCheck check = new("dependency", new Uri("https://dependency.invalid/health"), new FixedHttpClientFactory(client));

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Unhealthy, result.Status);
        Assert.Contains("503", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpCheckContainsTransportFailure()
    {
        HttpClient client = new(new ThrowingHandler());
        HttpEndpointDiagnosticCheck check = new("dependency", new Uri("https://dependency.invalid/health"), new FixedHttpClientFactory(client));

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Unhealthy, result.Status);
        Assert.IsType<HttpRequestException>(result.Exception);
    }

    private sealed class ThrowingCheck(string name) : IDiagnosticCheck
    {
        public string Name { get; } = name;
        public Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected diagnostic failure.");
    }

    private sealed class FixedCheck(string name, DiagnosticStatus status) : IDiagnosticCheck
    {
        public string Name { get; } = name;
        public Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default) => Task.FromResult(new DiagnosticResult(Name, status, "Injected result.", TimeSpan.Zero));
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FixedResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Injected transport failure.");
    }
}
