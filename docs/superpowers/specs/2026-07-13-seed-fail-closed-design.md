# Design — Seed profile fail-closed default (#2893)

**Date**: 2026-07-13
**Issue**: [#2893](https://github.com/meepleAi-app/meepleai-monorepo/issues/2893) — `fix(seed): production seed profile falls back to Dev default (fail-open)`
**Labels**: bug · area/security · area/backend · P1
**Branch**: `feature/issue-2893-seed-fail-closed` (parent `main-dev`)

## Problem

At first boot the API resolves its seed profile in `SeedOrchestrator.ResolveProfile`
(`apps/api/src/Api/Infrastructure/Seeders/SeedOrchestrator.cs:85-98`) with this precedence:

```
env SEED_PROFILE  →  config Seeding:Profile  →  DEFAULT = SeedProfile.Dev
```

`Dev` is the **most permissive** profile (enum ordinal `None=0, Prod=1, Staging=2, Dev=3`; a
layer runs when `MinimumProfile <= profile`). The hardcoded default is therefore **fail-open**:
if neither signal is set, the system seeds the maximum dataset.

Nothing in the versioned repo sets `SEED_PROFILE` (or `Seeding:Profile`) for production:

| Location | Fact (verified) |
|---|---|
| `SeedOrchestrator.cs:98` | `return SeedProfile.Dev;` — fail-open default |
| `infra/compose.staging.yml:66` | `SEED_PROFILE: Staging` — the **only** place it is set repo-wide |
| `infra/compose.prod.yml` (api `environment:`) | sets `ASPNETCORE_ENVIRONMENT: Production`, **no** `SEED_PROFILE` |
| `infra/compose.dev.yml:53` | sets `ASPNETCORE_ENVIRONMENT: Development`, no `SEED_PROFILE` (relies on the `Dev` code default) |
| `appsettings.json` / `appsettings.Production.json` | `Seeding` section has no `Profile` key — the config tier is never populated |
| `Dockerfile` | no `ENV SEED_PROFILE` |
| `infra/scripts/load-secrets-env.sh` | does not inject `SEED_PROFILE` |
| `scripts/deployment/deploy-meepleai.ps1:38-43` | overlays `docker-compose.yml + compose.prod.yml + compose.meepleai.yml` |
| `compose.meepleai.yml` | **does not exist** anywhere (glob + live server checkout both confirm absent) |

Consequence of the `Dev` fallback in production: `CatalogSeedLayer` (MinimumProfile=Staging) and
`LivedInSeedLayer` (MinimumProfile=Dev) run, seeding the `dev.yml` catalog (incl. the synthetic
`Nanolith` game), the Badsworm demo persona, and synthetic lived-in data.

**Severity is latent/preventive** (per the issue owner's live verification 2026-07-13): the known
host runs staging only; there is no live production deploy leaking data today. The fix must land
**before go-live** so the default protects production from day zero.

## Blast radius of the fix

Under a `Prod` profile only `CoreSeedLayer` (MinimumProfile=Prod) runs — admin user, AI models,
feature flags, badges, tier definitions: the minimum viable production seed. `CatalogSeedLayer`
and `LivedInSeedLayer` do **not** run at all, so no `dev.yml` catalog, no `Nanolith`, no Badsworm
persona, no lived-in data. Deriving `Prod` therefore fully closes the synthetic-data leak.

## Decisions

1. **Fail-closed strategy** — add a third resolution tier that derives the profile from
   `ASPNETCORE_ENVIRONMENT`, with `None` as the ultimate fail-closed fallback. Chosen over a strict
   `None` default or a `Prod` default because it fixes production automatically (prod already sets
   `ASPNETCORE_ENVIRONMENT=Production`) **and** preserves local dev (`Development → Dev`) with no
   compose changes required, while still failing closed on any unrecognized environment.

2. **Compose/deploy wiring** — add explicit `SEED_PROFILE: Prod` to `compose.prod.yml` (defense in
   depth on top of the code fix) and remove the phantom `-f compose.meepleai.yml` from the deploy
   script (commented, not deleted, for easy restoration).

## Design

### 1. `SeedOrchestrator.ResolveProfile`

The signature gains two optional parameters — `environmentName` (so the derive branch is
unit-testable without mutating the process-global `ASPNETCORE_ENVIRONMENT`) and `logger` (so the
static method can warn on misconfiguration; `SeedOrchestrator` already holds
`ILogger<SeedOrchestrator> _logger`, and `RunAsync` will thread it through). `ILogger` /
`Microsoft.Extensions.Logging` is already imported by the file.

```csharp
internal static SeedProfile ResolveProfile(
    IConfiguration? configuration,
    string? environmentName = null,
    ILogger? logger = null)
{
    // 1. Environment variable takes priority (explicit override)
    var envVar = Environment.GetEnvironmentVariable("SEED_PROFILE");
    if (!string.IsNullOrWhiteSpace(envVar))
    {
        if (Enum.TryParse<SeedProfile>(envVar, ignoreCase: true, out var envProfile))
            return envProfile;
        logger?.LogWarning("SEED_PROFILE='{Value}' is not a valid seed profile; ignoring.", envVar);
    }

    // 2. Configuration section (explicit override)
    var configValue = configuration?["Seeding:Profile"];
    if (!string.IsNullOrWhiteSpace(configValue))
    {
        if (Enum.TryParse<SeedProfile>(configValue, ignoreCase: true, out var cfgProfile))
            return cfgProfile;
        logger?.LogWarning("Seeding:Profile='{Value}' is not a valid seed profile; ignoring.", configValue);
    }

    // 3. Derive from ASPNETCORE_ENVIRONMENT (fail-closed)
    var env = environmentName ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    var derived = env?.Trim().ToLowerInvariant() switch
    {
        "production"  => SeedProfile.Prod,
        "staging"     => SeedProfile.Staging,
        "development" => SeedProfile.Dev,
        _             => SeedProfile.None,   // null / "Test" / CI / unknown → fail-closed
    };

    if (derived == SeedProfile.None)
        logger?.LogWarning(
            "Seed profile unresolved (SEED_PROFILE and Seeding:Profile unset; ASPNETCORE_ENVIRONMENT='{Env}' unrecognized). "
            + "Seeding with profile None (no data). Set SEED_PROFILE explicitly.", env ?? "(null)");

    return derived;
}
```

Notes:
- The startup/admin caller becomes `ResolveProfile(_configuration, logger: _logger)` (`environmentName`
  null → reads the env var). In production `ASPNETCORE_ENVIRONMENT=Production` → `Prod`.
- `RunAsync` already handles `profile == SeedProfile.None` (`SeedOrchestrator.cs:37-41`: logs
  `Seed profile is None — skipping all seeding` and returns). That branch is **preserved** — the new
  `LogWarning` inside `ResolveProfile` explains *why* the profile is `None` (misconfig) whereas an
  explicit `SEED_PROFILE=None` resolves at tier 1 and logs nothing (intentional, no warning).
- Existing tests call `ResolveProfile(null)` / `ResolveProfile(config)`; the new optional params keep
  them compiling. Logging assertions are not required — passing `logger: null` is the default.

### 2. Observability

- **Derive → `None`**: `Warning` naming the unrecognized environment. `None` seeds nothing — for a
  fresh production DB that means no admin user, so this must be loud.
- **Invalid explicit value** (previously a silent fallthrough — flagged in the issue's risk list):
  when `SEED_PROFILE` or `Seeding:Profile` is set but does not parse, emit a `Warning` with the
  received value instead of silently ignoring it. **In scope** per user decision.

### 3. Compose / deploy wiring

- `infra/compose.prod.yml` — add `SEED_PROFILE: Prod` to the api `environment:` block (next to
  `ASPNETCORE_ENVIRONMENT: Production`). Explicit belt-and-suspenders: production seeds `Prod` even
  if the derive logic is later changed or `ASPNETCORE_ENVIRONMENT` is overridden.
- `scripts/deployment/deploy-meepleai.ps1` — remove `-f compose.meepleai.yml` from `$ComposeFiles`,
  **commented out** with a note explaining the file never existed and prod profile is now wired via
  `compose.prod.yml` + the code derive. Commented (not deleted) so it is trivial to restore if a
  real prod overlay is introduced.

### 4. Config surface

`Seeding:Profile` remains a valid override tier but is **not** populated in any appsettings (YAGNI).
No appsettings changes.

### 5. Testing (TDD)

Extend the existing unit file `apps/api/tests/Api.Tests/Infrastructure/Seeders/SeedOrchestratorTests.cs`
(xUnit v3, FluentAssertions, Moq, `[Trait("Category","Unit")]`, `[Collection("EnvironmentVariableTests")]`,
try/finally env-var cleanup pattern). RED → GREEN order:

1. **Invert** `ResolveProfile_DefaultsToDev_WhenNothingConfigured`: with no `SEED_PROFILE`, no
   config, and an unrecognized/empty environment the result is now `SeedProfile.None`; add a case
   that `ResolveProfile(null, "Production")` ⇒ `Prod`.
2. `[Theory]` for the derive branch (via the explicit `environmentName` param — zero env pollution):
   `Production→Prod`, `Staging→Staging`, `Development→Dev`, `"Test"→None`, `null→None`, `""→None`,
   `"garbage"→None`. Case-insensitivity: `"production"→Prod`.
3. **Precedence** holds over derive: `SEED_PROFILE=Staging` + `environmentName="Production"` ⇒
   `Staging`; same for `Seeding:Profile` via in-memory config.
4. **Invalid explicit value** falls through to derive: `SEED_PROFILE="Prdo"` + `environmentName="Production"`
   ⇒ `Prod` (not the invalid value).
5. `FilterLayers_NoneProfile_ReturnsEmpty` already exists → confirms `None` runs zero layers.

## Risks

- **Deploy-script reference removal** (accepted): if an out-of-repo production overlay
  `compose.meepleai.yml` really exists and carries critical overrides, removing the `-f` would
  change that deploy. Mitigation: the code fix makes production safe regardless of the overlay, and
  the line is commented for quick restoration. The known host runs staging only, so prod deploy
  topology cannot be observed — this is a deliberate, reversible call.
- **Integration-test regression** (verification gate): the change alters seeding for any host where
  `ASPNETCORE_ENVIRONMENT ∉ {Production, Staging, Development}`. Integration tests that boot the app
  and rely on the implicit `Dev` default could now receive `None`. `WebApplicationFactory` defaults
  to `Development` (→ `Dev`, unchanged) but this must be **verified empirically by running the full
  backend suite**, not assumed. Any fixture that depended on the old default gets an explicit
  `SEED_PROFILE` set.

## Out of scope

- `CoreSeedLayer` seeds test/badsworm users when `SEED_TEST_PASSWORD` / `SEED_BADSWORM_PASSWORD`
  secrets are present. That concerns the *content* of the Core layer, not the fail-open default —
  tracked separately, not part of #2893.

## Acceptance criteria

- [ ] `ResolveProfile` derives from `ASPNETCORE_ENVIRONMENT` with `None` fail-closed fallback; the
      two explicit override tiers are unchanged.
- [ ] Invalid `SEED_PROFILE` / `Seeding:Profile` values log a `Warning` instead of silent fallthrough.
- [ ] Unresolved profile (`None`) logs a `Warning`.
- [ ] `compose.prod.yml` sets `SEED_PROFILE: Prod`; `deploy-meepleai.ps1` no longer references the
      non-existent `compose.meepleai.yml` (commented out).
- [ ] Unit tests in `SeedOrchestratorTests.cs` cover: derive matrix, precedence over derive, invalid
      value fallthrough, `None` runs zero layers. Full backend suite green (integration regressions
      addressed).
