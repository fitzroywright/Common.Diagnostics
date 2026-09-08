using Common.Diagnostics;
using Xunit;

namespace Common.Diagnostics.UnitTests;

public sealed class JsonEngineeringDiagnosticRunStoreTests
{
    [Fact]
    public async Task SaveReadResolve_RoundTripsRun()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Common.Diagnostics.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "runs.json");

        try
        {
            JsonEngineeringDiagnosticRunStore store = new(path);
            EngineeringDiagnosticRun run = new(
                Guid.NewGuid(),
                EngineeringDiagnosticLevel.Level4Analysis,
                "TestApp",
                "Test",
                "tester",
                "reason",
                DateTimeOffset.UtcNow.AddSeconds(-1),
                DateTimeOffset.UtcNow,
                EngineeringDiagnosticStatus.Warning,
                [EngineeringDiagnosticPolicy.Warning("check", "Check", "warning")]);

            await store.SaveAsync(run);

            EngineeringDiagnosticRun? loaded = await store.GetAsync(run.RunId);
            Assert.NotNull(loaded);
            Assert.Equal("TestApp", loaded.Application);
            Assert.Equal(EngineeringDiagnosticStatus.Warning, loaded.Status);

            EngineeringDiagnosticRun? resolved = await store.ResolveAsync(run.RunId, "engineer", "fixed");
            Assert.NotNull(resolved);
            Assert.Equal("engineer", resolved.ResolvedBy);
            Assert.Equal("fixed", resolved.Resolution);
            Assert.NotNull(resolved.ResolvedAt);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public async Task GetRecent_OrdersNewestFirstAndCapsResult()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Common.Diagnostics.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "runs.json");

        try
        {
            JsonEngineeringDiagnosticRunStore store = new(path);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            for (int index = 0; index < 3; index++)
            {
                await store.SaveAsync(new EngineeringDiagnosticRun(
                    Guid.NewGuid(),
                    EngineeringDiagnosticLevel.Level5Scan,
                    "TestApp",
                    "Test",
                    "tester",
                    null,
                    now.AddMinutes(index),
                    now.AddMinutes(index),
                    EngineeringDiagnosticStatus.Passed,
                    []));
            }

            IReadOnlyList<EngineeringDiagnosticRun> recent = await store.GetRecentAsync(2);
            Assert.Equal(2, recent.Count);
            Assert.True(recent[0].StartedAt > recent[1].StartedAt);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
