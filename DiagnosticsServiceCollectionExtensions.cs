namespace Common.Diagnostics;

using Microsoft.Extensions.DependencyInjection;

public static class DiagnosticsServiceCollectionExtensions
{
    public static IServiceCollection AddCommonDiagnostics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IDiagnosticRunner, DiagnosticRunner>();
        services.AddSingleton<IEngineeringDiagnosticRunStore, JsonEngineeringDiagnosticRunStore>();
        services.AddSingleton<EngineeringDiagnosticEngine>();
        return services;
    }

    public static IServiceCollection AddDiagnosticCheck<TCheck>(this IServiceCollection services)
        where TCheck : class, IDiagnosticCheck
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IDiagnosticCheck, TCheck>();
        return services;
    }
}
