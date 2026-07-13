# Seed Profile Fail-Closed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the API's seed profile fail *closed* — an unrecognized/absent deployment environment must never seed synthetic Dev data into production.

**Architecture:** `SeedOrchestrator.ResolveProfile` gains a third resolution tier that derives the profile from `ASPNETCORE_ENVIRONMENT` (`Production→Prod`, `Staging→Staging`, `Development→Dev`, anything else `→None`), replacing the fail-open `return SeedProfile.Dev`. The two explicit override tiers (`SEED_PROFILE` env, `Seeding:Profile` config) are unchanged and still win. Production is wired belt-and-suspenders via `compose.prod.yml`, and the phantom `compose.meepleai.yml` reference is removed from the deploy script.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs, xUnit v3, FluentAssertions, Moq, Docker Compose, PowerShell (deploy script).

**Spec:** `docs/superpowers/specs/2026-07-13-seed-fail-closed-design.md`
**Issue:** [#2893](https://github.com/meepleAi-app/meepleai-monorepo/issues/2893) (P1 · area/security · area/backend)

## Global Constraints

- Enum values are fixed: `SeedProfile { None = 0, Prod = 1, Staging = 2, Dev = 3 }`. A layer runs iff `MinimumProfile <= profile`. Do not reorder.
- Derive map (case-insensitive, trimmed): `production → Prod`, `staging → Staging`, `development → Dev`, everything else (null / `""` / `Testing` / `CI` / unknown) `→ None`.
- Precedence is strict: (1) `SEED_PROFILE` env var, (2) `Seeding:Profile` config, (3) derive-from-`ASPNETCORE_ENVIRONMENT`. Earlier tiers win.
- Invalid values in tiers 1–2 must log `Warning` and fall through (not throw, not silently swallow).
- Backend test project path: `apps/api/tests/Api.Tests` (NOT `tests/Api.Tests`).
- Test conventions: xUnit v3, FluentAssertions (`.Should().Be(...)`), `[Trait("Category", TestCategories.Unit)]`, env-var mutations wrapped in try/finally that resets to `null`, seed-profile tests live in `[Collection("EnvironmentVariableTests")]`.
- Derive-branch tests MUST pass `environmentName` explicitly — never mutate the process-global `ASPNETCORE_ENVIRONMENT` (it is read by many other components and would pollute parallel collections).
- Commit convention: `fix(seed): #2893 <subject>` / `test(seed): #2893 <subject>` / `chore(infra): #2893 <subject>`, subject ≤72 chars.
- PR base branch: `main-dev` (parent of `feature/issue-2893-seed-fail-closed`).
- Kill any orphan `Api.Tests.exe` / testhost before running `dotnet test` (DLL lock).

---

### Task 1: Fail-closed `ResolveProfile` (derive from `ASPNETCORE_ENVIRONMENT`) + unit tests

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/SeedOrchestrator.cs:85-99` (`ResolveProfile`) and `:35` (caller inside `RunAsync`)
- Test: `apps/api/tests/Api.Tests/Infrastructure/Seeders/SeedOrchestratorTests.cs`

**Interfaces:**
- Produces: `internal static SeedProfile ResolveProfile(IConfiguration? configuration, string? environmentName = null, ILogger? logger = null)` — resolution order env→config→derive(`environmentName` ?? `ASPNETCORE_ENVIRONMENT` env var); fail-closed default `SeedProfile.None`.
- Consumes: existing `SeedProfile` enum; `Microsoft.Extensions.Logging.ILogger` (already imported in `SeedOrchestrator.cs:4`).

- [ ] **Step 1: Replace the fail-open default test and add the new coverage**

In `SeedOrchestratorTests.cs`, **delete** the existing `ResolveProfile_DefaultsToDev_WhenNothingConfigured` method (lines ~52-58) and add these methods to the `SeedOrchestratorTests` class:

```csharp
[Theory]
[Trait("Category", TestCategories.Unit)]
[InlineData("Production", SeedProfile.Prod)]
[InlineData("production", SeedProfile.Prod)]
[InlineData("  Staging  ", SeedProfile.Staging)]
[InlineData("Development", SeedProfile.Dev)]
[InlineData("Testing", SeedProfile.None)]
[InlineData("CI", SeedProfile.None)]
[InlineData("", SeedProfile.None)]
[InlineData(null, SeedProfile.None)]
[InlineData("garbage", SeedProfile.None)]
public void ResolveProfile_DerivesFromAspNetCoreEnvironment_WhenNoOverride(string? environmentName, SeedProfile expected)
{
    Environment.SetEnvironmentVariable("SEED_PROFILE", null);
    try
    {
        var result = SeedOrchestrator.ResolveProfile(configuration: null, environmentName: environmentName);
        result.Should().Be(expected);
    }
    finally
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", null);
    }
}

