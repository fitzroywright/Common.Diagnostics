namespace Common.Diagnostics;

using System.Diagnostics;
using System.Net.Sockets;

public sealed class PostgreSqlDiagnosticCheck : IDiagnosticCheck
{
    private readonly Func<CancellationToken, Task> probe;
    private readonly string endpointDescription;

    public PostgreSqlDiagnosticCheck(string name, string endpointDescription, Func<CancellationToken, Task> probe)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : "PostgreSQL";
        this.endpointDescription = !string.IsNullOrWhiteSpace(endpointDescription) ? endpointDescription : "configured PostgreSQL database";
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public string Name { get; }

    public Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
        => DiagnosticProbe.RunAsync(Name, $"PostgreSQL query succeeded against {endpointDescription}.", "PostgreSQL query failed. Check DNS, TCP/5432, credentials, database existence, SSL policy, and server availability.", probe, cancellationToken);
}

public sealed class SecretResolutionDiagnosticCheck : IDiagnosticCheck
{
    private readonly string providerDescription;
    private readonly IReadOnlyList<string> requiredSecretNames;
    private readonly Func<string, CancellationToken, Task<string?>> resolver;

    public SecretResolutionDiagnosticCheck(
        string name,
        string providerDescription,
        IEnumerable<string> requiredSecretNames,
        Func<string, CancellationToken, Task<string?>> resolver)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : "OpenBao / Common.Secrets";
        this.providerDescription = !string.IsNullOrWhiteSpace(providerDescription) ? providerDescription : "configured secret provider chain";
        this.requiredSecretNames = requiredSecretNames?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            ?? throw new ArgumentNullException(nameof(requiredSecretNames));
        this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string Name { get; }

    public async Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            List<string> missing = [];
            foreach (string secretName in requiredSecretNames)
            {
                string? value = await resolver(secretName, cancellationToken);
                if (string.IsNullOrWhiteSpace(value)) missing.Add(secretName);
            }
            stopwatch.Stop();
            return missing.Count == 0
                ? new DiagnosticResult(Name, DiagnosticStatus.Healthy, $"{providerDescription} resolved {requiredSecretNames.Count} required secret(s) without exposing values.", stopwatch.Elapsed)
                : new DiagnosticResult(Name, DiagnosticStatus.Unhealthy, $"{providerDescription} could not resolve: {string.Join(", ", missing)}.", stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DiagnosticResult(Name, DiagnosticStatus.Unhealthy, $"{providerDescription} failed. Check provider availability, authentication, policy, TLS, and configured secret paths.", stopwatch.Elapsed, exception);
        }
    }
}

public sealed class StorageTargetDiagnosticCheck : IDiagnosticCheck
{
    private readonly string targetDescription;
    private readonly Func<CancellationToken, Task> probe;

    public StorageTargetDiagnosticCheck(string name, string targetDescription, Func<CancellationToken, Task> probe)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : "Storage target";
        this.targetDescription = !string.IsNullOrWhiteSpace(targetDescription) ? targetDescription : "configured storage target";
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public string Name { get; }

    public Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
        => DiagnosticProbe.RunAsync(Name, $"Storage target {targetDescription} passed its configured probe.", $"Storage target {targetDescription} failed. Check path/bucket, network, credentials, permissions, capacity, and provider availability.", probe, cancellationToken);
}

public sealed class MessagingDiagnosticCheck : IDiagnosticCheck
{
    private readonly string transportDescription;
    private readonly Func<CancellationToken, Task> probe;

    public MessagingDiagnosticCheck(string name, string transportDescription, Func<CancellationToken, Task> probe)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : "Messaging";
        this.transportDescription = !string.IsNullOrWhiteSpace(transportDescription) ? transportDescription : "configured messaging transport";
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public string Name { get; }

    public Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
        => DiagnosticProbe.RunAsync(Name, $"Messaging transport {transportDescription} passed its configured probe.", $"Messaging transport {transportDescription} failed. Check DNS, routing, credentials, scopes, TLS, endpoint availability, and provider configuration.", probe, cancellationToken);
}

public sealed class ActiveDirectoryEndpointDiagnosticCheck : IDiagnosticCheck
{
    private readonly IReadOnlyList<string> servers;
    private readonly int port;
    private readonly TimeSpan timeout;

    public ActiveDirectoryEndpointDiagnosticCheck(string name, IEnumerable<string> servers, int port = 636, TimeSpan? timeout = null)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : "Active Directory";
        this.servers = servers?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            ?? throw new ArgumentNullException(nameof(servers));
        this.port = port is > 0 and <= 65535 ? port : throw new ArgumentOutOfRangeException(nameof(port));
        this.timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public string Name { get; }

    public async Task<DiagnosticResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (servers.Count == 0)
        {
            stopwatch.Stop();
            return new DiagnosticResult(Name, DiagnosticStatus.Warning, "No Active Directory diagnostic servers are configured.", stopwatch.Elapsed);
        }

        foreach (string server in servers)
        {
            try
            {
                using TcpClient client = new();
                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(timeout);
                await client.ConnectAsync(server, port, timeoutSource.Token);
                stopwatch.Stop();
                return new DiagnosticResult(Name, DiagnosticStatus.Healthy, $"Active Directory endpoint {server}:{port} is reachable.", stopwatch.Elapsed);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Try the next configured domain controller.
            }
        }

        stopwatch.Stop();
        return new DiagnosticResult(Name, DiagnosticStatus.Unhealthy, $"No configured Active Directory endpoint is reachable on TCP/{port}. Check DNS, routing, firewall, LDAPS/LDAP service, and domain controller health.", stopwatch.Elapsed);
    }
}

internal static class DiagnosticProbe
{
    public static async Task<DiagnosticResult> RunAsync(
        string name,
        string successMessage,
        string failureMessage,
        Func<CancellationToken, Task> probe,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            await probe(cancellationToken);
            stopwatch.Stop();
            return new DiagnosticResult(name, DiagnosticStatus.Healthy, successMessage, stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new DiagnosticResult(name, DiagnosticStatus.Unhealthy, failureMessage, stopwatch.Elapsed, exception);
        }
    }
}
