# Config Key Environment Asymmetry Audit — 2026-06-11

**Trigger**: post-#2116 fix code review (PR #2159) → follow-up "audit other config keys for similar lookup/write asymmetry patterns".

**Scope**: all call sites of `IConfigurationService.GetConfigurationByKeyAsync` (the GET side) paired with `CreateConfigurationCommand` (the CREATE side). Look for handlers that read with current-environment fallback but write with a hardcoded environment string — the exact pattern that produced the `23505` duplicate-key 500 on `PUT /api/v1/admin/settings/registration-mode`.

## TL;DR

- **1 HIGH-severity finding** (same bug pattern as #2116, not yet reported as user-facing 500):
  - `FeatureFlagService.cs` — 4 methods (`EnableFeatureAsync`, `DisableFeatureAsync`, `EnableFeatureForTierAsync`, `DisableFeatureForTierAsync`) read with `_configService.GetConfigurationByKeyAsync(key)` (null env → current-env fallback) and write with `Environment: "Production"` hardcoded.
- **0 other findings**. The 6 `Update*LimitsCommandHandler` instances use `Environment = "All"` (constant), which is intentional `(Key, "All")` wildcard rows; the `FeatureFlagEndpoints.cs` POST resolves `request.Environment ?? "All"`; `ConfigurationEndpoints.cs` propagates `request.Environment` (user-provided).

## Method

```bash
# 1. All GET-side calls
grep -rn "GetConfigurationByKeyAsync" apps/api/src --include="*.cs"

# 2. All CREATE-side calls
grep -rn "new CreateConfigurationCommand" apps/api/src --include="*.cs"

# 3. For every CREATE call, inspect the `Environment` named argument:
#    - literal "Production" → suspect
#    - literal "All"        → wildcard, intentional
#    - variable/parameter   → trace source
```

## Findings

### HIGH-1 — `FeatureFlagService` 4-method asymmetry

| Method | GET (line) | CREATE (line) | GET env | CREATE env |
|---|---|---|---|---|
| `EnableFeatureAsync` | 181 | 206–214 | null → `_environment.EnvironmentName` (via `ConfigurationService.cs:79`) | `"Production"` literal |
| `DisableFeatureAsync` | 231 | 256–264 | same | `"Production"` literal |
| `EnableFeatureForTierAsync` | 282 | 307–315 | same | `"Production"` literal |
| `DisableFeatureForTierAsync` | 333 | 358–366 | same | `"Production"` literal |

**Reproduces the #2116 pattern verbatim**: in any non-Production environment (Development, Staging), the GET returns null on a fresh DB, the CREATE inserts `(Key, Environment="Production")` — and if a `"Production"` seed row already exists for that feature flag, the INSERT collides with `IX_system_configurations_Key_Environment`.

**Why not yet a P0 user-facing 500?**
- These four methods are not exposed via a public admin endpoint that toggles feature flags one at a time. The user-facing toggle path goes through `FeatureFlagEndpoints.cs` (which already uses `request.Environment ?? "All"` correctly).
- These four methods are used by **internal callers** (programmatic flag mutations, likely from seed/migration code or admin tooling). The 23505 has not been reported because the code path may rarely hit a pre-existing "Production" row in dev/staging — but the design fault is present.

**Recommended fix** (same as #2116):
1. Inject `IWebHostEnvironment` into `FeatureFlagService` constructor.
2. Replace `Environment: "Production"` in all four `CreateConfigurationCommand` blocks with `_environment.EnvironmentName`.
3. Pass `_environment.EnvironmentName` explicitly to the four `GetConfigurationByKeyAsync(key)` calls (currently null → relies on internal fallback; explicit makes the symmetry self-evident).
4. Existing `FeatureFlagServiceTests.cs` needs `IWebHostEnvironment` mock (additive — no test rewrite required).

**Severity rationale**: same root cause as a P0, but no current user-facing exploit path identified → classify as **P1**.

## Non-findings (verified safe)

### "All" wildcard handlers (6× — intentional, symmetric)

These 6 handlers use the `Environment = "All"` (or `EnvironmentValue = "All"`) class-level constant in BOTH the implicit `Update*Command` write path AND the matched read path that knows how to resolve "All" via `ConfigurationRepository.GetByKeyAsync` (per the reviewer's note in PR #2159 — the SQL `WHERE` clause supports the "All" wildcard).

| Handler | Constant |
|---|---|
| `UpdateChatHistoryLimitsCommandHandler.cs:21` | `Environment = "All"` |
| `UpdateGameLibraryLimitsCommandHandler.cs:24` | `Environment = "All"` |
| `UpdatePdfLimitsCommandHandler.cs:23` | `EnvironmentValue = "All"` |
| `UpdatePdfTierUploadLimitsCommandHandler.cs:29` | `Environment = "All"` |
| `UpdatePdfUploadLimitsCommandHandler.cs:25` | `Environment = "All"` |
| `UpdateSessionLimitsCommandHandler.cs:24` | `Environment = "All"` |

These are NOT affected — the "All" string means "env-agnostic config row" and the read side handles the wildcard.

### Variable-source environment (2× — intentional, user-controlled)

| Call site | Source |
|---|---|
| `FeatureFlagEndpoints.cs:191–200` | `request.Environment ?? "All"` (admin caller controls env) |
| `ConfigurationEndpoints.cs:176–184` | `request.Environment` (admin caller controls env) |

These are admin endpoints where the caller explicitly chooses the target environment. No symmetry violation.

### Query handlers reading current-env config (12× — read-only, no write path)

All `Get*LimitsQueryHandler` files in `BoundedContexts/SystemConfiguration/Application/Queries/` call `GetConfigurationByKeyAsync(key, null, ct)` to read a config value. They never write. No symmetry to violate.

## Recommendations

1. **Track HIGH-1 as a separate P1 issue** with the same playbook as #2116:
   - Failing unit test that asserts `CreateConfigurationCommand.Environment == "Development"` (or current env, not `"Production"`) — RED before fix, GREEN after.
   - Inject `IWebHostEnvironment` constructor dep.
   - Apply 4× line edit.
   - Add `IWebHostEnvironment` mock to existing `FeatureFlagServiceTests` setup.

2. **Architectural follow-up (P3, design discussion)**: standardise the "what does `Environment` mean for this Key?" decision tree. Today the codebase mixes three idioms:
   - **`"All"` wildcard** — env-agnostic config, used by `Update*LimitsCommandHandler`.
   - **Caller-provided** — used by `ConfigurationEndpoints` and `FeatureFlagEndpoints`.
   - **Current-env per-row** — used by `SetRegistrationMode` (post-#2116) and (incorrectly) by `FeatureFlagService` literal `"Production"`.

   Without a contract, future handlers will pick at random and the next 23505 is a matter of time. Either:
   - Document a convention (e.g. "global flags → `"All"`; per-env settings → current env"); or
   - Refactor `ConfigurationRepository.GetByKeyAsync` so that the "All" wildcard absorbs concrete env hits (then concrete env writes become equivalent to "All" reads, removing the asymmetry surface entirely).

   This is a docs / design RFC task, not a bugfix.

3. **Test-side**: when the IT Testcontainers test for `/admin/settings/registration-mode` lands (sibling follow-up), consider adding one IT that exercises `FeatureFlagService.EnableFeatureAsync` against a real DB with a seed `"Production"` row. Would catch HIGH-1 end-to-end.

## Out of scope

- The HIGH-1 fix itself — tracked as separate issue.
- Migration / data backfill of orphan `"Production"` rows produced by past calls of the four `FeatureFlagService` methods on dev/staging databases. If HIGH-1 is fixed and there are orphan rows, ops would need to either reconcile or accept the same "soft reset" semantics noted in PR #2159 for #2116.