[Fact]
[Trait("Category", TestCategories.Unit)]
public void ResolveProfile_EnvironmentVariable_TakesPrecedenceOverDerive()
{
    Environment.SetEnvironmentVariable("SEED_PROFILE", "Staging");
    try
    {
        var result = SeedOrchestrator.ResolveProfile(configuration: null, environmentName: "Production");
        result.Should().Be(SeedProfile.Staging); // explicit override beats derived Prod
    }
    finally
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", null);
    }
}

[Fact]
[Trait("Category", TestCategories.Unit)]
public void ResolveProfile_Config_TakesPrecedenceOverDerive_WhenNoEnvVar()
{
    Environment.SetEnvironmentVariable("SEED_PROFILE", null);
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Seeding:Profile"] = "Staging" })
        .Build();

    var result = SeedOrchestrator.ResolveProfile(config, environmentName: "Production");
    result.Should().Be(SeedProfile.Staging); // config override beats derived Prod
}

[Fact]
[Trait("Category", TestCategories.Unit)]
public void ResolveProfile_InvalidEnvVar_FallsThroughToDerive()
{
    Environment.SetEnvironmentVariable("SEED_PROFILE", "Prdo"); // typo → invalid enum
    try
    {
        var result = SeedOrchestrator.ResolveProfile(configuration: null, environmentName: "Production");
        result.Should().Be(SeedProfile.Prod); // invalid value ignored, derives from env
    }
    finally
    {
        Environment.SetEnvironmentVariable("SEED_PROFILE", null);
    }
}
```

Confirm the file already has `using Microsoft.Extensions.Configuration;` (for `ConfigurationBuilder`) and `using Api.Infrastructure.Seeders;`. The existing precedence tests `ResolveProfile_FromEnvironment_ParsesCorrectly` and `ResolveProfile_FromConfig_WhenNoEnvVar` stay as-is (they resolve at tiers 1/2 and never reach derive).

- [ ] **Step 2: Run the new tests to verify they FAIL to compile**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SeedOrchestratorTests" -v minimal`
Expected: FAIL — build error `CS1501` / `No overload for method 'ResolveProfile' takes 2 arguments` (the production method still has the 1-arg signature).

- [ ] **Step 3: Implement the fail-closed `ResolveProfile`**

In `apps/api/src/Api/Infrastructure/Seeders/SeedOrchestrator.cs`, replace the whole `ResolveProfile` method (lines 82-99, including its doc comment) with:

```csharp
    /// <summary>
    /// Resolve seed profile: SEED_PROFILE env var → Seeding:Profile config →
    /// derive from ASPNETCORE_ENVIRONMENT (fail-closed to None on unknown).
    /// </summary>
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
            "production" => SeedProfile.Prod,
            "staging" => SeedProfile.Staging,
            "development" => SeedProfile.Dev,
            _ => SeedProfile.None, // null / "Test" / "CI" / unknown → fail-closed
        };

        if (derived == SeedProfile.None)
        {
            logger?.LogWarning(
                "Seed profile unresolved (SEED_PROFILE and Seeding:Profile unset; ASPNETCORE_ENVIRONMENT='{Env}' unrecognized). "
                + "Seeding with profile None (no data). Set SEED_PROFILE explicitly.", env ?? "(null)");
        }

        return derived;
    }
```

Then update the caller at line 35 inside `RunAsync` from:

```csharp
        var profile = ResolveProfile(_configuration);
```

to:

```csharp
        var profile = ResolveProfile(_configuration, logger: _logger);
```

- [ ] **Step 4: Run the seeder tests to verify they PASS**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SeedOrchestratorTests|FullyQualifiedName~SeedProfileTests" -v minimal`
Expected: PASS — all `ResolveProfile_*`, `FilterLayers_*`, and `SeedProfile` parse tests green. `FilterLayers_NoneProfile_ReturnsEmpty` confirms `None` runs zero layers.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/SeedOrchestrator.cs \
        apps/api/tests/Api.Tests/Infrastructure/Seeders/SeedOrchestratorTests.cs
