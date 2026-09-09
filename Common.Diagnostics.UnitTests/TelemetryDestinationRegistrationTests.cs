namespace Common.Diagnostics.UnitTests;

using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class TelemetryDestinationRegistrationTests
{
    [Fact]
    public void StandardTelemetryRegistersAllOperationalDestinations()
    {
        ServiceCollection services = new();
        services.AddCommonDiagnostics();
        services.AddStandardDiagnosticTelemetry();
        using ServiceProvider provider = services.BuildServiceProvider();

        string[] names = provider.GetServices<IDiagnosticTelemetryDestination>()
            .Select(destination => destination.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(["Email", "Graylog", "Slack", "Wazuh"], names);
    }
}
