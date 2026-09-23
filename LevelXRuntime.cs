namespace Common.Diagnostics;

using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

public enum LevelXExecutionState
{
    Idle = 0,
    Requested = 1,
    Accepted = 2,
    Running = 3,
    Completed = 4,
    Cancelled = 5,
    Interrupted = 6,
    NotStarted = 7,
    Unknown = 8
}

public enum LevelXDeliveryState
{
    NotApplicable = 0,
    NotAttempted = 1,
    Accepted = 2,
    CallbackPending = 3,
    Delivered = 4,
    CallbackFailed = 5,
    NotReachable = 6,
    Rejected = 7
}

public sealed record LevelXComponentVersion(
    string Application,
    string Component,
    string Version,
    string? Build = null,
    string? Commit = null,
    string? Runtime = null,
    string? CatalogVersion = null)
{
    public static LevelXComponentVersion Capture(
        string application,
        string component,
        string? catalogVersion = null,
        Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        AssemblyName name = assembly.GetName();
        string version =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? name.Version?.ToString()
            ?? "Unknown";
        string? build = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        string? commit = TryExtractCommit(version);

        return new(
            application,
            component,
            string.IsNullOrWhiteSpace(version) ? "Unknown" : version,
            string.IsNullOrWhiteSpace(build) ? null : build,
            commit,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            string.IsNullOrWhiteSpace(catalogVersion) ? "1" : catalogVersion);
    }

    private static string? TryExtractCommit(string informationalVersion)
    {
        int plus = informationalVersion.IndexOf('+');
        if (plus < 0 || plus == informationalVersion.Length - 1) return null;
        string candidate = informationalVersion[(plus + 1)..].Trim();
        return candidate.Length >= 7 ? candidate : null;
    }
}

public sealed record LevelXTestResult(
    string TestId,
    string Name,
    string Owner,
    EngineeringDiagnosticLevel Level,
    EngineeringDiagnosticStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Summary,
    string? Evidence = null,
    string? Code = null)
{
    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;
}

public sealed record LevelXRunRecord(
    Guid RunId,
    Guid RequestId,
    Guid CorrelationId,
    string Application,
    string Component,
    string Host,
    EngineeringDiagnosticLevel Level,
    LevelXExecutionState ExecutionState,
    LevelXDeliveryState DeliveryState,
    string RequestedBy,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    LevelXComponentVersion Version,
    IReadOnlyList<LevelXTestResult> Tests,
    string? IntegrityHash = null,
    string? CurrentTestId = null,
    string? Failure = null,
    DateTimeOffset? LastProgressAtUtc = null)
{
    public int CompletedTests => Tests.Count;
    public int FailedTests => Tests.Count(x => x.Status == EngineeringDiagnosticStatus.Failed);
    public int WarningTests => Tests.Count(x => x.Status is EngineeringDiagnosticStatus.Warning or EngineeringDiagnosticStatus.InterventionRequired);
}