git commit -m "fix(seed): #2893 derive seed profile from ASPNETCORE_ENVIRONMENT (fail-closed)"
```

---

### Task 2: Wire prod seed profile + remove phantom compose reference

**Files:**
- Modify: `infra/compose.prod.yml` (api `environment:` block, ~line 74)
- Modify: `scripts/deployment/deploy-meepleai.ps1:38-43` (`$ComposeFiles`)

**Interfaces:**
- Consumes: the Task 1 derive logic (this task is defense-in-depth — explicit `SEED_PROFILE: Prod` also makes tier 1 resolve to Prod regardless of environment).
- Produces: nothing consumed by later code tasks.

- [ ] **Step 1: Add `SEED_PROFILE: Prod` to the prod api environment**

In `infra/compose.prod.yml`, inside the `api:` service `environment:` block, add the line directly under `ASPNETCORE_ENVIRONMENT: Production`:

```yaml
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      SEED_PROFILE: Prod   # #2893: explicit prod seed profile (belt-and-suspenders over code derive)
      ASPNETCORE_URLS: http://+:8080
```

(Preserve the existing surrounding keys — only insert the `SEED_PROFILE` line.)

- [ ] **Step 2: Validate compose syntax**

Run (if Docker is available): `docker compose -f infra/docker-compose.yml -f infra/compose.prod.yml config >/dev/null && echo OK`
Expected: prints `OK` (no YAML/interpolation error), and `SEED_PROFILE: Prod` appears when you grep the rendered config.
If Docker is unavailable in the dev environment, instead run a YAML parse check: `python -c "import yaml,sys; yaml.safe_load(open('infra/compose.prod.yml')); print('OK')"` → expected `OK`.

- [ ] **Step 3: Comment out the phantom `compose.meepleai.yml` reference**

In `scripts/deployment/deploy-meepleai.ps1`, change the `$ComposeFiles` array (lines 38-43) to:

```powershell
# Compose files (post-PR #738 — Traefik decommissioned, edge=CF Tunnel)
$ComposeFiles = @(
    '-f', 'docker-compose.yml',
    '-f', 'compose.prod.yml'
    # '-f', 'compose.meepleai.yml'  # #2893: file never existed in repo/server checkout;
    #   prod seed profile now wired via compose.prod.yml + code derive. Re-enable if a real
    #   prod overlay is introduced.
)
```

- [ ] **Step 4: Commit**

```bash
git add infra/compose.prod.yml scripts/deployment/deploy-meepleai.ps1
git commit -m "chore(infra): #2893 wire SEED_PROFILE=Prod, drop phantom compose.meepleai.yml ref"
```

---

### Task 3: Full-suite verification + integration/E2E seed-coupling contingency

Startup seeding runs in Testing/CI hosts too (`Program.cs:597`, outside the `ShouldSkipMigrations` guard), and the integration/E2E factories boot with `UseEnvironment("Testing")` / `UseEnvironment("CI")` (e.g. `apps/api/tests/Api.Tests/Routing/EndpointContractTests.cs:193`, `E2E/Infrastructure/E2ETestBase.cs:515`, `Integration/CorsHeaderWhitelistTests.cs:35`, `Integration/FrontendSdk/FrontendSdkTestFactory.cs:136`). Before this change those hosts resolved to the implicit `Dev` profile; after it they resolve to `None` (no startup seeding). This is expected and usually benign (integration tests build their own fixtures via `AdminUserFactory`/`TestSessionHelper`), but any test that asserted on **startup-seeded** data will fail and needs the seed profile pinned explicitly.

**Files:**
- (Contingent) Modify the affected test factory/fixture's `ConfigureWebHost` (only if Step 2 shows failures).

- [ ] **Step 1: Kill orphan testhost, then run the full backend suite**

```bash
taskkill //F //IM Api.Tests.exe 2>/dev/null; taskkill //F //IM testhost.exe 2>/dev/null; true
dotnet test apps/api/tests/Api.Tests -v minimal
```
Expected: the suite completes and the pass/fail count does not regress below the `main-dev` baseline (currently zero known unit failures per CLAUDE.md). Note any newly-failing test names.

- [ ] **Step 2: Decide — green vs regression**

- If the suite is green (no new failures vs baseline): this task is complete, skip Steps 3-4.
- If integration/E2E tests fail with symptoms of missing seeded data (e.g. "admin user not found", "expected N games", "seeded golden claim missing", 404 on a game the test never created): proceed to Step 3 for each affected factory.

- [ ] **Step 3: Pin the seed profile in the affected factory (only if Step 2 found regressions)**

In the failing test's `WebApplicationFactory` subclass `ConfigureWebHost` (or the `builder.UseEnvironment(...)` setup), pin the profile via **config** (tier 2 — no env-var pollution) so startup seeding matches the pre-change behavior. Add:

```csharp
builder.ConfigureAppConfiguration((_, config) =>
{
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        // #2893: pin startup seed profile for the test host (was implicitly Dev before
        // ResolveProfile went fail-closed; Testing/CI now derive to None).
        ["Seeding:Profile"] = "Dev",
    });
});
```

Use the smallest profile that satisfies the test's data needs (`Prod` if it only needs the admin user + core config; `Staging` if it needs the catalog/golden data; `Dev` to fully restore prior behavior). Re-run the specific failing test class:

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~<FailingClassName>" -v minimal`
Expected: PASS.

