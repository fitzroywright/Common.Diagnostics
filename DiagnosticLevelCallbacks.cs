namespace Common.Diagnostics;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public interface IDiagnosticLevelCompletionNotifier
{
    Task<DiagnosticLevelDeliveryState> NotifyAsync(
        DiagnosticLevelRunRequest request,
        DiagnosticLevelRunRecord run,
        CancellationToken cancellationToken = default);
}

public sealed class HttpDiagnosticLevelCompletionNotifier : IDiagnosticLevelCompletionNotifier, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDiagnosticLevelRequestCredentialProvider credentialProvider;
    private readonly DiagnosticLevelExecutionOptions executionOptions;
    private readonly ILogger<HttpDiagnosticLevelCompletionNotifier>? logger;
    private readonly HttpClient httpClient;

    public HttpDiagnosticLevelCompletionNotifier(
        IDiagnosticLevelRequestCredentialProvider credentialProvider,
        DiagnosticLevelExecutionOptions executionOptions,
        ILogger<HttpDiagnosticLevelCompletionNotifier>? logger = null)
    {
        this.credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        this.executionOptions = executionOptions ?? throw new ArgumentNullException(nameof(executionOptions));
        this.logger = logger;
        httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<DiagnosticLevelDeliveryState> NotifyAsync(
        DiagnosticLevelRunRequest request,
        DiagnosticLevelRunRecord run,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CallbackUrl))
            return DiagnosticLevelDeliveryState.NotApplicable;

        if (!Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out Uri? callbackUri) ||
            (callbackUri.Scheme != Uri.UriSchemeHttps && !callbackUri.IsLoopback))
        {
            logger?.LogWarning(
                "DiagnosticLevel callback URL is invalid or insecure. RunId={RunId} CallbackHost={CallbackHost}",
                run.RunId,
                callbackUri?.Host ?? "invalid");
            return DiagnosticLevelDeliveryState.CallbackFailed;
        }

        string? credential = await credentialProvider
            .GetCredentialAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential))
        {
            logger?.LogWarning(
                "DiagnosticLevel callback credential is unavailable. RunId={RunId}",
                run.RunId);
            return DiagnosticLevelDeliveryState.CallbackFailed;
        }

        var callback = new DiagnosticLevelCompletionCallback(
            run.RunId,
            run.RequestId,
            run.CorrelationId,
            run);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(callback, JsonOptions);
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        string nonce = Guid.NewGuid().ToString("N");
        string signature = DiagnosticLevelRequestSigning.CreateCompletionCallbackSignature(
            credential,
            callbackUri.AbsolutePath,
            timestamp,
            nonce,
            callback);

        using var message = new HttpRequestMessage(HttpMethod.Post, callbackUri)
        {
            Content = new ByteArrayContent(body)
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        message.Headers.TryAddWithoutValidation("X-Aegis-Diagnostics-Timestamp", timestamp);
        message.Headers.TryAddWithoutValidation("X-Aegis-Diagnostics-Nonce", nonce);
        message.Headers.TryAddWithoutValidation("X-Aegis-Diagnostics-Signature", signature);
        message.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", run.Application);
        message.Headers.TryAddWithoutValidation(
            "X-Aegis-Instance-Id",
            string.IsNullOrWhiteSpace(executionOptions.InstanceId)
                ? Environment.MachineName
                : executionOptions.InstanceId);

        try
        {
            using HttpResponseMessage response = await httpClient
                .SendAsync(message, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                logger?.LogInformation(
                    "DiagnosticLevel completion callback delivered. RunId={RunId} RequestId={RequestId} CorrelationId={CorrelationId}",
                    run.RunId,
                    run.RequestId,
                    run.CorrelationId);
                return DiagnosticLevelDeliveryState.Delivered;
            }

            logger?.LogWarning(
                "DiagnosticLevel completion callback failed. RunId={RunId} StatusCode={StatusCode}",
                run.RunId,
                (int)response.StatusCode);
            return DiagnosticLevelDeliveryState.CallbackFailed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger?.LogWarning(
                ex,
                "DiagnosticLevel completion callback could not be delivered. RunId={RunId}",
                run.RunId);
            return DiagnosticLevelDeliveryState.CallbackFailed;
        }
    }

    public void Dispose() => httpClient.Dispose();
}
