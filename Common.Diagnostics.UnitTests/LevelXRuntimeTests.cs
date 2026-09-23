namespace Common.Diagnostics.UnitTests;

public sealed class LevelXRuntimeTests
{
    [Fact]
    public void Level5AndLevel4RejectDestructiveTests()
    {
        Assert.Throws<ArgumentException>(() => new DelegateLevelXLocalTest(
            "L5.SAFE.001", "unsafe", "test", EngineeringDiagnosticLevel.Level5Scan, true,
            _ => Task.FromResult(EngineeringDiagnosticPolicy.Passed("L5.SAFE.001", "unsafe", "x"))));

        Assert.Throws<ArgumentException>(() => new DelegateLevelXLocalTest(
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
        string signature = LevelXRequestSigning.CreateSignature(
            "secret", "POST", "/diagnostics/run", "2026-09-22T00:00:00Z", "nonce-1", body);

        Assert.True(LevelXRequestSigning.Verify(
            "secret", "POST", "/diagnostics/run", "2026-09-22T00:00:00Z", "nonce-1", body, signature));
        Assert.False(LevelXRequestSigning.Verify(
            "secret", "POST", "/diagnostics/run", "2026-09-22T00:00:00Z", "nonce-2", body, signature));
    }


    [Fact]
    public void CanonicalRunRequestSignatureDetectsFieldTampering()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        LevelXRunRequest request = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            EngineeringDiagnosticLevel.Level4Analysis,
            "operator",
            "reason",
            "https://control.example/callback",
            now,
            now.AddMinutes(2));

        string timestamp = now.ToString("O");
        string signature = LevelXRequestSigning.CreateRunRequestSignature(
            "secret", "/api/engineering/diagnostics/levelx/run", timestamp, "nonce-2", request);

        Assert.True(LevelXRequestSigning.VerifyRunRequest(
            "secret", "/api/engineering/diagnostics/levelx/run", timestamp, "nonce-2", request, signature));

        LevelXRunRequest tampered = request with { Level = EngineeringDiagnosticLevel.Level3Verification };
        Assert.False(LevelXRequestSigning.VerifyRunRequest(
            "secret", "/api/engineering/diagnostics/levelx/run", timestamp, "nonce-2", tampered, signature));
    }

    [Fact]
    public void CompletionCallbackSignatureDetectsTampering()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid runId = Guid.NewGuid();
        Guid requestId = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();
        var version = new LevelXComponentVersion("App", "Service", "1.2.3");
        var run = new LevelXRunRecord(
            runId, requestId, correlationId, "App", "Service", "host",
            EngineeringDiagnosticLevel.Level4Analysis,
            LevelXExecutionState.Completed,
            LevelXDeliveryState.CallbackPending,
            "operator",
            now, now, now, now,
            version,
            [],
            "abc123");
        var callback = new LevelXCompletionCallback(runId, requestId, correlationId, run);

        string timestamp = now.ToString("O");
        string signature = LevelXRequestSigning.CreateCompletionCallbackSignature(
            "secret",
            "/api/engineering/diagnostics/levelx/callback",
            timestamp,
            "nonce-callback",
            callback);

        Assert.True(LevelXRequestSigning.VerifyCompletionCallback(
            "secret",
            "/api/engineering/diagnostics/levelx/callback",
            timestamp,
            "nonce-callback",
            callback,
            signature));

        var tamperedRun = run with { IntegrityHash = "tampered" };
        var tampered = callback with { Run = tamperedRun };
        Assert.False(LevelXRequestSigning.VerifyCompletionCallback(
            "secret",
            "/api/engineering/diagnostics/levelx/callback",
            timestamp,
            "nonce-callback",
            tampered,
            signature));
    }

    [Fact]
    public void NonceCannotBeReused()
    {
        LevelXNonceCache cache = new(TimeSpan.FromMinutes(10));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.True(cache.TryUse("abc", now));
        Assert.False(cache.TryUse("abc", now.AddSeconds(1)));
    }

    [Fact]
    public async Task SqliteStorePersistsAndFiltersHistory()
    {
        string root = Path.Combine(Path.GetTempPath(), "levelx-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SqliteLevelXRunStore(new LevelXStoreOptions { Path = Path.Combine(root, "levelx.db") });
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var run = new LevelXRunRecord(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "App", "Service", "host",
                EngineeringDiagnosticLevel.Level5Scan, LevelXExecutionState.Completed,
                LevelXDeliveryState.NotApplicable, "tester", now, now, now, now,
                LevelXComponentVersion.Capture("App", "Service"),
                [new LevelXTestResult("APP.L5.001", "Health", "App", EngineeringDiagnosticLevel.Level5Scan,
                    EngineeringDiagnosticStatus.Passed, now, now, "ok")],
                "hash");

            await store.SaveAsync(run);
            LevelXRunRecord? loaded = await store.GetAsync(run.RunId);
            IReadOnlyList<LevelXRunRecord> history = await store.QueryAsync(
                new LevelXHistoryQuery(Application: "App", TestId: "APP.L5.001"));

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