- [ ] **Step 4: Re-run the full suite and commit any fixture fixes**

```bash
dotnet test apps/api/tests/Api.Tests -v minimal
git add apps/api/tests/Api.Tests/<affected fixture files>
git commit -m "test(seed): #2893 pin explicit seed profile in integration/E2E fixtures"
```
Expected: full suite green.

---

### Task 4: PR to `main-dev` + code review + close issue

**Files:** none (git/GitHub operations).

- [ ] **Step 1: Push the branch**

```bash
git push -u origin feature/issue-2893-seed-fail-closed
```

- [ ] **Step 2: Open the PR against the parent branch**

```bash
gh pr create --base main-dev --head feature/issue-2893-seed-fail-closed \
  --title "fix(seed): #2893 fail-closed seed profile (derive from ASPNETCORE_ENVIRONMENT)" \
  --body "Closes #2893. Derives the seed profile from ASPNETCORE_ENVIRONMENT with a None fail-closed fallback; wires SEED_PROFILE=Prod in compose.prod.yml; drops the phantom compose.meepleai.yml deploy reference. Spec: docs/superpowers/specs/2026-07-13-seed-fail-closed-design.md. 🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

- [ ] **Step 3: Run code review before merge** (required per workflow rule)

Invoke `/code-review:code-review <PR-URL>` (pass the PR diff). Address any BLOCKER/HIGH findings; re-verify tests after fixes.

- [ ] **Step 4: Merge once CI green + review clean**

```bash
gh pr merge --squash --delete-branch
```
(Confirm `headRefOid` on GitHub matches local HEAD before merging — avoid the squash-race that drops commits.)

- [ ] **Step 5: Verify issue closed + update local memory**

Confirm #2893 auto-closed by the merge (PR body "Closes #2893"). If not, `gh issue close 2893 --comment "Fixed via PR #<n> (fail-closed seed profile)."`.

---

## Self-Review

**Spec coverage:**
- §1 ResolveProfile derive + fail-closed → Task 1. ✓
- §2 observability (invalid-value warning, None warning) → Task 1 (implementation) + `ResolveProfile_InvalidEnvVar_FallsThroughToDerive` asserts the fallthrough behavior. ✓
- §3 compose.prod.yml `SEED_PROFILE: Prod` + deploy-script phantom removal → Task 2. ✓
- §4 config surface (no appsettings change) → respected (no task touches appsettings). ✓
- §5 testing (invert default test, derive matrix, precedence, invalid) → Task 1 Step 1. ✓
- Risk: integration/E2E regression gate → Task 3. ✓
- AC bullets → covered across Tasks 1-3; issue closure → Task 4. ✓

**Placeholder scan:** No "TBD/TODO/handle edge cases". Task 3 is contingent-but-concrete: it branches on an observable test result and supplies the exact remediation code. ✓

**Type consistency:** `ResolveProfile(IConfiguration?, string? environmentName = null, ILogger? logger = null)` used identically in Task 1 tests (Step 1), implementation (Step 3), and caller update (Step 3). Enum members `Prod/Staging/Dev/None` consistent with the fixed enum. ✓
