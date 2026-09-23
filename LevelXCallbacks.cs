namespace Common.Diagnostics;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public interface ILevelXCompletionNotifier
{
    Task<LevelXDeliveryState> NotifyAsync(
        LevelXRunRequest request,
        LevelXRunRecord run,
        CancellationToken cancellationToken = default);
}

public sealed class HttpLevelXCompletionNotifier : ILevelXCompletionNotifier, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILevelXRequestCredentialProvider credentialProvider;
    private readonly LevelXExecutionOptions executionOptions;
    private readonly ILogger<HttpLevelXCompletionNotifier>? logger;
    private readonly HttpClient httpClient;

    public HttpLevelXCompletionNotifier(
        ILevelXRequestCredentialProvider credentialProvider,
        LevelXExecutionOptions executionOptions,
        ILogger<HttpLevelXCompletionNotifier>? logger = null)
    {
        this.credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        this.executionOptions = executionOptions ?? throw new ArgumentNullException(nameof(executionOptions));
        this.logger = logger;
        httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<LevelXDeliveryState> NotifyAsync(
        LevelXRunRequest request,
        LevelXRunRecord run,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CallbackUrl))
            return LevelXDeliveryState.NotApplicable;

        if (!Uri.TryCreate(request.CallbackUrl, UriKind.Absolute, out Uri? callbackUri) ||
            (callbackUri.Scheme != Uri.UriSchemeHttps && !callbackUri.IsLoopback))
        {
            logger?.LogWarning(
                "LevelX callback URL is invalid or insecure. RunId={RunId} CallbackHost={CallbackHost}",
                run.RunId,
                callbackUri?.Host ?? "invalid");
            return LevelXDeliveryState.CallbackFailed;
        }

        string? credential = await credentialProvider
            .GetCredentialAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential))
        {
            logger?.LogWarning(
                "LevelX callback credential is unavailable. RunId={RunId}",
                run.RunId);
            return LevelXDeliveryState.CallbackFailed;
        }

        var callback = new LevelXCompletionCallback(
            run.RunId,
            run.RequestId,
            run.CorrelationId,
            run);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(callback, JsonOptions);
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        string nonce = Guid.NewGuid().ToString("N");
        string signature = LevelXRequestSigning.CreateCompletionCallbackSignature(
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
                    "LevelX completion callback delivered. RunId={RunId} RequestId={RequestId} CorrelationId={CorrelationId}",
                    run.RunId,
                    run.RequestId,
                    run.CorrelationId);
                return LevelXDeliveryState.Delivered;
            }

            logger?.LogWarning(
                "LevelX completion callback failed. RunId={RunId} StatusCode={StatusCode}",
                run.RunId,
                (int)response.StatusCode);
            return LevelXDeliveryState.CallbackFailed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger?.LogWarning(
                ex,
                "LevelX completion callback could not be delivered. RunId={RunId}",
                run.RunId);
            return LevelXDeliveryState.CallbackFailed;
        }
    }

    public void Dispose() => httpClient.Dispose();
}
