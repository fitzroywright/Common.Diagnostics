# Common.Diagnostics

Reusable .NET 10 diagnostics and engineering-readiness infrastructure for the application family.

## Repository policy

`main` is the authoritative trunk and the only branch that should be used for ongoing development, integration, packaging, and releases. Older feature/integration branches are historical once their work has been incorporated into `main`.

## Two complementary layers

### Operational diagnostics

Use `IDiagnosticCheck` and `IDiagnosticRunner` for small reusable health/readiness checks such as HTTP endpoints and infrastructure probes.

Reusable checks include:

- `TcpEndpointDiagnosticCheck` for PostgreSQL, SQL Server, LDAP/AD, SMB endpoints, and other TCP dependencies.
- `HttpEndpointDiagnosticCheck` for OpenBao, Microsoft Graph, web APIs, messaging webhooks, and other HTTP dependencies.
- `FileSystemDiagnosticCheck` for local and NAS storage read/read-write validation.
- `DelegateDiagnosticCheck` for adapting provider-specific checks such as Common.Secrets health, S3 storage, application messaging, and Active Directory binds without duplicating runner behavior.

### Standard dependency catalogue

Applications should register dependencies in this order when applicable:

1. PostgreSQL/database connectivity.
2. OpenBao and Common.Secrets provider health/resolution.
3. Common.Storage targets: local/NAS/S3.
4. Microsoft Graph and messaging transports.
5. Active Directory/directory services.

Checks must never include secret values, bearer tokens, passwords, full sensitive connection strings, or document contents in diagnostic messages.

### Engineering diagnostics

Use the engineering diagnostics API for auditable Level 5 through Level 1 diagnostic runs:

- `EngineeringDiagnosticLevel` and `EngineeringDiagnosticStatus`
- `EngineeringDiagnosticCheckResult` and `EngineeringDiagnosticRun`
- `EngineeringDiagnosticPolicy`
- `EngineeringDiagnosticEngine`
- `IEngineeringDiagnosticRunStore`
- `JsonEngineeringDiagnosticRunStore`
- `EngineeringDiagnosticsHtml`

The level order is deliberate: Level 5 is the quickest/lightest diagnostic level and Level 1 is the deepest/full-system level. Deeper runs are cumulative according to engineering diagnostic policy.

`EngineeringDiagnosticEngine` owns level filtering, failure isolation, intervention gates, status aggregation, run creation, and persistence. Applications supply only their application-specific diagnostic check definitions.

## Ownership boundary

Common.Diagnostics contains reusable diagnostic mechanics and checks that apply to more than one application. It does not own application-specific business concepts.

For example, Cafeteria retains checks for its funding policy, employee/holiday sources, publication backlog, service database, and service-event stream. Those checks are passed to the Common engineering engine.

`Aegis.Diagnostics` is the top-level operator/orchestration application. `Common.Diagnostics` remains the reusable engine and wire-contract library consumed by applications.

This keeps application knowledge in each host while ensuring diagnostic policy, persistence, UI contracts, and orchestration behave consistently across applications.

## Dependency injection

```csharp
services.AddCommonDiagnostics();
```

This registers the operational diagnostic runner, durable engineering run store, and engineering diagnostic engine.

Additional operational checks can be registered with:

```csharp
services.AddDiagnosticCheck<MyDiagnosticCheck>();
```

## Safety

Level 2 and Level 1 runs are intervention gates. A diagnostic run gathers and records evidence but does not automatically perform destructive repair actions.

## Current maturity

The reusable diagnostics engine, contracts, persistence, safety policy, and integration hooks are implemented. Remaining work is primarily runtime proof: exercise Level 5 through Level 1 against real consumers, deliberately introduce controlled failures, verify accurate non-green reporting, and complete cross-application regression testing.