public sealed record LevelXRunRequest(
    Guid RequestId,
    Guid CorrelationId,
    EngineeringDiagnosticLevel Level,
    string RequestedBy,
    string? Reason,
    string? CallbackUrl,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record LevelXRunAccepted(
    Guid RunId,
    Guid RequestId,
    Guid CorrelationId,
    LevelXExecutionState State,
    DateTimeOffset AcceptedAtUtc,
    EngineeringDiagnosticLevel Level,
    string Application,
    string Component);

public sealed record LevelXCompletionCallback(
    Guid RunId,
    Guid RequestId,
    Guid CorrelationId,
    LevelXRunRecord Run);

public sealed record LevelXHistoryQuery(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Application = null,
    string? Component = null,
    string? Host = null,
    EngineeringDiagnosticLevel? Level = null,
    LevelXExecutionState? State = null,
    string? TestId = null,
    Guid? CorrelationId = null,
    string? RequestedBy = null,
    string? Version = null,
    int Take = 100);

public sealed class LevelXStoreOptions
{
    public string Path { get; set; } = System.IO.Path.Combine("data", "common-diagnostics", "levelx.db");
    public int RetainPassedDays { get; set; } = 90;
    public int RetainNonPassedDays { get; set; } = 365;
}

public interface ILevelXRunStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(LevelXRunRecord run, CancellationToken cancellationToken = default);
    Task<LevelXRunRecord?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LevelXRunRecord>> QueryAsync(LevelXHistoryQuery query, CancellationToken cancellationToken = default);
    Task PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

public sealed class SqliteLevelXRunStore : ILevelXRunStore
{
    private readonly LevelXStoreOptions options;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SqliteLevelXRunStore(LevelXStoreOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string fullPath = System.IO.Path.GetFullPath(options.Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath) ?? ".");
            await using SqliteConnection connection = Open(fullPath);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS levelx_runs (
                    run_id TEXT PRIMARY KEY,
                    request_id TEXT NOT NULL,
                    correlation_id TEXT NOT NULL,
                    application TEXT NOT NULL,
                    component TEXT NOT NULL,
                    host TEXT NOT NULL,
                    level INTEGER NOT NULL,
                    execution_state INTEGER NOT NULL,
                    delivery_state INTEGER NOT NULL,
                    requested_by TEXT NOT NULL,
                    requested_at_utc TEXT NOT NULL,
                    accepted_at_utc TEXT NULL,
                    started_at_utc TEXT NULL,
                    completed_at_utc TEXT NULL,
                    version TEXT NULL,
                    integrity_hash TEXT NULL,
                    current_test_id TEXT NULL,
                    payload_json TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_levelx_runs_requested_at ON levelx_runs(requested_at_utc DESC);
                CREATE INDEX IF NOT EXISTS ix_levelx_runs_application_component ON levelx_runs(application, component);
                CREATE INDEX IF NOT EXISTS ix_levelx_runs_correlation ON levelx_runs(correlation_id);
                CREATE INDEX IF NOT EXISTS ix_levelx_runs_state ON levelx_runs(execution_state);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(LevelXRunRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        string fullPath = System.IO.Path.GetFullPath(options.Path);
        string payload = JsonSerializer.Serialize(run, JsonOptions);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = Open(fullPath);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO levelx_runs (
                    run_id, request_id, correlation_id, application, component, host, level,
                    execution_state, delivery_state, requested_by, requested_at_utc, accepted_at_utc,
                    started_at_utc, completed_at_utc, version, integrity_hash, current_test_id, payload_json)
                VALUES (
                    $run_id, $request_id, $correlation_id, $application, $component, $host, $level,
                    $execution_state, $delivery_state, $requested_by, $requested_at_utc, $accepted_at_utc,
                    $started_at_utc, $completed_at_utc, $version, $integrity_hash, $current_test_id, $payload_json)
                ON CONFLICT(run_id) DO UPDATE SET
                    request_id=excluded.request_id,
                    correlation_id=excluded.correlation_id,
                    application=excluded.application,
                    component=excluded.component,
                    host=excluded.host,
                    level=excluded.level,
                    execution_state=excluded.execution_state,
                    delivery_state=excluded.delivery_state,
                    requested_by=excluded.requested_by,
                    requested_at_utc=excluded.requested_at_utc,
                    accepted_at_utc=excluded.accepted_at_utc,
                    started_at_utc=excluded.started_at_utc,
                    completed_at_utc=excluded.completed_at_utc,
                    version=excluded.version,
                    integrity_hash=excluded.integrity_hash,
                    current_test_id=excluded.current_test_id,
                    payload_json=excluded.payload_json;
                """;
            Add(command, "$run_id", run.RunId.ToString("D"));
            Add(command, "$request_id", run.RequestId.ToString("D"));
            Add(command, "$correlation_id", run.CorrelationId.ToString("D"));
            Add(command, "$application", run.Application);
            Add(command, "$component", run.Component);
            Add(command, "$host", run.Host);
            Add(command, "$level", (int)run.Level);
            Add(command, "$execution_state", (int)run.ExecutionState);
            Add(command, "$delivery_state", (int)run.DeliveryState);
            Add(command, "$requested_by", run.RequestedBy);
            Add(command, "$requested_at_utc", run.RequestedAtUtc.ToString("O"));
            Add(command, "$accepted_at_utc", run.AcceptedAtUtc?.ToString("O"));
            Add(command, "$started_at_utc", run.StartedAtUtc?.ToString("O"));
            Add(command, "$completed_at_utc", run.CompletedAtUtc?.ToString("O"));
            Add(command, "$version", run.Version.Version);
            Add(command, "$integrity_hash", run.IntegrityHash);
            Add(command, "$current_test_id", run.CurrentTestId);
            Add(command, "$payload_json", payload);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LevelXRunRecord?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        string fullPath = System.IO.Path.GetFullPath(options.Path);
        await using SqliteConnection connection = Open(fullPath);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM levelx_runs WHERE run_id=$run_id LIMIT 1;";
        Add(command, "$run_id", runId.ToString("D"));
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is string json ? JsonSerializer.Deserialize<LevelXRunRecord>(json, JsonOptions) : null;
    }

    public async Task<IReadOnlyList<LevelXRunRecord>> QueryAsync(LevelXHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        string fullPath = System.IO.Path.GetFullPath(options.Path);

        List<string> filters = [];
        await using SqliteConnection connection = Open(fullPath);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();

        void Filter(string sql, string name, object value)
        {
            filters.Add(sql);
            Add(command, name, value);
        }

        if (query.FromUtc is { } from) Filter("requested_at_utc >= $from", "$from", from.ToString("O"));
        if (query.ToUtc is { } to) Filter("requested_at_utc <= $to", "$to", to.ToString("O"));
        if (!string.IsNullOrWhiteSpace(query.Application)) Filter("application = $application", "$application", query.Application.Trim());
        if (!string.IsNullOrWhiteSpace(query.Component)) Filter("component = $component", "$component", query.Component.Trim());
        if (!string.IsNullOrWhiteSpace(query.Host)) Filter("host = $host", "$host", query.Host.Trim());
        if (query.Level is { } level) Filter("level = $level", "$level", (int)level);
        if (query.State is { } state) Filter("execution_state = $state", "$state", (int)state);
        if (query.CorrelationId is { } correlation) Filter("correlation_id = $correlation", "$correlation", correlation.ToString("D"));
        if (!string.IsNullOrWhiteSpace(query.RequestedBy)) Filter("requested_by = $requested_by", "$requested_by", query.RequestedBy.Trim());
        if (!string.IsNullOrWhiteSpace(query.Version)) Filter("version = $version", "$version", query.Version.Trim());

        string where = filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
        command.CommandText = $"SELECT payload_json FROM levelx_runs{where} ORDER BY requested_at_utc DESC LIMIT $take;";
        Add(command, "$take", Math.Clamp(query.Take, 1, 1000));

        List<LevelXRunRecord> runs = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            LevelXRunRecord? run = JsonSerializer.Deserialize<LevelXRunRecord>(reader.GetString(0), JsonOptions);
            if (run is null) continue;
            if (!string.IsNullOrWhiteSpace(query.TestId) &&
                !run.Tests.Any(x => string.Equals(x.TestId, query.TestId, StringComparison.OrdinalIgnoreCase)))
                continue;
            runs.Add(run);
        }

        return runs;
    }

    public async Task PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LevelXRunRecord> runs = await QueryAsync(new LevelXHistoryQuery(Take: 1000), cancellationToken).ConfigureAwait(false);
        foreach (LevelXRunRecord run in runs)
        {
            bool passed = run.ExecutionState == LevelXExecutionState.Completed && run.FailedTests == 0 && run.WarningTests == 0;
            int days = passed ? options.RetainPassedDays : options.RetainNonPassedDays;
            if (run.RequestedAtUtc.AddDays(Math.Max(1, days)) >= nowUtc) continue;

            string fullPath = System.IO.Path.GetFullPath(options.Path);
            await using SqliteConnection connection = Open(fullPath);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM levelx_runs WHERE run_id=$run_id;";
            Add(command, "$run_id", run.RunId.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static SqliteConnection Open(string path)
        => new(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared, Pooling = false }.ToString());

    private static void Add(SqliteCommand command, string name, object? value)
        => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
}

public interface ILevelXLocalTest
{
    string TestId { get; }
    string Name { get; }
    string Owner { get; }
    EngineeringDiagnosticLevel Level { get; }
    bool IsDestructive { get; }
    Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken);
}

public sealed class DelegateLevelXLocalTest : ILevelXLocalTest
{
    private readonly Func<CancellationToken, Task<EngineeringDiagnosticCheckResult>> run;

    public DelegateLevelXLocalTest(
        string testId,
        string name,
        string owner,
        EngineeringDiagnosticLevel level,
        bool isDestructive,
        Func<CancellationToken, Task<EngineeringDiagnosticCheckResult>> run)
    {
        TestId = string.IsNullOrWhiteSpace(testId) ? throw new ArgumentException("TestId is required.", nameof(testId)) : testId.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name.Trim();
        Owner = string.IsNullOrWhiteSpace(owner) ? throw new ArgumentException("Owner is required.", nameof(owner)) : owner.Trim();
        Level = level;
        IsDestructive = isDestructive;
        this.run = run ?? throw new ArgumentNullException(nameof(run));
        if (isDestructive && level is EngineeringDiagnosticLevel.Level5Scan or EngineeringDiagnosticLevel.Level4Analysis)
            throw new ArgumentException("Level 5 and Level 4 tests must be non-destructive.", nameof(isDestructive));
    }

    public string TestId { get; }
    public string Name { get; }
    public string Owner { get; }
    public EngineeringDiagnosticLevel Level { get; }
    public bool IsDestructive { get; }

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken) => run(cancellationToken);
}

public sealed class LevelXEndpointSecurityOptions
{
    public string? SharedSecret { get; set; }
    public TimeSpan MaxClockSkew { get; set; } = TimeSpan.FromMinutes(2);
    public bool RequireSignedRequests { get; set; } = true;
}

public interface ILevelXRequestCredentialProvider
{
    ValueTask<string?> GetCredentialAsync(
        Microsoft.AspNetCore.Http.HttpContext context,
        CancellationToken cancellationToken = default);
}

public sealed class StaticLevelXRequestCredentialProvider(LevelXEndpointSecurityOptions options)
    : ILevelXRequestCredentialProvider
{
    private readonly LevelXEndpointSecurityOptions options =
        options ?? throw new ArgumentNullException(nameof(options));

    public ValueTask<string?> GetCredentialAsync(
        Microsoft.AspNetCore.Http.HttpContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(
            string.IsNullOrWhiteSpace(options.SharedSecret)
                ? null
                : options.SharedSecret.Trim());
}

public sealed class LevelXExecutionOptions
{
    public string Application { get; set; } = "Unknown";
    public string Component { get; set; } = "Unknown";
    public string CatalogVersion { get; set; } = "1";
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(5);
}

public static class LevelXRuntimeEndpoints
{
    public static void MapLevelXRuntime(
        this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints,
        string routePrefix = "/api/engineering/diagnostics/levelx")
    {
        endpoints.MapPost(routePrefix + "/run", async (
            LevelXRunRequest request,
            Microsoft.AspNetCore.Http.HttpContext context,
            LevelXExecutionService service,
            LevelXEndpointSecurityOptions security,
            ILevelXRequestCredentialProvider credentialProvider,
            LevelXNonceCache nonceCache,
            CancellationToken ct) =>
        {
            if (security.RequireSignedRequests)
            {
                string? credential = await credentialProvider
                    .GetCredentialAsync(context, ct)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(credential))
                    return Microsoft.AspNetCore.Http.Results.Problem(
                        "LevelX signed-request authentication is required but no per-install credential is available.",
                        statusCode: Microsoft.AspNetCore.Http.StatusCodes.Status503ServiceUnavailable);

                string timestamp = context.Request.Headers["X-Aegis-Diagnostics-Timestamp"].ToString();
                string nonce = context.Request.Headers["X-Aegis-Diagnostics-Nonce"].ToString();
                string signature = context.Request.Headers["X-Aegis-Diagnostics-Signature"].ToString();

                if (!DateTimeOffset.TryParse(timestamp, out DateTimeOffset issuedAt) ||
                    DateTimeOffset.UtcNow - issuedAt > security.MaxClockSkew ||
                    issuedAt - DateTimeOffset.UtcNow > security.MaxClockSkew)
                    return Microsoft.AspNetCore.Http.Results.Unauthorized();

                if (!nonceCache.TryUse(nonce, DateTimeOffset.UtcNow))
                    return Microsoft.AspNetCore.Http.Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(signature) ||
                    !LevelXRequestSigning.VerifyRunRequest(
                        credential,
                        context.Request.Path,
                        timestamp,
                        nonce,
                        request,
                        signature))
                    return Microsoft.AspNetCore.Http.Results.Unauthorized();
            }

            try
            {
                LevelXRunAccepted accepted = await service.AcceptAsync(request, ct).ConfigureAwait(false);
                return Microsoft.AspNetCore.Http.Results.Accepted(value: accepted);
            }
            catch (InvalidOperationException ex)
            {
                return Microsoft.AspNetCore.Http.Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Microsoft.AspNetCore.Http.Results.BadRequest(new { error = ex.Message });
            }
        });

        endpoints.MapGet(routePrefix + "/runs/{runId:guid}", async (
            Guid runId,
            ILevelXRunStore store,
            CancellationToken ct) =>
        {
            LevelXRunRecord? run = await store.GetAsync(runId, ct).ConfigureAwait(false);
            return run is null
                ? Microsoft.AspNetCore.Http.Results.NotFound()
                : Microsoft.AspNetCore.Http.Results.Ok(run);
        });

        endpoints.MapGet(routePrefix + "/history", async (
            ILevelXRunStore store,
            CancellationToken ct) =>
            Microsoft.AspNetCore.Http.Results.Ok(
                await store.QueryAsync(new LevelXHistoryQuery(), ct).ConfigureAwait(false)));

        endpoints.MapPost(routePrefix + "/runs/{runId:guid}/cancel", async (
            Guid runId,
            LevelXExecutionService service,
            CancellationToken ct) =>
            await service.CancelAsync(runId, ct).ConfigureAwait(false)
                ? Microsoft.AspNetCore.Http.Results.Accepted()
                : Microsoft.AspNetCore.Http.Results.NotFound());
    }
}

public sealed class LevelXExecutionService
{
    private static readonly JsonSerializerOptions IntegrityJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<ILevelXLocalTest> tests;
    private readonly ILevelXRunStore store;
    private readonly LevelXExecutionOptions options;
    private readonly ILogger<LevelXExecutionService>? logger;
    private readonly ConcurrentDictionary<string, ActiveRun> active = new(StringComparer.OrdinalIgnoreCase);

    public LevelXExecutionService(
        IEnumerable<ILevelXLocalTest> tests,
        ILevelXRunStore store,
        LevelXExecutionOptions options,
        ILogger<LevelXExecutionService>? logger = null)
    {
        this.tests = tests?.ToArray() ?? throw new ArgumentNullException(nameof(tests));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger;
        ValidateCatalogue(this.tests);
    }

    public bool TryGetActive(string component, out Guid runId)
    {
        if (active.TryGetValue(component, out ActiveRun? value))
        {
            runId = value.RunId;
            return true;
        }

        runId = Guid.Empty;
        return false;
    }

    public async Task<LevelXRunAccepted> AcceptAsync(LevelXRunRequest request, CancellationToken cancellationToken = default)
    {
        EngineeringDiagnosticPolicy.ValidateLevel(request.Level);
        if (request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Diagnostic request has expired.", nameof(request));

        string key = $"{options.Application}/{options.Component}";
        Guid runId = Guid.NewGuid();
        CancellationTokenSource runCts = new();
        ActiveRun activeRun = new(runId, runCts);

        if (!active.TryAdd(key, activeRun))
            throw new InvalidOperationException($"A LevelX diagnostic run is already active for {key}.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        LevelXRunRecord accepted = NewRun(runId, request, now) with
        {
            ExecutionState = LevelXExecutionState.Accepted,
            DeliveryState = string.IsNullOrWhiteSpace(request.CallbackUrl)
                ? LevelXDeliveryState.NotApplicable
                : LevelXDeliveryState.CallbackPending,
            AcceptedAtUtc = now,
            LastProgressAtUtc = now
        };
        await store.SaveAsync(accepted, cancellationToken).ConfigureAwait(false);

        _ = ExecuteAcceptedAsync(key, request, accepted, runCts.Token);
        return new(runId, request.RequestId, request.CorrelationId, LevelXExecutionState.Accepted, now, request.Level, options.Application, options.Component);
    }

    public async Task<bool> CancelAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        KeyValuePair<string, ActiveRun>? found = active.FirstOrDefault(x => x.Value.RunId == runId);
        if (found is null || found.Value.Value is null) return false;
        found.Value.Value.Cancellation.Cancel();
        await Task.CompletedTask;
        return true;
    }

    private async Task ExecuteAcceptedAsync(string key, LevelXRunRequest request, LevelXRunRecord accepted, CancellationToken cancellationToken)
    {
        List<LevelXTestResult> results = [];
        LevelXRunRecord current = accepted;
        try
        {
            DateTimeOffset started = DateTimeOffset.UtcNow;
            current = current with
            {
                ExecutionState = LevelXExecutionState.Running,
                StartedAtUtc = started,
                LastProgressAtUtc = started
            };
            await store.SaveAsync(current, CancellationToken.None).ConfigureAwait(false);

            ILevelXLocalTest[] selected = tests
                .Where(x => (int)x.Level >= (int)request.Level)
                .OrderByDescending(x => (int)x.Level)
                .ThenBy(x => x.TestId, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (ILevelXLocalTest test in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (test.IsDestructive && test.Level is EngineeringDiagnosticLevel.Level5Scan or EngineeringDiagnosticLevel.Level4Analysis)
                    throw new InvalidOperationException($"Test {test.TestId} violates the non-destructive Level 5/4 rule.");

                DateTimeOffset testStarted = DateTimeOffset.UtcNow;
                current = current with { CurrentTestId = test.TestId, LastProgressAtUtc = testStarted, Tests = results.ToArray() };
                await store.SaveAsync(current, CancellationToken.None).ConfigureAwait(false);

                EngineeringDiagnosticCheckResult raw;
                try
                {
                    raw = await test.RunAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    raw = EngineeringDiagnosticPolicy.Failed(test.TestId, test.Name, ex.Message, ex.GetType().FullName);
                }

                DateTimeOffset testCompleted = DateTimeOffset.UtcNow;
                LevelXTestResult result = new(
                    test.TestId,
                    test.Name,
                    test.Owner,
                    test.Level,
                    raw.Status,
                    testStarted,
                    testCompleted,
                    raw.Summary,
                    raw.Evidence,
                    raw.Code);
                results.Add(result);

                logger?.LogInformation(
                    "LevelX test completed. RunId={RunId} RequestId={RequestId} CorrelationId={CorrelationId} Application={Application} Component={Component} Level={Level} TestId={TestId} TestName={TestName} Status={Status} DurationMs={DurationMs}",
                    current.RunId,
                    current.RequestId,
                    current.CorrelationId,
                    current.Application,
                    current.Component,
                    (int)request.Level,
                    result.TestId,
                    result.Name,
                    result.Status,
                    result.Duration.TotalMilliseconds);
            }

            DateTimeOffset completed = DateTimeOffset.UtcNow;
            current = current with
            {
                ExecutionState = LevelXExecutionState.Completed,
                CompletedAtUtc = completed,
                LastProgressAtUtc = completed,
                CurrentTestId = null,
                Tests = results.ToArray()
            };
            current = current with { IntegrityHash = ComputeIntegrityHash(current) };
            await store.SaveAsync(current, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            current = current with
            {
                ExecutionState = LevelXExecutionState.Cancelled,
                CompletedAtUtc = completed,
                LastProgressAtUtc = completed,
                CurrentTestId = null,
                Tests = results.ToArray(),
                Failure = "Cancelled at a safe test boundary."
            };
            current = current with { IntegrityHash = ComputeIntegrityHash(current) };
            await store.SaveAsync(current, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            current = current with
            {
                ExecutionState = LevelXExecutionState.Interrupted,
                CompletedAtUtc = completed,
                LastProgressAtUtc = completed,
                CurrentTestId = null,
                Tests = results.ToArray(),
                Failure = ex.Message
            };
            current = current with { IntegrityHash = ComputeIntegrityHash(current) };
            await store.SaveAsync(current, CancellationToken.None).ConfigureAwait(false);
            logger?.LogError(ex, "LevelX run interrupted. RunId={RunId}", current.RunId);
        }
        finally
        {
            active.TryRemove(key, out ActiveRun? removed);
            removed?.Cancellation.Dispose();
        }
    }

    private LevelXRunRecord NewRun(Guid runId, LevelXRunRequest request, DateTimeOffset now)
        => new(
            runId,
            request.RequestId,
            request.CorrelationId,
            options.Application,
            options.Component,
            Environment.MachineName,
            request.Level,
            LevelXExecutionState.Requested,
            LevelXDeliveryState.NotAttempted,
            request.RequestedBy,
            request.IssuedAtUtc,
            null,
            null,
            null,
            LevelXComponentVersion.Capture(options.Application, options.Component, options.CatalogVersion),
            [],
            LastProgressAtUtc: now);

    private static string ComputeIntegrityHash(LevelXRunRecord run)
    {
        var payload = new
        {
            run.RunId,
            run.RequestId,
            run.CorrelationId,
            run.Application,
            run.Component,
            run.Host,
            Level = (int)run.Level,
            State = (int)run.ExecutionState,
            run.RequestedAtUtc,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            Version = run.Version,
            Tests = run.Tests.Select(x => new
            {
                x.TestId,
                x.Name,
                x.Owner,
                Level = (int)x.Level,
                Status = (int)x.Status,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Summary,
                x.Evidence,
                x.Code
            }).ToArray()
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, IntegrityJsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void ValidateCatalogue(IEnumerable<ILevelXLocalTest> tests)
    {
        IGrouping<string, ILevelXLocalTest>? duplicate = tests
            .GroupBy(x => x.TestId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate LevelX TestId '{duplicate.Key}'.");

        foreach (ILevelXLocalTest test in tests)
        {
            EngineeringDiagnosticPolicy.ValidateLevel(test.Level);
            if (test.IsDestructive && test.Level is EngineeringDiagnosticLevel.Level5Scan or EngineeringDiagnosticLevel.Level4Analysis)
                throw new InvalidOperationException($"LevelX test '{test.TestId}' is destructive at Level {(int)test.Level}; Levels 5 and 4 must never change state.");
        }
    }

    private sealed record ActiveRun(Guid RunId, CancellationTokenSource Cancellation);
}

public static class LevelXRequestSigning
{
    public static string CreateRunRequestSignature(
        string secret,
        string path,
        string timestamp,
        string nonce,
        LevelXRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string canonicalBody = string.Join("\n",
            request.RequestId.ToString("D"),
            request.CorrelationId.ToString("D"),
            ((int)request.Level).ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.RequestedBy ?? string.Empty,
            request.Reason ?? string.Empty,
            request.CallbackUrl ?? string.Empty,
            request.IssuedAtUtc.ToUniversalTime().ToString("O"),
            request.ExpiresAtUtc.ToUniversalTime().ToString("O"));
        byte[] body = Encoding.UTF8.GetBytes(canonicalBody);
        return CreateSignature(secret, "POST", path, timestamp, nonce, body);
    }

    public static bool VerifyRunRequest(
        string secret,
        string path,
        string timestamp,
        string nonce,
        LevelXRunRequest request,
        string suppliedSignature)
    {
        string expected = CreateRunRequestSignature(secret, path, timestamp, nonce, request);
        byte[] left;
        byte[] right;
        try
        {
            left = Convert.FromHexString(expected);
            right = Convert.FromHexString(suppliedSignature);
        }
        catch (FormatException)
        {
            return false;
        }

        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public static string CreateSignature(
        string secret,
        string method,
        string path,
        string timestamp,
        string nonce,
        ReadOnlySpan<byte> body)
    {
        if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("Secret is required.", nameof(secret));
        string bodyHash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        string canonical = $"{method.ToUpperInvariant()}\n{path}\n{timestamp}\n{nonce}\n{bodyHash}";
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool Verify(
        string secret,
        string method,
        string path,
        string timestamp,
        string nonce,
        ReadOnlySpan<byte> body,
        string suppliedSignature)
    {
        string expected = CreateSignature(secret, method, path, timestamp, nonce, body);
        byte[] left;
        byte[] right;
        try
        {
            left = Convert.FromHexString(expected);
            right = Convert.FromHexString(suppliedSignature);
        }
        catch (FormatException)
        {
            return false;
        }

        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public sealed class LevelXNonceCache
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> seen = new(StringComparer.Ordinal);
    private readonly TimeSpan lifetime;

    public LevelXNonceCache(TimeSpan? lifetime = null)
    {
        this.lifetime = lifetime ?? TimeSpan.FromMinutes(10);
    }

    public bool TryUse(string nonce, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(nonce)) return false;
        foreach ((string key, DateTimeOffset value) in seen)
        {
            if (nowUtc - value > lifetime) seen.TryRemove(key, out _);
        }
        return seen.TryAdd(nonce, nowUtc);
    }
}
