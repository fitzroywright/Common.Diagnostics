namespace Common.Diagnostics;

using System.Diagnostics;
using System.Net.Sockets;

public sealed class TcpEndpointDiagnosticCheck : IDiagnosticCheck
{
    private readonly string host;
    private readonly int port;
    private readonly TimeSpan timeout;

    public TcpEndpointDiagnosticCheck(string name, string host, int port, TimeSpan? timeout = null)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Diagnostic name is required.", nameof(name));
        this.host = !string.IsNullOrWhiteSpace(host) ? host : throw new ArgumentException("Host is required.", nameof(host));
        this.port = port is > 0 and <= 65535 ? port : throw new ArgumentOutOfRangeException(nameof(port));
        this.timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public string Name { get; }

    public async Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            using TcpClient client = new();
            await client.ConnectAsync(host, port, timeoutSource.Token);
            stopwatch.Stop();
            return new DiagnosticResult(Name, DiagnosticStatus.Healthy, $"TCP endpoint {host}:{port} is reachable.", stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DiagnosticResult(Name, DiagnosticStatus.Unhealthy, $"TCP endpoint {host}:{port} is unavailable. Check DNS, routing, firewall, service availability, and credentials where applicable.", stopwatch.Elapsed, exception);
        }
    }
}
