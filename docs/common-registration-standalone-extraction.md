# Common.Registration standalone repository extraction

## Goal

Move Common.Registration out of the temporary `Common.Diagnostics` staging branch into a standalone repository without changing the registration contract or downstream application behavior.

The extraction is a source-location change, not a redesign.

## Final repository

Repository name: `Common.Registration`

Target sibling layout used by Aegis application repositories:

```text
Common/
  Common.Diagnostics/
  Common.Messaging/
  Common.Registration/
  Common.Secrets/
  Common.Security/
  Common.Storage/
```

Downstream projects should reference:

```text
$(CommonRoot)/Common.Registration/Common.Registration.csproj
```

## Source ownership

Move these files to the standalone repository unchanged except for repository-level documentation/CI paths:

- `Common.Registration.csproj`
- `RegistrationContracts.cs`

The standalone repository owns the `Common.Registration` namespace and assembly.

`Common.Diagnostics` must not compile, package, or otherwise own these registration types after extraction.

## Required tests

The standalone repository must have executable tests that retain the current proven behavior:

1. `ConfigurationContractPolicy.MetadataOnly` strips value-bearing requirement fields.
2. Metadata such as keys, `required`, `isConfigured`, `hasDefault`, and source/state evidence is preserved.
3. Sanitization clones rather than mutates the source contract.
4. `ConfigurationRegistrar.RegisterContractAsync` sends the sanitized payload.
5. Non-success HTTP responses are failures.
6. Timeout and transport failures are failures.
7. A successful result is returned only after Configuration accepts the request.

The existing staging tests are evidence for the contract but should move to a test project owned by the standalone repository rather than remain coupled to Common.Diagnostics.

## Contract rules that must not change during extraction

- Applications register only with Aegis.Configuration.
- Aegis.Diagnostics is not a registration target.
- Aegis.Configuration receives configuration requirement metadata/configured-state evidence, never the values themselves.
- No environment-variable value, appsettings value, code default content, secret, connection string, resolved value, or display value derived from configuration may cross the registration boundary.
- Registration failure must never be logged or reported as success.
- An unavailable Configuration service may leave an application operational when the application is designed for offline tolerance; registration retries remain the application's responsibility.

## Repository creation sequence

1. Create repository `Common.Registration`.
2. Copy the registration project and source files from the proven staging branch without semantic changes.
3. Add a dedicated test project and port the current registration tests.
4. Add CI for restore, build, and test on the supported .NET version.
5. Require the standalone repository CI to be green before changing any downstream checkout/reference.

## Downstream cutover sequence

Change one downstream repository at a time.

Recommended order:

1. Ebolito — current downstream migration already has a green executable CI run and is the best canary.
2. Aegis.Cafeteria.
3. RequestPortal.
4. Aegis.Studio.
5. Aegis.SensorNetwork.
6. Any additional Aegis application that adopts Common.Registration later.

For each repository:

1. Replace workflow staging/checkouts that source Common.Registration from `Common.Diagnostics@common-registration` with the standalone `Common.Registration` repository.
2. Keep the sibling path expected by `$(CommonRoot)` unchanged.
3. Do not alter application registration logic in the same commit unless a real incompatibility is discovered.
4. Run restore, build, and tests.
5. Verify application-specific registration truth audit still holds.
6. Keep the migration draft if executable CI did not run.

## Completion gate

The staging copy in Common.Diagnostics may be removed only after:

- standalone Common.Registration CI is green;
- every currently migrated downstream repository references the standalone repository;
- at least the Ebolito canary has a green downstream build/test against the standalone repository;
- no active migration workflow still checks out `Common.Diagnostics@common-registration` for the registration project.

After those conditions are met, remove from Common.Diagnostics:

- `Common.Registration.csproj`
- `RegistrationContracts.cs`
- registration-only test references/files that have moved to the standalone repository
- temporary staging documentation/workflow adjustments

## Rollback

If a downstream repository fails specifically because of the standalone extraction, restore that downstream repository's previous staging checkout/reference while the issue is corrected. Do not weaken metadata sanitization, failure semantics, or the Configuration-only ownership rule as a rollback mechanism.

## Not part of this extraction

This work does not redesign Aegis.Configuration, Aegis.Diagnostics, Aegis.Operations, Common.Diagnostics, Common.Secrets, or downstream application configuration ownership. It only gives the already-proven Common.Registration contract its permanent repository boundary.