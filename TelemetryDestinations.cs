namespace Common.Diagnostics;

using System.Net.Http.Json;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

public sealed class GraylogTelemetryOptions
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 12201;
}

public sealed class WazuhTelemetryOptions
{
    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = string.Empty;
}

public sealed class SlackTelemetryOptions
{
    public bool Enabled { get; init; }
    public string WebhookUrl { get; init; } = string.Empty;
}

public sealed class EmailTelemetryOptions
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 25;
    public bool EnableSsl { get; init; }
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
}

public sealed class GraylogDiagnosticTelemetryDestination : IDiagnosticTelemetryDestination
{
    private readonly GraylogTelemetryOptions options;

    public GraylogDiagnosticTelemetryDestination(GraylogTelemetryOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => "Graylog";

    public async Task WriteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return;
        }

        byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            version = "1.1",
            host = Environment.MachineName,
            short_message = telemetryEvent.Message,
            timestamp = telemetryEvent.Timestamp.ToUnixTimeMilliseconds() / 1000d,
            level = ToSyslogLevel(telemetryEvent.Severity),
            _source = telemetryEvent.Source,
            _code = telemetryEvent.Code,
            _correlationId = telemetryEvent.CorrelationId
        }));
        using UdpClient client = new();
        await client.SendAsync(payload, new System.Net.IPEndPoint(
            (await System.Net.Dns.GetHostAddressesAsync(options.Host, cancellationToken).ConfigureAwait(false))[0],
            options.Port), cancellationToken).ConfigureAwait(false);
    }

    private static int ToSyslogLevel(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Critical => 2,
        DiagnosticSeverity.Error => 3,
        DiagnosticSeverity.Warning => 4,
        _ => 6
    };
}

public sealed class WazuhDiagnosticTelemetryDestination : IDiagnosticTelemetryDestination
{
    private readonly WazuhTelemetryOptions options;
    private readonly HttpClient httpClient;

    public WazuhDiagnosticTelemetryDestination(WazuhTelemetryOptions options, HttpClient httpClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string Name => "Wazuh";

    public async Task WriteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return;
        }

        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(options.Endpoint, telemetryEvent, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class SlackDiagnosticTelemetryDestination : IDiagnosticTelemetryDestination
{
    private readonly SlackTelemetryOptions options;
    private readonly HttpClient httpClient;

    public SlackDiagnosticTelemetryDestination(SlackTelemetryOptions options, HttpClient httpClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string Name => "Slack";

    public async Task WriteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.WebhookUrl))
        {
            return;
        }

        string text = $"[{telemetryEvent.Severity}] {telemetryEvent.Source} {telemetryEvent.Code}: {telemetryEvent.Message}";
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(options.WebhookUrl, new { text }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class EmailDiagnosticTelemetryDestination : IDiagnosticTelemetryDestination
{
    private readonly EmailTelemetryOptions options;

    public EmailDiagnosticTelemetryDestination(EmailTelemetryOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => "Email";

    public async Task WriteAsync(DiagnosticTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.From) || string.IsNullOrWhiteSpace(options.To))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using MailMessage message = new(options.From, options.To)
        {
            Subject = $"Engineering Diagnostics {telemetryEvent.Severity}: {telemetryEvent.Code}",
            Body = $"Source: {telemetryEvent.Source}\nCode: {telemetryEvent.Code}\nSeverity: {telemetryEvent.Severity}\nCorrelation: {telemetryEvent.CorrelationId}\n\n{telemetryEvent.Message}"
        };
        using SmtpClient client = new(options.Host, options.Port) { EnableSsl = options.EnableSsl };
        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
