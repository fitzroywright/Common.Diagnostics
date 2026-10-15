namespace Common.Diagnostics;

/// <summary>
/// Identifies this running process, independently of its durable installation identity.
/// Shared by registration, contract publication and telemetry; never persisted to disk.
/// </summary>
public static class ApplicationRuntimeSession
{
    public const string HeaderName = "X-Aegis-Runtime-Session-Id";
    public static string Id { get; } = Guid.NewGuid().ToString("N");

    public static void Apply(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(HeaderName, Id);
    }
}
