# Common.Diagnostics

Reusable .NET 10 operational and engineering diagnostics for the Aegis application family.

## V1 ownership boundary

Aegis.Configuration answers **what an application requires and whether the application reports it configured**.

Common.Diagnostics answers **whether the running application and its dependencies are working**. It must not become a second configuration service and must never expose secret values.

Applications are authoritative for their diagnostics. They run their own application-specific checks through Common.Diagnostics and expose/report the resulting operational state. Aegis.Diagnostics aggregates, stores, correlates and presents that state.

All diagnostic publishers should use `DiagnosticApplicationIdentity` with the same `ApplicationId`, `SiteId`, `InstanceId` and `Version` used by Aegis.Configuration. The normalized V1 operational states are `Healthy`, `Degraded`, `Unhealthy` and `Unknown`.

## Suite-wide `/health` standard

Every independently deployable Aegis HTTP application or service must expose `GET /health` through `Common.Diagnostics` using `MapAegisHealth`. Class libraries do not expose health endpoints.

The endpoint is intentionally small, anonymous, safe to poll repeatedly, non-destructive and must never expose credentials, connection strings or other sensitive diagnostic evidence.

Canonical HTTP behavior:

- `Healthy` -> HTTP 200
- `Degraded` -> HTTP 200
- `Unhealthy` -> HTTP 503

The common response contract contains `status`, `application`, `version`, `environment`, `instanceId`, `utc` and an optional safe `summary`.

The shared health implementation is provider-neutral. Database engines, secret providers, storage systems and other concrete dependencies must not be hard-coded into `Common.Diagnostics`. Each application supplies its own dependency assessment callback and determines which dependencies are critical to its core role.

A deployable application's normal pattern is:

```csharp
app.MapAegisHealth("Aegis.Example", async (services, cancellationToken) =>
{
    // Run application-owned dependency checks here.
    return AegisHealthAssessment.Healthy("Application and critical dependencies are healthy.");
});
```

If an application has no critical external dependency to assess, `app.MapAegisHealth("Aegis.Example")` provides a process-level health response.

Deployment automation may use `/health` for start/validate/rollback decisions. A temporary non-critical external outage should normally produce `Degraded`, not falsely report the application itself as dead. Failure of a dependency required for the application's core role should produce `Unhealthy`.

## Starfleet Engineering Protocols

The existing Level 5 through Level 1 lifecycle is preserved:

- **Level 5 — Scan:** fast baseline health, heartbeat and critical dependency checks.
- **Level 4 — Analysis:** deeper dependency, data-flow, performance and error-history analysis.
- **Level 3 — Verification:** verify a suspected fault or verify recovery and capture evidence.
- **Level 2 — Repair:** controlled repair stage requiring a reason, authorization and a registered safe action.
- **Level 1 — Critical Intervention:** emergency intervention requiring high-trust approval, full audit evidence and a narrowly scoped playbook.

Lower numbers are deliberately more serious/deeper interventions. This ordering is retained for compatibility with the existing engineering engine and application implementations.

## Reusable checks

`IDiagnosticCheck` / `IDiagnosticRunner` support reusable operational checks. Standard checks include TCP, HTTP and file-system dependencies plus delegate checks for provider-specific diagnostics. Applications retain their own business-specific checks.

Checks and evidence must never include secret values, bearer tokens, passwords, full sensitive connection strings or document contents.

## Engineering diagnostics

The engineering layer includes `EngineeringDiagnosticLevel`, `EngineeringDiagnosticStatus`, `EngineeringDiagnosticRun`, `EngineeringDiagnosticLifecycle`, `EngineeringDiagnosticPolicy`, `EngineeringDiagnosticEngine`, `IEngineeringDiagnosticRunStore`, `JsonEngineeringDiagnosticRunStore` and the engineering HTML renderer.

History is intentional in Diagnostics. Operational incidents and engineering runs are useful evidence and should be retained according to the configured retention policy.

## Aegis.Diagnostics

Aegis.Diagnostics is the operator-facing aggregation/orchestration product. Common.Diagnostics remains the reusable engine and wire-contract library consumed by applications. Aegis.Diagnostics may actively request an engineering run, but configuration completeness remains owned by Aegis.Configuration.

## Dependency injection

```csharp
services.AddCommonDiagnostics();
```

Additional checks can be registered with:

```csharp
services.AddDiagnosticCheck<MyDiagnosticCheck>();
```

## Safety

Level 2 and Level 1 are intervention gates. Diagnostic collection itself must remain observational and must not silently perform destructive repair actions.
