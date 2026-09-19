# Aegis Operational Runbook Convention

Operational applications should maintain a runbook that is specific to the application while following a common discoverable structure.

Recommended sections, where applicable:

1. Purpose
2. Dependencies
3. Startup
4. Shutdown
5. Configuration
6. Registration
7. Secrets
8. Health Verification
9. Expected Diagnostics
10. Normal State
11. Common Warnings
12. Common Failures
13. Recovery
14. Backup
15. Restore
16. Deployment
17. Rollback
18. Purge
19. Re-registration
20. Escalation
21. Log Locations

## Rules

- Registration and diagnostics should carry a reference to the runbook, not copy the entire document into registration records.
- Runbooks may contain application-specific operational commands; Common.Registration must not.
- Do not place passwords, bearer tokens, registration credentials, secret values, sensitive connection strings, cookies, or one-time claim tokens in a runbook.
- Describe expected healthy, degraded, offline, warning, failed, unsupported and unknown states truthfully where those distinctions apply.
- Recovery instructions should preserve durable queues, registration identity and other recoverable state unless an explicitly authorized destructive procedure requires otherwise.
- Destructive operations such as purge must be clearly identified as destructive and separated from ordinary recovery.
