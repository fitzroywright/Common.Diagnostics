namespace Common.Diagnostics;

using System.Text.Json;
using Microsoft.Extensions.Hosting;

public sealed class JsonEngineeringDiagnosticRunStore : IEngineeringDiagnosticRunStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path;
    private readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonEngineeringDiagnosticRunStore(IHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, "diagnostics", "diagnostic-runs.json"))
    {
    }

    public JsonEngineeringDiagnosticRunStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A diagnostic run store path is required.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        this.path = fullPath;
    }

    public async Task<IReadOnlyList<EngineeringDiagnosticRun>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            List<EngineeringDiagnosticRun> runs = await ReadAsync(cancellationToken);
            return runs.OrderByDescending(run => run.StartedAt).Take(Math.Clamp(take, 1, 100)).ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<EngineeringDiagnosticRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            List<EngineeringDiagnosticRun> runs = await ReadAsync(cancellationToken);
            return runs.SingleOrDefault(run => run.RunId == runId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(EngineeringDiagnosticRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        await gate.WaitAsync(cancellationToken);
        try
        {
            List<EngineeringDiagnosticRun> runs = await ReadAsync(cancellationToken);
            runs.RemoveAll(existing => existing.RunId == run.RunId);
            runs.Add(run);
            await WriteAsync(runs, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<EngineeringDiagnosticRun?> ResolveAsync(Guid runId, string resolvedBy, string resolution, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resolvedBy))
        {
            throw new ArgumentException("ResolvedBy is required.", nameof(resolvedBy));
        }

        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new ArgumentException("Resolution is required.", nameof(resolution));
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            List<EngineeringDiagnosticRun> runs = await ReadAsync(cancellationToken);
            int index = runs.FindIndex(run => run.RunId == runId);
            if (index < 0)
            {
                return null;
            }

            EngineeringDiagnosticRun resolved = runs[index] with
            {
                ResolvedAt = DateTimeOffset.UtcNow,
                ResolvedBy = resolvedBy.Trim(),
                Resolution = resolution.Trim()
            };
            runs[index] = resolved;
            await WriteAsync(runs, cancellationToken);
            return resolved;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<EngineeringDiagnosticRun>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<EngineeringDiagnosticRun>>(json, serializerOptions) ?? [];
    }

    private async Task WriteAsync(IEnumerable<EngineeringDiagnosticRun> runs, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(runs.OrderByDescending(run => run.StartedAt).Take(500), serializerOptions);
        string temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
        File.Move(temporaryPath, path, true);
    }
}
