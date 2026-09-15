using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Aegis.Engineering.Diagnostics;

public sealed record OperationalTelemetrySnapshot(
    DateTimeOffset ObservedAtUtc,
    TimeSpan ProcessUptime,
    long ProcessWorkingSetBytes,
    long? RuntimeAvailableMemoryBytes,
    double? ProcessCpuPercent,
    IReadOnlyList<OperationalNetworkInterfaceSnapshot> NetworkInterfaces,
    IReadOnlyList<OperationalStorageSnapshot> Storage);

public sealed record OperationalNetworkInterfaceSnapshot(
    string Name,
    string Status);

public sealed record OperationalStorageSnapshot(
    string Name,
    long TotalBytes,
    long AvailableBytes)
{
    public double? PercentFree => TotalBytes > 0 ? AvailableBytes * 100d / TotalBytes : null;
}

/// <summary>
/// Captures live facts observable by the current process. Values are measurements, not declared
/// configuration. The collector deliberately avoids inventing system-wide CPU or RAM figures that
/// .NET cannot prove portably.
/// </summary>
public static class OperationalTelemetryCollector
{
    public static async Task<OperationalTelemetrySnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        using Process process = Process.GetCurrentProcess();
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        TimeSpan firstCpu = process.TotalProcessorTime;
        DateTime firstSample = DateTime.UtcNow;

        try { await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }

        process.Refresh();
        TimeSpan elapsed = DateTime.UtcNow - firstSample;
        TimeSpan secondCpu = process.TotalProcessorTime;
        double? processCpuPercent = elapsed.TotalMilliseconds > 0 && Environment.ProcessorCount > 0
            ? Math.Clamp((secondCpu - firstCpu).TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100d, 0d, 100d)
            : null;

        long runtimeAvailable = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        DateTime startTime;
        try { startTime = process.StartTime.ToUniversalTime(); }
        catch { startTime = DateTime.UtcNow; }

        List<OperationalNetworkInterfaceSnapshot> network = [];
        foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            network.Add(new(item.Name, item.OperationalStatus.ToString()));

        List<OperationalStorageSnapshot> storage = [];
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                storage.Add(new(drive.Name, drive.TotalSize, drive.AvailableFreeSpace));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return new(
            observedAt,
            observedAt - new DateTimeOffset(startTime, TimeSpan.Zero),
            process.WorkingSet64,
            runtimeAvailable > 0 ? runtimeAvailable : null,
            processCpuPercent,
            network,
            storage);
    }
}
