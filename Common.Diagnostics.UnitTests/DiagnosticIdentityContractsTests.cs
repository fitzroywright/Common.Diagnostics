namespace Common.Diagnostics.UnitTests;

using Xunit;

public sealed class DiagnosticIdentityContractsTests
{
    [Theory]
    [InlineData(DiagnosticStatus.Healthy, OperationalHealth.Healthy)]
    [InlineData(DiagnosticStatus.Warning, OperationalHealth.Degraded)]
    [InlineData(DiagnosticStatus.Unhealthy, OperationalHealth.Unhealthy)]
    [InlineData(DiagnosticStatus.Unknown, OperationalHealth.Unknown)]
    public void DiagnosticStatusMapsToOperationalHealth(DiagnosticStatus status, OperationalHealth expected)
        => Assert.Equal(expected, OperationalHealthMapping.From(status));

    [Fact]
    public void IdentityCarriesConfigurationCompatibleDeploymentIdentity()
    {
        DiagnosticApplicationIdentity identity = new("Aegis.Cafeteria.Services", "Cafeteria Services", "1.0.0", "FFP-JM", "SVC-01");
        Assert.Equal("Aegis.Cafeteria.Services", identity.ApplicationId);
        Assert.Equal("FFP-JM", identity.SiteId);
        Assert.Equal("SVC-01", identity.InstanceId);
    }

    [Fact]
    public void ApplicationStateCanRepresentUnknownWithoutPretendingConfigurationIsMissing()
    {
        DiagnosticApplicationState state = new(new("RequestPortal", "Request Portal", "1.0.0"), OperationalHealth.Unknown, [], DateTimeOffset.UtcNow);
        Assert.Equal(OperationalHealth.Unknown, state.Health);
        Assert.Empty(state.Dependencies);
    }
}
