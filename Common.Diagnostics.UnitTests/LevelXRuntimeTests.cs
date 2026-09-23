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
