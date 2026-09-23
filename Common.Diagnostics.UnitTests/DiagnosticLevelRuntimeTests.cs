namespace Common.Diagnostics.UnitTests;

public sealed class DiagnosticLevelRuntimeTests
{
    [Fact]
    public void Level5AndLevel4RejectDestructiveTests()
    {
        Assert.Throws<ArgumentException>(() => new DelegateDiagnosticLevelLocalTest(
            "L5.SAFE.001", "unsafe", "test", EngineeringDiagnosticLevel.Level5Scan, true,
            _ => Task.FromResult(EngineeringDiagnosticPolicy.Passed("L5.SAFE.001", "unsafe", "x"))));

        Assert.Throws<ArgumentException>(() => new DelegateDiagnosticLevelLocalTest(
            "L4.SAFE.001", "unsafe", "test", EngineeringDiagnosticLevel.Level4Analysis, true,
            _ => Task.FromResult(EngineeringDiagnosticPolicy.Passed("L4.SAFE.001", "unsafe", "x"))));
    }

    [Theory]
    [InlineData(EngineeringDiagnosticLevel.Level5Scan, 1)]
    [InlineData(EngineeringDiagnosticLevel.Level4Analysis, 2)]
    [InlineData(EngineeringDiagnosticLevel.Level3Verification, 3)]
    [InlineData(EngineeringDiagnosticLevel.Level2Repair, 4)]
    [InlineData(EngineeringDiagnosticLevel.Level1CriticalIntervention, 5)]
    public void StarfleetCumulativeSelectionIsNeverReversed(EngineeringDiagnosticLevel requested, int expected)
    {
        EngineeringDiagnosticLevel[] levels =
        [
            EngineeringDiagnosticLevel.Level5Scan,
            EngineeringDiagnosticLevel.Level4Analysis,
            EngineeringDiagnosticLevel.Level3Verification,
            EngineeringDiagnosticLevel.Level2Repair,
            EngineeringDiagnosticLevel.Level1CriticalIntervention
        ];

        int selected = levels.Count(level => (int)level >= (int)requested);
        Assert.Equal(expected, selected);
    }

    [Fact]
    public void RequestSignatureDetectsTampering()
    {
        byte[] body = System.Text.Encoding.UTF8.GetBytes("{\"level\":4}");
        string signature = DiagnosticLevelRequestSigning.CreateSignature(
            "secret", "POST", "/diagnostics/run", "2026-09-22T00:00:00Z", "nonce-1", body);

        Assert.True(DiagnosticLevelRequestSigning.Verify(
            "secret", "POST", "/diagnostics/run", "2026-09-22T00:00:00Z", "nonce-1", body, signature));
        Assert.False(DiagnosticLevelRequestSigning.Verify(
            "secret", "POST", "/diagnostics/run", "2026-09-22T00:00:00Z", "nonce-2", body, signature));
    }


