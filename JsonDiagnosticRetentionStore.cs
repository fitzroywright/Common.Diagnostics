namespace Common.Diagnostics;

using System.Text.Json;

public sealed class JsonDiagnosticRetentionStore : IDiagnosticRetentionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string rootPath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonDiagnosticRetentionStore(string? rootPath = null)
    {
        this.rootPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(rootPath)
                ? Path.Combine("data", "common-diagnostics", "telemetry")
                : rootPath);
        Directory.CreateDirectory(this.rootPath);
    }

    public async Task AppendAsync(
        DiagnosticTelemetryEvent telemetryEvent,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(telemetryEvent);
        RetainedDiagnosticTelemetry envelope = new(telemetryEvent, expiresAt);
        string path = Path.Combine(
            rootPath,
            $"{telemetryEvent.Timestamp:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
        string tempPath = path + ".tmp";

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = File.Open(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, false);
        }
        finally
        {
            gate.Release();
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public async Task PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (string path in Directory.EnumerateFiles(rootPath, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    RetainedDiagnosticTelemetry? envelope = await JsonSerializer.DeserializeAsync<RetainedDiagnosticTelemetry>(
                        stream,
                        JsonOptions,
                        cancellationToken).ConfigureAwait(false);

                    if (envelope is not null && envelope.ExpiresAt <= DateTimeOffset.UtcNow)
                    {
                        File.Delete(path);
                    }
                }
                catch (IOException)
                {
                    // A concurrent reader/writer can be retried on the next purge pass.
                }
                catch (JsonException)
                {
                    // Preserve malformed files for operator inspection rather than silently deleting evidence.
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record RetainedDiagnosticTelemetry(
        DiagnosticTelemetryEvent Event,
        DateTimeOffset ExpiresAt);
}
