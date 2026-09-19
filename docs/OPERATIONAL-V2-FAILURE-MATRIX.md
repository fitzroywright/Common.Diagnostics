# Operational V2 Controlled Failure Matrix

This matrix records the controlled failure/recovery proof for the Operational Contract V2 pass.

Legend:
- **AUTOMATED** — covered by repeatable unit/integration tests in source control.
- **SCRIPTED** — executable acceptance script exists but requires an authorized deployed environment.
- **INFRASTRUCTURE-BLOCKED** — requires live credentials/services and was not executed from this repository-only pass.
- **PARTIAL** — safe failure behavior is implemented, but full end-to-end recovery proof still requires the deployed estate.

| Failure | Status | Evidence / expected behavior |
|---|---|---|
| Diagnostic check throws | AUTOMATED | Common.Diagnostics `FaultInjectionTests.RunnerContainsThrownCheckAndContinuesWithRemainingChecks`; the runner contains the fault and continues. |
| Dependency returns HTTP 503 | AUTOMATED | Common.Diagnostics HTTP diagnostic test reports Unhealthy without crashing the runner. |
| Dependency transport failure | AUTOMATED | Common.Diagnostics HTTP diagnostic test contains the transport exception. |
| Stale telemetry | AUTOMATED | Diagnostic Contract V2 tests force a previously Healthy observation to effective Unknown after the freshness window. |
| Malformed health payload | AUTOMATED | Aegis.Diagnostics classifier tests prove HTTP 200 + malformed payload is Unknown. |
| HTTP 200 with no recognized state | AUTOMATED | Aegis.Diagnostics classifier tests prove it is Unknown, not Healthy. |
| Registration credential loss | AUTOMATED + SCRIPTED | Common.Registration tests enter RecoveryPending rather than creating a duplicate registration; Hello destructive acceptance script exercises deployed recovery. |
| Invalid registration credential | AUTOMATED + SCRIPTED | Common.Registration tests request recovery rather than create a duplicate pending record. |
| Revoked registration | AUTOMATED + SCRIPTED | Registration lifecycle preserves Revoked as a distinct state; Hello destructive script exercises revoke/purge. |
| Registration identity conflict | AUTOMATED + SCRIPTED | Lifecycle tests and Hello destructive script preserve IdentityConflict distinctly. |
| Configuration unavailable during registration | PARTIAL | Common.Registration contains transport failure handling; deployed outage/recovery proof requires the live Configuration endpoint. |
| Operations unavailable | PARTIAL | Application telemetry publishers catch/report locally and never make business processing depend on Operations. Deployed outage proof requires the running applications. |
| Messaging process restart | AUTOMATED | Common.Messaging `FileQueue_SurvivesProviderRecreation` proves durable queued work survives provider/process recreation. |
| Duplicate messaging dispatch | AUTOMATED | Common.Messaging idempotency tests preserve already-successful channel legs. |
| Messaging provider branch failure | AUTOMATED | Dispatcher isolates one failing provider/channel and continues independent branches. |
| Retry exhaustion / dead letter | AUTOMATED | Common.Messaging durable hardening/dead-letter tests cover dead-letter health, inspection, replay and completion. |
| Multi-node lease recovery | AUTOMATED when PostgreSQL test dependency is available | Common.Messaging PostgreSQL tests cover exclusive live leases, expired lease recovery and shared retry/dead-letter/idempotency. |
| Provider accepted vs delivered | AUTOMATED | Messaging diagnostics contract tests prove ProviderAccepted is not Delivered without receipt evidence. |
| Secrets provider unavailable | AUTOMATED | Common.Secrets V2 tests keep provider reachability and authentication state distinct; no secret values are returned. |
| Secrets authentication failure | AUTOMATED | Common.Secrets V2 test returns AuthenticationFailed separately from generic unreachability. |
| Missing required secret | AUTOMATED | Missing secret names are reported without returning values. |
| Missing required configuration | AUTOMATED | Aegis.Configuration readiness tests distinguish required/optional settings and do not expose values. |
| Cafeteria Services/network unavailable | PARTIAL | POS durable SQLite outbox preserves pending transactions; synchronization telemetry reports Offline/Degraded/Warning from durable evidence. Full deployed recovery requires POS/Services connectivity testing. |
| Cafeteria restart with pending transaction | PARTIAL | Durable outbox architecture is present; deployed restart/eventual-sync proof remains required. |
| Request Portal downstream unavailable | PARTIAL | Durable outbox retries; FFP Manager submission failure is emitted at the Downstream stage and rethrown for retry. Live downstream recovery requires the deployed bridge. |
| Request Portal authentication failure | PARTIAL | OIDC telemetry localizes callback/claims/authorization failures without token/claim leakage. End-to-end identity-provider failure requires the live provider. |
| Studio attachment failure | PARTIAL | Attachment/storage exceptions are contained, return controlled failure and emit sanitized flow evidence. Full NAS/provider outage recovery requires deployed storage. |
| Studio gallery publication failure | PARTIAL | Gallery publication emits an independent sanitized failed stage. Public-gallery provider verification remains environment-dependent. |
| Studio one-provider notification failure | AUTOMATED at Common.Messaging layer / PARTIAL in Studio | Common.Messaging proves independent provider branches; Studio maps external notifications onto that durable queue. |
| Ebolito valid negative decision | AUTOMATED contract semantics / implemented integration | Valid negative outcomes can remain Healthy informational decisions rather than infrastructure failure. |
| Storage corruption | AUTOMATED | Common.Storage detects SHA-256 corruption on current and historical reads. |
| Storage disappearance | AUTOMATED | Common.Storage health reports unavailable after root disappearance. |
| Storage cross-application access | AUTOMATED | Application-scoped wrapper rejects access to an object owned by another application. |
| Database unavailable — Configuration | IMPLEMENTED / INFRASTRUCTURE-BLOCKED | Registration APIs return controlled 503 and do not report a successful state change; live PostgreSQL outage/recovery not executed in this pass. |
| Database backup/restore — Registration | SCRIPTED | Hello production gate references `verify-registration-backup-restore.sh`; requires isolated restore database URLs. |
| Network interruption — full estate | INFRASTRUCTURE-BLOCKED | Requires authorized control of deployed network paths. |
| Application/machine restart — Hello | SCRIPTED | Hello lifecycle scripts verify durable InstallationId/registration behavior; machine-level restart requires deployed host access. |

## Recovery rule

A repair action is not considered proof of recovery. Recovery must be established by fresh post-repair evidence: a new health observation, successful registration authentication, drained durable queue, successful synchronization, or equivalent authoritative evidence.

## Production-only checks still required

The following must be executed against authorized non-production/controlled production infrastructure before declaring the entire estate fully proven:

- Hello destructive registration lifecycle matrix.
- Registration PostgreSQL backup/restore test.
- Cafeteria POS offline → restart → reconnect → queue drain.
- Request Portal FFP Manager downstream outage → retry → recovery.
- Request Portal real OIDC callback/authorization failures.
- Studio NAS/storage disappearance and recovery.
- Studio real provider-branch failure and delivery-receipt behavior.
- Central Operations/Diagnostics/Configuration outage while business applications continue.
