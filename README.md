# Common.Diagnostics

Reusable .NET 10 diagnostics and engineering-readiness infrastructure for the FFP application family.

## Two complementary layers

### Operational diagnostics

Use `IDiagnosticCheck` and `IDiagnosticRunner` for small reusable health/readiness checks such as HTTP endpoints and infrastructure probes.

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
