namespace Common.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DiagnosticsServiceCollectionExtensions
{
    public static IServiceCollection AddCommonDiagnostics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IDiagnosticRunner, DiagnosticRunner>();
        services.AddSingleton<IEngineeringDiagnosticRunStore, JsonEngineeringDiagnosticRunStore>();
        services.AddSingleton<EngineeringDiagnosticEngine>();

        services.TryAddSingleton(new DiagnosticOperationsOptions());
        services.TryAddSingleton<IDiagnosticSuppressionStore, InMemoryDiagnosticSuppressionStore>();
        services.TryAddSingleton<IDiagnosticRetentionStore, JsonDiagnosticRetentionStore>();
        services.TryAddSingleton<IDiagnosticOperationsRouter, DiagnosticOperationsRouter>();

        return services;
    }

    public static IServiceCollection AddDiagnosticCheck<TCheck>(this IServiceCollection services)
        where TCheck : class, IDiagnosticCheck
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IDiagnosticCheck, TCheck>();
        return services;
    }

    public static IServiceCollection AddDiagnosticTelemetryDestination<TDestination>(
        this IServiceCollection services)
        where TDestination : class, IDiagnosticTelemetryDestination
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDiagnosticTelemetryDestination, TDestination>();
        return services;
    }
}
