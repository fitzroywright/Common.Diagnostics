# Common.Diagnostics

Reusable .NET 10 diagnostics and engineering-readiness infrastructure for the FFP application family.

## Two complementary layers

### Operational diagnostics

Use `IDiagnosticCheck` and `IDiagnosticRunner` for small reusable health/readiness checks such as HTTP endpoints and infrastructure probes.

Reusable checks now include:

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

`EngineeringDiagnosticEngine` owns level filtering, failure isolation, intervention gates, status aggregation, run creation, and persistence. Applications supply only their application-specific diagnostic check definitions.

## Ownership boundary

Common.Diagnostics should contain reusable diagnostics mechanics and checks that apply to more than one application. It should not know about application-specific business concepts.

For example, Cafeteria retains checks for its funding policy, FFP employee/holiday sources, publication backlog, service database, and service-event stream. Those checks are passed to the Common engineering engine.

This keeps application knowledge in the host while ensuring diagnostic policy, persistence, UI, and orchestration behave consistently across applications.

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
