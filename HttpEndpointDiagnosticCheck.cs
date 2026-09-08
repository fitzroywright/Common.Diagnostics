namespace Common.Diagnostics;

using System.Diagnostics;

public sealed class HttpEndpointDiagnosticCheck : IDiagnosticCheck
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly Uri endpoint;

    public HttpEndpointDiagnosticCheck(string name, Uri endpoint, IHttpClientFactory httpClientFactory)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Diagnostic name is required.", nameof(name));
        this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public string Name { get; }

    public async Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpClient client = httpClientFactory.CreateClient();

        try
        {
            using HttpResponseMessage response = await client.GetAsync(endpoint, cancellationToken);
            stopwatch.Stop();

            DiagnosticStatus status = response.IsSuccessStatusCode
                ? DiagnosticStatus.Healthy
                : DiagnosticStatus.Unhealthy;

            return new DiagnosticResult(
                Name,
                status,
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DiagnosticResult(Name, DiagnosticStatus.Unhealthy, exception.Message, stopwatch.Elapsed, exception);
        }
    }
}
