namespace Common.Diagnostics.UnitTests;

public sealed class StandardDependencyChecksTests
{
    [Fact]
    public async Task PostgreSqlCheckReportsHealthyWhenProbeSucceeds()
    {
        PostgreSqlDiagnosticCheck check = new("PostgreSQL", "test database", _ => Task.CompletedTask);

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task PostgreSqlCheckReportsUnhealthyWhenProbeFails()
    {
        PostgreSqlDiagnosticCheck check = new("PostgreSQL", "test database", _ => throw new InvalidOperationException("boom"));

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task SecretResolutionCheckNeverIncludesSecretValues()
    {
        SecretResolutionDiagnosticCheck check = new(
            "Secrets",
            "test provider",
            ["DatabasePassword", "ApiToken"],
            (name, _) => Task.FromResult<string?>(name == "DatabasePassword" ? "super-secret-value" : null));

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Unhealthy, result.Status);
        Assert.Contains("ApiToken", result.Message);
        Assert.DoesNotContain("super-secret-value", result.Message);
    }

    [Fact]
    public async Task StorageCheckReportsProbeFailure()
    {
        StorageTargetDiagnosticCheck check = new("Storage", "test target", _ => throw new IOException("offline"));

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task MessagingCheckReportsHealthyWhenProbeSucceeds()
    {
        MessagingDiagnosticCheck check = new("Messaging", "test transport", _ => Task.CompletedTask);

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ActiveDirectoryCheckWarnsWhenNoServersAreConfigured()
    {
        ActiveDirectoryEndpointDiagnosticCheck check = new("Active Directory", []);

        DiagnosticResult result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Warning, result.Status);
    }
}
