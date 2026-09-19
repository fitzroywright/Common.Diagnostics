# Aegis Operational Contract V2 Acceptance Matrix

This matrix is the shared acceptance baseline for Common.Diagnostics V2 and the applications that publish operational evidence.

| Area | Required proof |
|---|---|
| Health truthfulness | HTTP success alone must not become Healthy. Missing, stale, malformed or unreachable evidence becomes Unknown unless the application has explicit stronger evidence. |
| Staleness | A previously Healthy observation becomes Unknown after its configured freshness window. |
| Registration | Normal restart reuses durable identity and does not create duplicate Pending registrations. Credential loss enters recovery. Revoked and identity-conflict states remain distinct. |
| Configuration | Required and optional readiness are counted separately. Missing required names may be shown; sensitive values must not be shown. |
| Secrets | Provider not configured, provider unreachable, authentication failure and missing required secret names remain distinguishable. No secret values are returned. |
| Messaging | Queued, dispatching, provider accepted, delivered, failed and dead-letter remain semantically distinct. Provider acceptance is never labeled recipient delivery without receipt evidence. |
| Queue health | Pending depth, oldest age, retrying, failed/dead-letter and expired leases remain separately observable. |
| Correlation | Business correlation survives application, durable outbox/queue and provider-boundary transitions wherever the application has a durable business ID. |
| Flow semantics | Branches remain independent. Warning and timeout thresholds derive from real stage timestamps, not decorative timers. |
| Offline operation | Local-first applications preserve durable work while offline. Offline is not automatically Failed. |
| Synchronization | Pending count, oldest pending age, last attempt/success and failed items are based on durable queue/store evidence. |
| Decisions | Valid negative outcomes such as decline/deny/unsupported may be Healthy informational decisions rather than infrastructure failures. |
| Storage | Reads verify integrity. Version history is retained according to application policy. Application-scoped access cannot cross declared owner boundaries. |
| Security | Operational capabilities are permission keys, not job titles. Authorization decisions remain auditable. |
| Runbooks | Registration/diagnostics reference application-owned runbooks; runbooks contain no credentials or secret values. |
| Central outage | Loss of Operations/Diagnostics/Configuration telemetry must not stop the business application unless that central service is itself a required business dependency. |
| Recovery | Recovery is proven by fresh post-repair evidence, not inferred from the repair action succeeding. |

## Reference applications

- **Aegis.Hello** — canonical registration lifecycle, configuration contract and destructive registration recovery acceptance.
- **Aegis.Cafeteria** — local-first POS transaction/synchronization evidence.
- **RequestPortal** — request submission, document persistence, staff authentication, durable outbox and downstream FFP Manager handoff.
- **Aegis.Studio** — workspace messaging, attachments, gallery publication, durable external notification queues and provider branches.
- **Ebolito** — engagement flow, Common.Messaging correlation and valid business/security decision diagnostics.

## Required CI sequence

1. Common.Diagnostics
2. Common.Registration
3. Common.Secrets
4. Common.Messaging
5. Common.Security
6. Common.Storage
7. Aegis.Diagnostics
8. Aegis.Configuration
9. Aegis.Hello
10. Application consumers

A red historical pipeline must be reported separately from a new regression. A change is not accepted as green merely because the repository already had failing CI.
