# ADR-062 — `SystemConfigurationEntity.Environment` Field Semantics

**Status**: Proposed
**Date**: 2026-06-12
**Deciders**: Pending (Product Owner / Tech Lead review)
**Tracking**: discovered during audit follow-up of [#2116](https://github.com/meepleAi-app/meepleai-monorepo/issues/2116) — see `docs/for-developers/audits/2026-06-11-config-key-environment-asymmetry-audit.md` § Recommendations item 2.
**Related**: [#2116 closed](https://github.com/meepleAi-app/meepleai-monorepo/issues/2116) ([PR #2159](https://github.com/meepleAi-app/meepleai-monorepo/pull/2159)) · [#2162 closed](https://github.com/meepleAi-app/meepleai-monorepo/issues/2162) ([PR #2261](https://github.com/meepleAi-app/meepleai-monorepo/pull/2261)) · [PR #2163](https://github.com/meepleAi-app/meepleai-monorepo/pull/2163) (IT testcontainers + audit) · [PR #2267](https://github.com/meepleAi-app/meepleai-monorepo/pull/2267) (test infra refactor).

## Context

The `SystemConfigurationEntity` table (`system_configurations`) stores runtime configuration values keyed by `(Key, Environment)` with a UNIQUE constraint `IX_system_configurations_Key_Environment`. The `Environment` column is `VARCHAR(50) NOT NULL`. In production today, three distinct idioms populate it:

### Idiom 1 — `"All"` wildcard (env-agnostic config)

Used by 6 `Update*LimitsCommandHandler` files as a private const:

```csharp
// apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Commands/UpdatePdfUploadLimitsCommandHandler.cs:25
private const string Environment = "All";
```

| File | Line |
|---|---|
| `UpdateChatHistoryLimitsCommandHandler.cs` | 21 |
| `UpdateGameLibraryLimitsCommandHandler.cs` | 24 |
| `UpdatePdfLimitsCommandHandler.cs` | 23 (named `EnvironmentValue`) |
| `UpdatePdfTierUploadLimitsCommandHandler.cs` | 29 |
| `UpdatePdfUploadLimitsCommandHandler.cs` | 25 |
| `UpdateSessionLimitsCommandHandler.cs` | 24 |

**Read-side support**: `ConfigurationRepository.GetByKeyAsync` (`apps/api/src/Api/BoundedContexts/SystemConfiguration/Infrastructure/Persistence/ConfigurationRepository.cs:49-78`) treats `"All"` as an explicit wildcard:

```csharp
// Filter: Match exact environment OR "All"
query = query.Where(c => c.Environment == environment || c.Environment == "All");

// Order: Prioritize environment-specific (0) over "All" (1)
query = query
    .OrderBy(c => c.Environment == environment ? 0 : 1)
    .ThenByDescending(c => c.UpdatedAt)
    .ThenByDescending(c => c.Version);
```

Semantics: **one canonical row, valid in any environment, optionally overridable per environment**. A lookup for `("PdfUpload:MaxFileSizeBytes", "Development")` returns the `"All"` row if no `"Development"` override exists; if both exist, the `"Development"` row wins via the `OrderBy`. This is the design intent the repository was built for.

### Idiom 2 — Caller-provided (admin chooses target env)

Used by 2 admin endpoints that propagate the value from a request DTO:

```csharp
// apps/api/src/Api/Routing/FeatureFlagEndpoints.cs:191
var environment = request.Environment ?? "All";
// ... then passed to CreateConfigurationCommand
```

| File | Line | Default |
|---|---|---|
| `ConfigurationEndpoints.cs` | 176-184 | from `CreateConfigurationRequest.Environment` — DTO default is `"All"` (`Api/Models/Contracts.cs:688`) |
| `FeatureFlagEndpoints.cs` | 191-202 | `request.Environment ?? "All"` |

Semantics: **admin caller declares the target environment explicitly**. Used for A/B testing, staging-only flags, or any flag intentionally scoped per environment.

> **Note**: when the caller omits the `Environment` field in the request DTO, both paths above silently fall back to `"All"`. This means **Idiom 2 with a missing field is functionally equivalent to Idiom 1**. The distinction matters only when the admin actively supplies a concrete environment name (e.g., `"Staging"` for a staging-only experiment).

### Idiom 3 — Current-env per-row (introduced by recent bugfixes)

Used by 2 services post-bugfix:

```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/SetRegistrationModeCommand.cs:31 (post-#2116)
var environmentName = _environment.EnvironmentName;
// ... then passed to GetConfigurationByKeyAsync + CreateConfigurationCommand
```

| File | Lines | Source |
|---|---|---|
| `SetRegistrationModeCommandHandler` (in `SetRegistrationModeCommand.cs`) | 31, 33, 53 | `_environment.EnvironmentName` |
| `FeatureFlagService` (4 methods: `EnableFeatureAsync`, `DisableFeatureAsync`, `EnableFeatureForTierAsync`, `DisableFeatureForTierAsync`) | 189-191, 222, 240-242, 273, 291-293, 325, 344-346, 377 | `_environment.EnvironmentName` |

Semantics: **one row per environment for the same Key**. A Development DB and a Staging DB hold separate rows for `Registration:PublicEnabled`. Cross-env consistency is not maintained automatically.

## Problem

The three idioms coexist without an explicit convention. The empirical consequences observed in the last 30 days:

1. **Issue [#2116](https://github.com/meepleAi-app/meepleai-monorepo/issues/2116)** (P0 user-facing 500): `SetRegistrationMode` hardcoded `Environment="Production"` on the write path but read from current env on the lookup path → 23505 duplicate-key collisions in any non-Production deploy. Fix shipped via [PR #2159](https://github.com/meepleAi-app/meepleai-monorepo/pull/2159) chose **Idiom 3** (current-env per-row), which solves the immediate 500 but introduces orphan rows when the seed value already exists at `"Production"`.

2. **Issue [#2162](https://github.com/meepleAi-app/meepleai-monorepo/issues/2162)** (P1, same pattern, hidden): `FeatureFlagService` had the same lookup/write asymmetry on 4 methods, latent until exercised. Fix shipped via [PR #2261](https://github.com/meepleAi-app/meepleai-monorepo/pull/2261) also chose **Idiom 3**.

3. **Latent ambiguity**: every new handler that persists configuration must currently decide which idiom to use **without guidance**. Two of the recent fixes picked Idiom 3 even when the config (`Registration:PublicEnabled`, feature flags) is conceptually global. The next handler may pick differently, creating a new 23505 surface.

The deeper observation is that the `ConfigurationRepository` already correctly implements the `"All"` + override pattern (Idiom 1 + selective Idiom 2). The recent bugfixes (Idiom 3) bypass the repository's design intent rather than aligning with it.

## Discussion (spec-panel synthesis)

A panel discussion across architectural, production-reliability, and service-boundary perspectives produced the following convergent insights:

**Architecture (Fowler-style)**: the repository `GetByKeyAsync` is already a well-designed lookup that supports both env-agnostic defaults and per-env overrides. The bug in #2116 was not a design failure of the repository — it was a code-side misuse: writing under a hardcoded `"Production"` for a flag that is conceptually env-agnostic. The current-env per-row idiom is a workaround, not a design improvement. Aligning with the repository's intent is the cleaner path.

**Production reliability (Nygard-style)**: the per-env per-row idiom guarantees that read and write always agree (no 23505), but at the cost of cross-env drift (the Production seed row becomes orphan when Development writes its own row, as documented in the [post-#2116 ops smoke checklist](../../../for-developers/operations/2026-06-11-issue-2116-post-deploy-smoke.md)). The `"All"` + override pattern eliminates orphan rows: there is at most one canonical row per Key, and any environment-specific divergence is an explicit override.

**Service boundaries (Newman-style)**: distinguishing the three idioms by intent — not by accident — makes the architecture self-documenting. Caller-provided remains the right choice when an admin actively scopes a flag per env (A/B testing, staging-only experiments). Current-env per-row is justified for values that genuinely diverge by environment design (e.g., env-specific external API endpoints), not for global feature flags.

## Decision (Proposed)

Adopt **Option A**: `"All"` wildcard is the default for env-agnostic config; per-env rows are an explicit opt-in for values that diverge by environment design; caller-provided remains the appropriate idiom for admin-scoped flags.

### Decision tree for new config keys

When adding a new key persisted via `CreateConfigurationCommand`, answer in order:

1. **Does the admin caller explicitly choose the target environment in the request?**
   - **Yes** → use **Idiom 2 (caller-provided)**. Take the value from the request DTO; default to `"All"` if the request leaves it null.
   - **No** → continue to question 2.

2. **Does the value need to diverge between environments by design?** (e.g. external API base URL `https://staging.api.example.com` vs `https://api.example.com`; env-specific signing key)
   - **Yes** → use **Idiom 3 (current-env per-row)**. Inject `IWebHostEnvironment`, write with `_environment.EnvironmentName`, lookup with the same value. Accept that ops must seed/maintain each environment row.
   - **No** → use **Idiom 1 (`"All"` wildcard)**. Write with `Environment = "All"`. Reads will resolve to it from any environment via the repository's wildcard fallback. Add a `private const string Environment = "All";` to make the choice explicit.

3. **Default**: when in doubt, prefer **Idiom 1**. Per-env divergence can be added later as an override row without code change.

### Concrete reclassification

Applying the decision tree to the two recent bugfixes:

| Config key | Current idiom (post-bugfix) | Recommended idiom | Rationale |
|---|---|---|---|
| `Registration:PublicEnabled` | Idiom 3 (per-env) | **Idiom 1 (`"All"`)** | A global feature flag; the toggle is a tenant-wide policy decision, not per-env scoping. Admin UI does not expose an env selector. |
| `<FeatureFlag>` (4 `FeatureFlagService` paths, including role-keyed `<FeatureFlag>.<Role>` and tier-keyed `<FeatureFlag>.Tier.<TierName>` variants) | Idiom 3 (per-env) | **Idiom 1 (`"All"`)** | Same rationale — these are tenant-wide kill switches, not env-scoped experiments. Role/tier scoping is encoded in the Key itself (`<FeatureFlag>.Admin`, `<FeatureFlag>.Tier.premium`), independent of the Environment column. Admin endpoint at `FeatureFlagEndpoints.cs` remains the right surface for per-env overrides when intentionally needed. |

The 6 `Update*LimitsCommandHandler` files already use Idiom 1 correctly. The 2 endpoint files at `ConfigurationEndpoints.cs` / `FeatureFlagEndpoints.cs` already use Idiom 2 correctly.

## Alternatives Considered

### Option B — current-env per-row as default

Make Idiom 3 the universal default; reserve `"All"` only for cross-env constants.

**Pros**: read and write are always symmetric (the fix path chosen by #2116 and #2162). No 23505 surface from asymmetric writes.

**Cons**:
- **Orphan rows**: every deploy to a new environment writes a new row; pre-existing rows in other envs become unreachable. Ops must manually reconcile (see the post-#2116 smoke checklist).
- **Cross-env drift**: a flag enabled in Staging is NOT enabled in Production unless the admin actively toggles it in both places.
- **Bypasses repository design**: the repository's `OR Environment == "All"` clause becomes dead code for all but a handful of historical rows.
- **Doubles seed work**: every new environment needs a fresh seed for every key, instead of inheriting the `"All"` default.

Rejected because the orphan row pattern is the dominant source of the operational complexity that #2116 / #2162 introduced.

### Option C — refactor repository to absorb concrete env into `"All"` lookup

Modify `GetByKeyAsync` so that a write with `Environment="Production"` also matches lookups for any other environment when no override exists. Removes the asymmetry by design.

**Pros**: no application-side decision required; the repository absorbs the difference invisibly.

**Cons**:
- **Breaks per-env override semantics**: today, `Environment="Production"` means "this row applies ONLY to Production". Changing that to "applies everywhere unless overridden" is a silent semantic change that would affect every row currently persisted under a concrete env name (including all rows produced by Idiom 3 since #2116 and #2162).
- **Read ambiguity**: when both `"Production"` and `"Development"` rows exist, which wins for a lookup at `"Staging"`? The ordering becomes arbitrary.
- **Migration is irreversible**: once concrete env names are reinterpreted as wildcards, the original per-env semantics cannot be recovered without a schema change.

Rejected because the semantic loss is permanent and the rule "concrete env name = scoped to that env" is intuitive and worth keeping.

### Option D — make `Environment` a typed enum + add `Scope` enum

Replace the `VARCHAR(50)` column with a structured `Scope` (`Global` / `EnvSpecific`) and `Environment` (nullable, only meaningful when `Scope = EnvSpecific`).

**Pros**: the intent is encoded in the schema. Compiler enforces the choice. No "magic string" `"All"`.

**Cons**: requires a database migration, breaking change for all existing rows, and impacts every consumer of the `Environment` column. Disproportionate to the scope of the asymmetry problem. Would be a separate, larger ADR if pursued.

Rejected for this iteration as out of scope. Could be revisited if the asymmetry recurs after adopting Option A.

## Consequences

### Positive

- One canonical convention reduces the cognitive load when adding new config keys.
- Aligns application code with the repository's design intent (the `"All"` + override pattern already implemented).
- Eliminates orphan rows for the dominant class of config keys (tenant-wide flags).
- Reads always resolve to a value (via wildcard fallback), eliminating the "config not found in this env" failure mode.

### Negative

- The two recent bugfixes (#2116 fix in PR #2159, #2162 fix in PR #2261) are now misaligned with the proposed convention. Migration required — see below.
- Existing seed rows at `Environment="Production"` for global flags need a one-time correction (UPDATE to `"All"`) to enable cross-env read fallback. Otherwise, lookups from non-Production environments will continue to miss the seed value.
- The decision tree adds a small upfront cognitive load on the next handler author. Mitigation: link this ADR from `CLAUDE.md` so it surfaces during code review.

### Neutral

- The `Environment` column remains `VARCHAR(50)` — no schema change required.
- Both repository code paths (`Environment == "All"` matching and exact-env matching) remain in use.

## Migration Path

If this ADR is accepted, the following changes are required across two PRs:

### PR M1 — Code reclassification (~2-3h)

1. **`SetRegistrationModeCommandHandler`**: replace `_environment.EnvironmentName` with the string literal `"All"` in both the lookup call and the `CreateConfigurationCommand` write. Remove the `IWebHostEnvironment` constructor dependency (no longer needed). Update unit tests accordingly (replace `Mock<IWebHostEnvironment>` setup with simple constant expectations).
2. **`FeatureFlagService`**: same change applied to all 4 methods (`EnableFeatureAsync`, `DisableFeatureAsync`, `EnableFeatureForTierAsync`, `DisableFeatureForTierAsync`). Remove the constructor dependency on `IWebHostEnvironment`.
3. **Tests**: update `SetRegistrationModeCommandHandlerTests` (4 unit tests asserting `c.Environment == "<current-env>"`) and `FeatureFlagServiceTests` (4 unit tests added by PR #2261) to assert `c.Environment == "All"` instead.
4. **Integration test**: update `SetRegistrationModeIntegrationTests.cs` — the existing scenarios stay valid (a `"Production"` seed row is still served to a `"Development"` lookup via the repository wildcard); the assertion that a new env-specific row gets created flips to "no new row gets created; the seed `"All"` row is updated in place".

### PR M2 — Data migration (~1h + ops coordination)

For each affected environment (Production, Staging, any dev DB that has been touched by Idiom 3 writes), run:

```sql
-- For each global config key currently stored at a concrete environment, promote to "All".
-- Repeat for: Registration:PublicEnabled, plus every feature flag key under Category = 'FeatureFlags'.

BEGIN;

-- Step 0: lock all rows for the affected keys to prevent concurrent admin toggles
-- racing with the migration. PostgreSQL 9.5+ supports FOR UPDATE inside a CTE.
WITH locked_rows AS (
    SELECT "Id"
    FROM system_configurations
    WHERE "Key" IN ('Registration:PublicEnabled')  -- extend with feature flag keys
    FOR UPDATE
)
SELECT 1 FROM locked_rows;  -- materialize the lock

-- Step 1: pick the most recent row per Key (in case multiple env-specific rows exist post-bugfix)
-- and promote it to "All". This becomes the canonical row.
WITH most_recent AS (
    SELECT DISTINCT ON ("Key") "Id"
    FROM system_configurations
    WHERE "Key" IN ('Registration:PublicEnabled')  -- same list
    ORDER BY "Key", "UpdatedAt" DESC, "Version" DESC
)
UPDATE system_configurations sc
SET "Environment" = 'All',
    "UpdatedAt" = NOW(),
    "Version" = sc."Version" + 1
WHERE sc."Id" IN (SELECT "Id" FROM most_recent);

-- Step 2: delete the orphan rows that the bugfix created in other environments.
DELETE FROM system_configurations
WHERE "Key" IN ('Registration:PublicEnabled')  -- same list
  AND "Environment" <> 'All';

COMMIT;
```

Ops checklist:
- The `FOR UPDATE` in Step 0 guarantees serializability against concurrent admin toggles. Alternative if the PostgreSQL version does not support `FOR UPDATE` in CTEs: run during a maintenance window with admin UI temporarily disabled.
- **Sequence**: deploy PR M1 (code change) FIRST, then run PR M2 (data migration), then invalidate the HybridCache. Running M2 before M1 is harmless (no code yet relies on the `"All"` row), but the reverse order leaves a window where the code looks up `"All"` and finds nothing until M2 lands.
- Verify post-migration: `SELECT "Key", "Environment", "Value" FROM system_configurations WHERE "Key" IN (...)` returns exactly one row per Key with `Environment = 'All'`.
- The HybridCache TTL is 5 min — invalidate the cache via `POST /admin/cache/invalidate` (if exposed) or wait one TTL window after the migration.

### Rollback

PR M1 is fully reversible (revert commit). PR M2 is reversible only if M2 was idempotent and the data state pre-migration was captured (recommend `pg_dump` of the `system_configurations` table immediately before M2). Without a snapshot, rollback would require manually re-creating per-env rows.

## Why this is `Proposed`, not `Accepted`

The Product Owner / Tech Lead has not yet ratified the migration cost. The two recent bugfixes (#2159 and #2261) chose Idiom 3 as a deliberate decision at review time. Re-litigating that choice across two more PRs and a data migration deserves explicit signoff before scheduling work.

Open questions for the deciders:

1. **Does the cost of M1 + M2 outweigh the future-bug-prevention value?** The current Idiom 3 code works; the migration is a hygiene improvement, not a P0 fix.
2. **Are there any planned config keys in the next 60 days that the decision tree would route to Idiom 3?** If yes, this ADR is more valuable; if all upcoming keys are clearly Idiom 1, the existing inconsistency may simply be grandfathered.
3. **Tolerance for orphan rows on staging post-deploy?** Currently mitigated by the [post-#2116 smoke checklist](../../../for-developers/operations/2026-06-11-issue-2116-post-deploy-smoke.md). If the checklist is reliably followed, the orphan-row problem is contained.

## Implementation rule (if Accepted)

Add to `CLAUDE.md` under the existing **Known Pitfalls** section:

> **Config `Environment` field**: see [ADR-062](./docs/for-claude/architecture/adr/adr-062-config-environment-field-semantics.md). Default to `"All"` for new global config keys; per-env per-row only when the value must diverge by environment design. Decision tree in the ADR.

## References

- Audit: [`docs/for-developers/audits/2026-06-11-config-key-environment-asymmetry-audit.md`](../../../for-developers/audits/2026-06-11-config-key-environment-asymmetry-audit.md)
- Ops checklist: [`docs/for-developers/operations/2026-06-11-issue-2116-post-deploy-smoke.md`](../../../for-developers/operations/2026-06-11-issue-2116-post-deploy-smoke.md)
- Repository read logic: [`apps/api/src/Api/BoundedContexts/SystemConfiguration/Infrastructure/Persistence/ConfigurationRepository.cs:49-78`](../../../../apps/api/src/Api/BoundedContexts/SystemConfiguration/Infrastructure/Persistence/ConfigurationRepository.cs)
- Pattern reference (correct Idiom 1 usage): [`apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Commands/UpdatePdfUploadLimitsCommandHandler.cs:25`](../../../../apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Commands/UpdatePdfUploadLimitsCommandHandler.cs)