    [Fact]
    public void CanonicalRunRequestSignatureDetectsFieldTampering()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DiagnosticLevelRunRequest request = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            EngineeringDiagnosticLevel.Level4Analysis,
            "operator",
            "reason",
            "https://control.example/callback",
            now,
            now.AddMinutes(2));

        string timestamp = now.ToString("O");
        string signature = DiagnosticLevelRequestSigning.CreateRunRequestSignature(
            "secret", "/api/engineering/diagnostics/diagnostic-level/run", timestamp, "nonce-2", request);

        Assert.True(DiagnosticLevelRequestSigning.VerifyRunRequest(
            "secret", "/api/engineering/diagnostics/diagnostic-level/run", timestamp, "nonce-2", request, signature));

        DiagnosticLevelRunRequest tampered = request with { Level = EngineeringDiagnosticLevel.Level3Verification };
        Assert.False(DiagnosticLevelRequestSigning.VerifyRunRequest(
            "secret", "/api/engineering/diagnostics/diagnostic-level/run", timestamp, "nonce-2", tampered, signature));
    }

    [Fact]
    public void CompletionCallbackSignatureDetectsTampering()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid runId = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();
        var version = new DiagnosticLevelComponentVersion("App", "Service", "1.2.3");
        var run = new DiagnosticLevelRunRecord(
            runId, requestId, correlationId, "App", "Service", "host",
            EngineeringDiagnosticLevel.Level4Analysis,
            DiagnosticLevelExecutionState.Completed,
            DiagnosticLevelDeliveryState.CallbackPending,
            "operator",
            now, now, now, now,
            version,
            [],
            "abc123");
        var callback = new DiagnosticLevelCompletionCallback(runId, requestId, correlationId, run);

        string timestamp = now.ToString("O");
        string signature = DiagnosticLevelRequestSigning.CreateCompletionCallbackSignature(
            "secret",
            "/api/engineering/diagnostics/diagnostic-level/callback",
            timestamp,
            "nonce-callback",
            callback);

        Assert.True(DiagnosticLevelRequestSigning.VerifyCompletionCallback(
            "secret",
            "/api/engineering/diagnostics/diagnostic-level/callback",
            timestamp,
            "nonce-callback",
            callback,
            signature));

        var tamperedRun = run with { IntegrityHash = "tampered" };
        var tampered = callback with { Run = tamperedRun };
        Assert.False(DiagnosticLevelRequestSigning.VerifyCompletionCallback(
            "secret",
            "/api/engineering/diagnostics/diagnostic-level/callback",
            timestamp,
            "nonce-callback",
            tampered,
            signature));
    }

    [Fact]
    public void ResultIntegrityDetectsPayloadTampering()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var run = new DiagnosticLevelRunRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "App",
            "Service",
            "host",
            EngineeringDiagnosticLevel.Level4Analysis,
            DiagnosticLevelExecutionState.Completed,
            DiagnosticLevelDeliveryState.CallbackPending,
            "operator",
            now,
            now,
            now,
            now,
            new DiagnosticLevelComponentVersion("App", "Service", "1.2.3"),
            [new DiagnosticLevelTestResult(
                "APP.L4.001",
                "Probe",
                "App",
                EngineeringDiagnosticLevel.Level4Analysis,
                EngineeringDiagnosticStatus.Passed,
                now,
                now,
                "ok")]);

        run = run with { IntegrityHash = DiagnosticLevelIntegrity.ComputeHash(run) };
        Assert.True(DiagnosticLevelIntegrity.Verify(run));

        DiagnosticLevelRunRecord tampered = run with
        {
            Tests = [run.Tests[0] with { Summary = "tampered" }]
        };
        Assert.False(DiagnosticLevelIntegrity.Verify(tampered));
    }

    [Fact]
    public void NonceCannotBeReused()
    {
        DiagnosticLevelNonceCache cache = new(TimeSpan.FromMinutes(10));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.True(cache.TryUse("abc", now));
        Assert.False(cache.TryUse("abc", now.AddSeconds(1)));
    }

    [Fact]
    public async Task SqliteStorePersistsAndFiltersHistory()
    {
        string root = Path.Combine(Path.GetTempPath(), "diagnostic-level-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SqliteDiagnosticLevelRunStore(new DiagnosticLevelStoreOptions { Path = Path.Combine(root, "diagnostic-level.db") });
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var run = new DiagnosticLevelRunRecord(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "App", "Service", "host",
                EngineeringDiagnosticLevel.Level5Scan, DiagnosticLevelExecutionState.Completed,
                DiagnosticLevelDeliveryState.NotApplicable, "tester", now, now, now, now,
                DiagnosticLevelComponentVersion.Capture("App", "Service"),
                [new DiagnosticLevelTestResult("APP.L5.001", "Health", "App", EngineeringDiagnosticLevel.Level5Scan,
                    EngineeringDiagnosticStatus.Passed, now, now, "ok")],
                "hash");

            await store.SaveAsync(run);
            DiagnosticLevelRunRecord? loaded = await store.GetAsync(run.RunId);
            IReadOnlyList<DiagnosticLevelRunRecord> history = await store.QueryAsync(
                new DiagnosticLevelHistoryQuery(Application: "App", TestId: "APP.L5.001"));

            Assert.NotNull(loaded);
            Assert.Single(history);
            Assert.Equal(run.CorrelationId, history[0].CorrelationId);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
