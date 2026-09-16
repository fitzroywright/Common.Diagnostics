using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Diagnostics;

public enum AegisHealthState
{
    Healthy,
    Degraded,
    Unhealthy
}

public sealed record AegisHealthAssessment(
    AegisHealthState State,
    string? Summary = null)
{
    public static AegisHealthAssessment Healthy(string? summary = null) => new(AegisHealthState.Healthy, summary);
    public static AegisHealthAssessment Degraded(string? summary = null) => new(AegisHealthState.Degraded, summary);
    public static AegisHealthAssessment Unhealthy(string? summary = null) => new(AegisHealthState.Unhealthy, summary);
}

public sealed record AegisHealthReport(
    string Status,
    string Application,
    string Version,
    string Environment,
    string InstanceId,
    DateTimeOffset Utc,
    string? Summary = null);

public static class AegisHealthEndpointExtensions
{
    /// <summary>
    /// Maps the suite-standard Aegis health endpoint using the host environment automatically.
    /// Every independently deployable Aegis HTTP application should use this overload.
    /// Application-specific dependency checks belong in <paramref name="assess"/>; Common.Diagnostics
    /// remains provider-neutral.
    /// </summary>
    public static RouteHandlerBuilder MapAegisHealth(
        this WebApplication app,
        string application,
        Func<IServiceProvider, CancellationToken, Task<AegisHealthAssessment>>? assess = null,
        string path = "/health",
        string? version = null,
        string? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        return ((IEndpointRouteBuilder)app).MapAegisHealth(
            application,
            app.Environment.EnvironmentName,
            assess,
            path,
            version,
            instanceId);
    }

    public static RouteHandlerBuilder MapAegisHealth(
        this IEndpointRouteBuilder endpoints,
        string application,
        string environment,
        Func<IServiceProvider, CancellationToken, Task<AegisHealthAssessment>>? assess = null,
        string path = "/health",
        string? version = null,
        string? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (string.IsNullOrWhiteSpace(application)) throw new ArgumentException("Application is required.", nameof(application));
        if (string.IsNullOrWhiteSpace(environment)) throw new ArgumentException("Environment is required.", nameof(environment));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Health path is required.", nameof(path));

        string resolvedApplication = application.Trim();
        string resolvedEnvironment = environment.Trim();
        string resolvedVersion = string.IsNullOrWhiteSpace(version)
            ? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown"
            : version.Trim();
        string resolvedInstanceId = string.IsNullOrWhiteSpace(instanceId)
            ? Environment.MachineName
            : instanceId.Trim();

        return endpoints.MapGet(path, async (HttpContext context, CancellationToken cancellationToken) =>
        {
            AegisHealthAssessment assessment;
            try
            {
                assessment = assess is null
                    ? AegisHealthAssessment.Healthy("Application process is running.")
                    : await assess(context.RequestServices, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                assessment = AegisHealthAssessment.Unhealthy($"Health assessment failed: {exception.GetType().Name}.");
            }

            var report = new AegisHealthReport(
                assessment.State.ToString(),
                resolvedApplication,
                resolvedVersion,
                resolvedEnvironment,
                resolvedInstanceId,
                DateTimeOffset.UtcNow,
                assessment.Summary);

            int statusCode = assessment.State == AegisHealthState.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;

            return Results.Json(report, statusCode: statusCode);
        })
        .AllowAnonymous();
    }
}
