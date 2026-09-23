namespace Common.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DiagnosticsServiceCollectionExtensions
{
    public static IServiceCollection AddCommonDiagnostics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IDiagnosticRunner, DiagnosticRunner>();
        services.AddScoped<ILeveledDiagnosticRunner, LeveledDiagnosticRunner>();
        services.AddSingleton<IEngineeringDiagnosticRunStore, JsonEngineeringDiagnosticRunStore>();
        services.AddSingleton<EngineeringDiagnosticEngine>();
        services.TryAddSingleton(new DiagnosticLevelStoreOptions());
        services.TryAddSingleton(new DiagnosticLevelExecutionOptions());
        services.TryAddSingleton(new DiagnosticLevelEndpointSecurityOptions());
        services.TryAddSingleton<IDiagnosticLevelRequestCredentialProvider, StaticDiagnosticLevelRequestCredentialProvider>();
        services.TryAddSingleton<IDiagnosticLevelCompletionNotifier, HttpDiagnosticLevelCompletionNotifier>();
        services.TryAddSingleton<IDiagnosticLevelRunStore, SqliteDiagnosticLevelRunStore>();
        services.TryAddSingleton<DiagnosticLevelNonceCache>();
        services.AddSingleton<DiagnosticLevelExecutionService>();

        services.TryAddSingleton(new DiagnosticOperationsOptions());
        services.TryAddSingleton<IDiagnosticSuppressionStore, InMemoryDiagnosticSuppressionStore>();
        services.TryAddSingleton<IDiagnosticRetentionStore, JsonDiagnosticRetentionStore>();
        services.TryAddSingleton<IDiagnosticOperationsRouter, DiagnosticOperationsRouter>();

        return services;
    }

    public static IServiceCollection AddStandardDiagnosticTelemetry(
        this IServiceCollection services,
        GraylogTelemetryOptions? graylog = null,
        WazuhTelemetryOptions? wazuh = null,
        SlackTelemetryOptions? slack = null,
        EmailTelemetryOptions? email = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(graylog ?? new GraylogTelemetryOptions());
        services.AddSingleton(wazuh ?? new WazuhTelemetryOptions());
        services.AddSingleton(slack ?? new SlackTelemetryOptions());
        services.AddSingleton(email ?? new EmailTelemetryOptions());
        services.AddSingleton<GraylogDiagnosticTelemetryDestination>();
        services.AddSingleton<IDiagnosticTelemetryDestination>(provider => provider.GetRequiredService<GraylogDiagnosticTelemetryDestination>());
        services.AddHttpClient<WazuhDiagnosticTelemetryDestination>();
        services.AddSingleton<IDiagnosticTelemetryDestination>(provider => provider.GetRequiredService<WazuhDiagnosticTelemetryDestination>());
        services.AddHttpClient<SlackDiagnosticTelemetryDestination>();
        services.AddSingleton<IDiagnosticTelemetryDestination>(provider => provider.GetRequiredService<SlackDiagnosticTelemetryDestination>());
        services.AddSingleton<EmailDiagnosticTelemetryDestination>();
        services.AddSingleton<IDiagnosticTelemetryDestination>(provider => provider.GetRequiredService<EmailDiagnosticTelemetryDestination>());
        return services;
    }

    public static IServiceCollection AddDiagnosticCheck<TCheck>(this IServiceCollection services)
        where TCheck : class, IDiagnosticCheck
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IDiagnosticCheck, TCheck>();
        return services;
    }

    public static IServiceCollection AddDiagnosticLevelTest<TTest>(this IServiceCollection services)
        where TTest : class, IDiagnosticLevelLocalTest
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDiagnosticLevelLocalTest, TTest>();
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
