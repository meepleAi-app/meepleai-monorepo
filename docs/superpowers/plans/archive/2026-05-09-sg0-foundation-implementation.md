# SG0 Foundation — Bruno Smoke Test Infrastructure Implementation Plan

**Status**: ✅ COMPLETED (PR #919 — Bruno smoke test infrastructure)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Costruire l'infrastruttura comune per le 4 sub-issue di smoke test API CRUD (#902-905) — Bruno collection scaffold, runner cross-platform, CI workflow soft-launch, persona test dedicata `smoke-aaron@meepleai.test` (free-tier), README guida — così che le successive SG possano focalizzarsi solo sui propri scenari.

**Architecture:** Greenfield setup sotto `tests/api-smoke/` (repository-root, fuori da `apps/`) con Bruno collection git-friendly. Runner bash + PowerShell che invoca Bruno CLI (`@usebruno/cli`). CI workflow `api-smoke.yml` distinto dal nightly E2E (no Playwright, solo HTTP). Persona seedata via SQL fixture con bcrypt hash pre-calcolato (no code change in `Program.cs`).

**Tech Stack:** Bruno (`.bru` DSL) · `@usebruno/cli` (Node 22) · GitHub Actions · PowerShell 7+ + bash · Postgres 16 (per fixture SQL) · BCrypt (Pbkdf2/SHA256-12 default `BCrypt.Net`)

**Reference issue:** #910 (SG0), parent EPIC #906.

---

## File Structure

### New (created in this plan)

```
tests/
├─ api-smoke/
│  ├─ bruno-collection/
│  │  ├─ collection.bru                          # T1 root config
│  │  ├─ environments/
│  │  │  ├─ local.bru                            # T1 dev/local env
│  │  │  └─ staging.bru                          # T1 staging env
│  │  ├─ private-game/.gitkeep                   # T1 (consumer SG1 #902)
│  │  ├─ kb/.gitkeep                             # T1 (consumer SG2 #903)
│  │  ├─ agents/.gitkeep                         # T1 (consumer SG3 #904)
│  │  └─ sessions/.gitkeep                       # T1 (consumer SG4 #905)
│  ├─ run-smoke.sh                               # T3 bash runner
│  ├─ run-smoke.ps1                              # T4 PowerShell runner
│  └─ test/
│     ├─ run-smoke-help.test.sh                  # T3 contract test bash
│     └─ Test-RunSmokeHelp.ps1                   # T4 contract test PS
├─ fixtures/
│  └─ smoke-test-users.sql                       # T2 fixture utente smoke-aaron
└─ ...

.github/
└─ workflows/
   └─ api-smoke.yml                              # T5 CI workflow soft-launch

docs/
└─ for-developers/
   └─ testing/
      └─ api-smoke/
         └─ README.md                            # T6 guida + persona + naming preview
```

### Modified

- `docs/for-developers/testing/README.md` — T6: aggiunta entry "API smoke tests" con link
- `apps/api/src/Api/Infrastructure/Persistence/MeepleAiDbContext.cs` — T2: NESSUNA MODIFICA (fixture è SQL puro applicato post-migration)

---

## Conventions

**Branch:** già su `feature/issue-910-sg0-foundation-bruno-setup` (creato da `main-dev`, parent tracking configurato via `git config branch.<name>.parent main-dev`).

**Commits:** conventional `feat|test|docs|chore(scope): description` — uno per task minimum.

**Test path:** backend tests eventuali in `apps/api/tests/Api.Tests/Integration/Smoke/`. Smoke-test infra resta in `tests/api-smoke/` (greenfield, NON dentro `apps/`).

**Persona NOT to use:** Aaron `badsworm@alice.it` è superadmin nel DB reale (vedi `MEMORY.md`). Per smoke test usare SOLO l'utente dedicato `smoke-aaron@meepleai.test` con `Tier="free"`, `Role="user"`.

**Bruno version:** pinata in `tests/api-smoke/.bruno-version` (T1) per riproducibilità CI.

**FREEZE compliance:** N/A — questa issue non tocca componenti v2 frontend.

---

## Tasks

### Task 1: Bruno collection scaffold + environments

**Files:**
- Create: `tests/api-smoke/.bruno-version`
- Create: `tests/api-smoke/bruno-collection/collection.bru`
- Create: `tests/api-smoke/bruno-collection/environments/local.bru`
- Create: `tests/api-smoke/bruno-collection/environments/staging.bru`
- Create: `tests/api-smoke/bruno-collection/private-game/.gitkeep`
- Create: `tests/api-smoke/bruno-collection/kb/.gitkeep`
- Create: `tests/api-smoke/bruno-collection/agents/.gitkeep`
- Create: `tests/api-smoke/bruno-collection/sessions/.gitkeep`

**Rationale:** Bruno è git-friendly (DSL `.bru`) e supporta env var. Pinning della versione in `.bruno-version` evita drift tra dev e CI.

- [ ] **Step 1: Pin Bruno CLI version**

Crea `tests/api-smoke/.bruno-version`:

```
2.15.1
```

> Versione corrente stabile al 2026-05-09. Verifica con `npx -y @usebruno/cli@latest --version` se vuoi aggiornare.

- [ ] **Step 2: Write collection root config**

Crea `tests/api-smoke/bruno-collection/collection.bru`:

```
meta {
  name: meepleai-api-smoke
  type: collection
  version: 1
}

vars {
  apiBase: {{apiBase}}
  smokeUserEmail: {{smokeUserEmail}}
  smokeUserPassword: {{smokeUserPassword}}
}

auth {
  mode: bearer
}

auth:bearer {
  token: {{authToken}}
}

script:pre-request {
  // Auto-login if no authToken yet (set after login script in env)
  if (!bru.getEnvVar("authToken")) {
    bru.setVar("authToken", "");
  }
}
```

- [ ] **Step 3: Write `local.bru` env (dev locale via `make dev-core`)**

Crea `tests/api-smoke/bruno-collection/environments/local.bru`:

```
vars {
  apiBase: http://localhost:8080
  smokeUserEmail: smoke-aaron@meepleai.test
  smokeUserPassword: SmokeAaron1!!
  authToken:
}
```

- [ ] **Step 4: Write `staging.bru` env (staging deploy)**

Crea `tests/api-smoke/bruno-collection/environments/staging.bru`:

```
vars {
  apiBase: https://staging.meepleai.app
  smokeUserEmail: smoke-aaron@meepleai.test
  smokeUserPassword: {{process.env.SMOKE_AARON_PASSWORD}}
  authToken:
}
```

> `SMOKE_AARON_PASSWORD` viene iniettato dal CI workflow (T5) via GitHub Secrets, NON committato in chiaro per staging.

- [ ] **Step 5: Create empty sub-collection directories**

```bash
touch tests/api-smoke/bruno-collection/private-game/.gitkeep
touch tests/api-smoke/bruno-collection/kb/.gitkeep
touch tests/api-smoke/bruno-collection/agents/.gitkeep
touch tests/api-smoke/bruno-collection/sessions/.gitkeep
```

- [ ] **Step 6: Smoke-validate Bruno can open the collection**

Run:

```bash
npx -y @usebruno/cli@2.15.1 run --env local tests/api-smoke/bruno-collection/ 2>&1 | head -20
```

Expected output: `No requests to run` (collection valida ma vuota — atteso, gli scenari arrivano in SG1-SG4).

> Se invece appare `Error: invalid collection format`, rivedi `collection.bru`.

- [ ] **Step 7: Commit**

```bash
git add tests/api-smoke/.bruno-version tests/api-smoke/bruno-collection/
git commit -m "feat(smoke): #910 Bruno collection scaffold + 2 env (local, staging)"
```

---

### Task 2: Smoke-aaron persona SQL fixture

**Files:**
- Create: `tests/fixtures/smoke-test-users.sql`
- Create: `apps/api/tests/Api.Tests/Integration/Smoke/SmokeAaronFixtureTests.cs`
- Modify: `apps/api/tests/Api.Tests/Integration/Smoke/` (new directory)

**Rationale:** Lo schema `UserEntity` (vedi `apps/api/src/Api/Infrastructure/Entities/Authentication/UserEntity.cs:7-77`) ha default `Tier="free"`, `Role="user"`. Quindi un INSERT puro è sufficiente — niente seed code in `Program.cs`. Il PasswordHash è pre-calcolato BCrypt per password fissa `SmokeAaron1!!`.

- [ ] **Step 1: Pre-calcola BCrypt hash per password `SmokeAaron1!!`**

Run:

```bash
cd apps/api/src/Api
dotnet run --no-build --project ../../tools/HashPassword.csproj -- "SmokeAaron1!!" 2>/dev/null || \
  echo 'use this oneliner:' && \
  dotnet script -e 'Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("SmokeAaron1!!", 12));'
```

Se nessuno dei due funziona, alternativa portabile:

```bash
node -e "require('bcrypt').hash('SmokeAaron1!!', 12).then(h => console.log(h))"
```

Annota l'output (es. `$2a$12$abc...xyz`). **Lo userai nel SQL fixture al passo 3**.

> ⚠️ Il hash dipende dal salt random — generalo UNA VOLTA e committalo. Non ri-generarlo ad ogni run o l'integration test si rompe.

- [ ] **Step 2: Write failing integration test**

Crea `apps/api/tests/Api.Tests/Integration/Smoke/SmokeAaronFixtureTests.cs`:

```csharp
using Api.Infrastructure;
using Api.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.Smoke;

[Collection("Integration-GroupA")]
public sealed class SmokeAaronFixtureTests : IClassFixture<SharedTestcontainersFixture>
{
    private readonly SharedTestcontainersFixture _fixture;

    public SmokeAaronFixtureTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SmokeAaronFixture_AppliedAfterMigrations_HasFreeTier()
    {
        // Arrange — apply tests/fixtures/smoke-test-users.sql against the seeded DB
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..",
            "tests", "fixtures", "smoke-test-users.sql");
        var fixtureSql = await File.ReadAllTextAsync(fixturePath);

        await using var ctx = _fixture.CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync(fixtureSql);

        // Act
        var aaron = await ctx.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "smoke-aaron@meepleai.test");

        // Assert
        aaron.Should().NotBeNull("smoke-aaron fixture should insert the user");
        aaron!.Tier.Should().Be("free", "smoke-aaron is the free-tier persona for SG smoke tests");
        aaron.Role.Should().Be("user", "smoke-aaron must NOT be admin/editor/superadmin (those bypass tier limits)");
        aaron.IsContributor.Should().BeFalse("contributors are resolved as premium tier");
        aaron.IsDemoAccount.Should().BeTrue("smoke-aaron is a demo/test account");
        aaron.EmailVerified.Should().BeTrue("avoid grace-period gating in smoke tests");
        aaron.Status.Should().Be("Active");
        aaron.PasswordHash.Should().NotBeNullOrEmpty("must be able to login");
    }
}
```

- [ ] **Step 3: Run integration test — expect FAIL**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SmokeAaronFixtureTests" --logger "console;verbosity=normal"
```

Expected: `FAIL` con `FileNotFoundException` su `smoke-test-users.sql` (file non esiste).

- [ ] **Step 4: Write the SQL fixture**

Crea `tests/fixtures/smoke-test-users.sql`:

```sql
-- Smoke test user fixture — applied AFTER EF Core migrations.
--
-- Persona: smoke-aaron@meepleai.test (free-tier, Role=user)
-- Used by: tests/api-smoke/bruno-collection/* (Bruno collection runs against
--   running API; SG1-SG4 sub-issues #902-905 use this persona to validate
--   tier-quota gating in API smoke tests).
--
-- ⚠️  DO NOT confuse with badsworm@alice.it (Aaron, real superadmin in DB)
-- ⚠️  DO NOT confuse with smoke-user@meepleai.test (admin, used by nightly E2E)
--
-- BCrypt hash regenerable with:
--   node -e "require('bcrypt').hash('SmokeAaron1!!', 12).then(h => console.log(h))"
-- (committed once — DO NOT regenerate per run or integration tests will break)
--
-- Idempotent: ON CONFLICT (Email) DO NOTHING — safe to apply multiple times.

INSERT INTO "Users" (
    "Id",
    "Email",
    "DisplayName",
    "PasswordHash",
    "Role",
    "Tier",
    "CreatedAt",
    "IsDemoAccount",
    "Language",
    "EmailNotifications",
    "Theme",
    "DataRetentionDays",
    "IsTwoFactorEnabled",
    "EmailVerified",
    "EmailVerifiedAt",
    "IsSuspended",
    "Status",
    "Level",
    "ExperiencePoints",
    "FailedLoginAttempts",
    "IsContributor",
    "OnboardingCompleted",
    "OnboardingSkipped"
)
VALUES (
    -- Stable Guid (same across all environments — easy to reference in tests)
    '00000000-5MOK-4AAA-AAAA-000000000001',
    'smoke-aaron@meepleai.test',
    'Smoke Aaron Free-Tier',
    -- ⚠️  REPLACE WITH HASH FROM STEP 1 (per la password 'SmokeAaron1!!')
    '$2a$12$REPLACE_WITH_REAL_BCRYPT_HASH_FROM_STEP_1',
    'user',
    'free',
    NOW(),
    TRUE,                  -- IsDemoAccount
    'it',
    FALSE,                 -- EmailNotifications (no spam in test)
    'system',
    90,
    FALSE,                 -- IsTwoFactorEnabled
    TRUE,                  -- EmailVerified (skip grace period)
    NOW(),                 -- EmailVerifiedAt
    FALSE,                 -- IsSuspended
    'Active',
    1,                     -- Level
    0,                     -- ExperiencePoints
    0,                     -- FailedLoginAttempts
    FALSE,                 -- IsContributor (NOT premium override)
    TRUE,                  -- OnboardingCompleted (skip onboarding wizard)
    FALSE                  -- OnboardingSkipped
)
ON CONFLICT ("Email") DO NOTHING;
```

> Sostituisci `$2a$12$REPLACE_WITH_REAL_BCRYPT_HASH_FROM_STEP_1` con il hash reale generato al Step 1.

- [ ] **Step 5: Run integration test — expect PASS**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SmokeAaronFixtureTests" --logger "console;verbosity=normal"
```

Expected: `PASS` con tutti gli assert verdi.

> Se fallisce: verifica che la struttura colonne in `UserEntity.cs` sia ancora identica al momento della scrittura del plan (campi possono essere stati aggiunti/rimossi). Aggiorna SQL e ri-run.

- [ ] **Step 6: Commit**

```bash
git add tests/fixtures/smoke-test-users.sql apps/api/tests/Api.Tests/Integration/Smoke/SmokeAaronFixtureTests.cs
git commit -m "feat(smoke): #910 smoke-aaron@meepleai.test fixture (free-tier persona)"
```

---

### Task 3: bash runner `run-smoke.sh` (Linux/CI)

**Files:**
- Create: `tests/api-smoke/run-smoke.sh`
- Create: `tests/api-smoke/test/run-smoke-help.test.sh`

- [ ] **Step 1: Write contract test (FAILS before implementation)**

Crea `tests/api-smoke/test/run-smoke-help.test.sh`:

```bash
#!/usr/bin/env bash
# Contract test for run-smoke.sh.
# Verifica che --help: (a) exit 0, (b) print "Usage:" line, (c) menziona --env.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_SMOKE="$SCRIPT_DIR/run-smoke.sh"

if [[ ! -x "$RUN_SMOKE" ]]; then
  echo "FAIL: $RUN_SMOKE does not exist or is not executable"
  exit 1
fi

OUTPUT="$("$RUN_SMOKE" --help 2>&1)"
EXIT_CODE=$?

if [[ $EXIT_CODE -ne 0 ]]; then
  echo "FAIL: --help exited with $EXIT_CODE (expected 0)"
  echo "$OUTPUT"
  exit 1
fi

if ! grep -q "Usage:" <<< "$OUTPUT"; then
  echo "FAIL: --help output missing 'Usage:'"
  echo "$OUTPUT"
  exit 1
fi

if ! grep -q -- "--env" <<< "$OUTPUT"; then
  echo "FAIL: --help output missing '--env' flag"
  echo "$OUTPUT"
  exit 1
fi

echo "PASS: run-smoke.sh --help contract"
```

Make it executable:

```bash
chmod +x tests/api-smoke/test/run-smoke-help.test.sh
```

- [ ] **Step 2: Run contract test — expect FAIL**

```bash
bash tests/api-smoke/test/run-smoke-help.test.sh
```

Expected output: `FAIL: ...run-smoke.sh does not exist or is not executable`.

- [ ] **Step 3: Implement `run-smoke.sh`**

Crea `tests/api-smoke/run-smoke.sh`:

```bash
#!/usr/bin/env bash
# meepleai-api-smoke runner (Linux/macOS/CI)
#
# Invokes Bruno CLI against the bruno-collection/ directory with the chosen env.
# Prints a JSON summary + table.
#
# Usage:
#   tests/api-smoke/run-smoke.sh --env local
#   tests/api-smoke/run-smoke.sh --env staging --collection private-game
#   tests/api-smoke/run-smoke.sh --help
#
# Exit codes:
#   0  = all scenarios passed
#   1  = one or more scenarios failed
#   2  = invalid arguments / Bruno CLI not found

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COLLECTION_ROOT="$SCRIPT_DIR/bruno-collection"
BRUNO_VERSION="$(cat "$SCRIPT_DIR/.bruno-version" 2>/dev/null || echo "2.15.1")"

ENV_NAME=""
SUB_COLLECTION=""

usage() {
  cat <<EOF
Usage: run-smoke.sh [OPTIONS]

Run meepleai API smoke tests via Bruno CLI.

Options:
  --env <name>          Environment name (local|staging). REQUIRED.
  --collection <name>   Run only one sub-collection (private-game|kb|agents|sessions).
                        If omitted, runs all 4.
  --help                Show this help and exit.

Examples:
  run-smoke.sh --env local
  run-smoke.sh --env staging --collection private-game

Exit codes:
  0  all scenarios passed
  1  one or more scenarios failed
  2  invalid arguments / Bruno CLI not found
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env)
      ENV_NAME="${2:-}"
      shift 2
      ;;
    --collection)
      SUB_COLLECTION="${2:-}"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$ENV_NAME" ]]; then
  echo "Error: --env is required" >&2
  usage >&2
  exit 2
fi

ENV_FILE="$COLLECTION_ROOT/environments/${ENV_NAME}.bru"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Error: env file not found: $ENV_FILE" >&2
  exit 2
fi

TARGET="$COLLECTION_ROOT"
if [[ -n "$SUB_COLLECTION" ]]; then
  TARGET="$COLLECTION_ROOT/$SUB_COLLECTION"
  if [[ ! -d "$TARGET" ]]; then
    echo "Error: sub-collection not found: $TARGET" >&2
    exit 2
  fi
fi

echo "Running Bruno (v${BRUNO_VERSION}) — env=${ENV_NAME}, target=${TARGET#$SCRIPT_DIR/}"
npx -y "@usebruno/cli@${BRUNO_VERSION}" run --env "$ENV_NAME" "$TARGET"
```

Make it executable:

```bash
chmod +x tests/api-smoke/run-smoke.sh
```

- [ ] **Step 4: Run contract test — expect PASS**

```bash
bash tests/api-smoke/test/run-smoke-help.test.sh
```

Expected output: `PASS: run-smoke.sh --help contract`.

- [ ] **Step 5: Smoke-validate against local env (optional, requires API up)**

```bash
# Solo se hai `make dev-core` running su :8080:
tests/api-smoke/run-smoke.sh --env local 2>&1 | tail -5
```

Expected: Bruno output `No requests to run` (collection vuota — atteso).

- [ ] **Step 6: Commit**

```bash
git add tests/api-smoke/run-smoke.sh tests/api-smoke/test/run-smoke-help.test.sh
git commit -m "feat(smoke): #910 run-smoke.sh bash runner with --help/--env/--collection"
```

---

### Task 4: PowerShell runner `run-smoke.ps1` (Windows)

**Files:**
- Create: `tests/api-smoke/run-smoke.ps1`
- Create: `tests/api-smoke/test/Test-RunSmokeHelp.ps1`

- [ ] **Step 1: Write contract test (FAILS before implementation)**

Crea `tests/api-smoke/test/Test-RunSmokeHelp.ps1`:

```powershell
#!/usr/bin/env pwsh
# Contract test for run-smoke.ps1.
# Verifica che -Help: (a) exit 0, (b) stampa "Usage:" line, (c) menziona -Env.

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $PSScriptRoot
$RunSmoke = Join-Path $ScriptDir 'run-smoke.ps1'

if (-not (Test-Path $RunSmoke)) {
    Write-Host "FAIL: $RunSmoke does not exist"
    exit 1
}

$Output = & pwsh -NoProfile -File $RunSmoke -Help 2>&1
$ExitCode = $LASTEXITCODE

if ($ExitCode -ne 0) {
    Write-Host "FAIL: -Help exited with $ExitCode (expected 0)"
    Write-Host $Output
    exit 1
}

$OutputText = $Output -join "`n"

if ($OutputText -notmatch 'Usage:') {
    Write-Host "FAIL: -Help output missing 'Usage:'"
    Write-Host $OutputText
    exit 1
}

if ($OutputText -notmatch '-Env') {
    Write-Host "FAIL: -Help output missing '-Env' flag"
    Write-Host $OutputText
    exit 1
}

Write-Host "PASS: run-smoke.ps1 -Help contract"
exit 0
```

- [ ] **Step 2: Run contract test — expect FAIL**

```powershell
pwsh -NoProfile -File tests/api-smoke/test/Test-RunSmokeHelp.ps1
```

Expected: `FAIL: ...run-smoke.ps1 does not exist`.

- [ ] **Step 3: Implement `run-smoke.ps1`**

Crea `tests/api-smoke/run-smoke.ps1`:

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    meepleai-api-smoke runner (Windows / cross-platform PowerShell 7+).

.DESCRIPTION
    Invokes Bruno CLI against the bruno-collection/ directory with the chosen env.
    Prints a JSON summary + table.

.PARAMETER Env
    Environment name (local|staging). REQUIRED.

.PARAMETER Collection
    Run only one sub-collection (private-game|kb|agents|sessions). Optional.

.PARAMETER Help
    Show usage and exit.

.EXAMPLE
    .\run-smoke.ps1 -Env local

.EXAMPLE
    .\run-smoke.ps1 -Env staging -Collection private-game

.NOTES
    Exit codes:
      0  all scenarios passed
      1  one or more scenarios failed
      2  invalid arguments / Bruno CLI not found
#>
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory = $false)]
    [string]$Env,

    [Parameter(ParameterSetName = 'Run')]
    [string]$Collection,

    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    @"
Usage: run-smoke.ps1 [OPTIONS]

Run meepleai API smoke tests via Bruno CLI.

Options:
  -Env <name>          Environment name (local|staging). REQUIRED.
  -Collection <name>   Run only one sub-collection
                       (private-game|kb|agents|sessions).
  -Help                Show this help and exit.

Examples:
  .\run-smoke.ps1 -Env local
  .\run-smoke.ps1 -Env staging -Collection private-game

Exit codes:
  0  all scenarios passed
  1  one or more scenarios failed
  2  invalid arguments / Bruno CLI not found
"@ | Write-Output
}

if ($Help) {
    Show-Usage
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Env)) {
    Write-Error "Error: -Env is required"
    Show-Usage | Write-Error
    exit 2
}

$ScriptDir = $PSScriptRoot
$CollectionRoot = Join-Path $ScriptDir 'bruno-collection'
$BrunoVersionFile = Join-Path $ScriptDir '.bruno-version'
$BrunoVersion = if (Test-Path $BrunoVersionFile) {
    (Get-Content $BrunoVersionFile -Raw).Trim()
} else {
    '2.15.1'
}

$EnvFile = Join-Path $CollectionRoot "environments\$Env.bru"
if (-not (Test-Path $EnvFile)) {
    Write-Error "Error: env file not found: $EnvFile"
    exit 2
}

$Target = $CollectionRoot
if (-not [string]::IsNullOrWhiteSpace($Collection)) {
    $Target = Join-Path $CollectionRoot $Collection
    if (-not (Test-Path $Target)) {
        Write-Error "Error: sub-collection not found: $Target"
        exit 2
    }
}

Write-Host "Running Bruno (v$BrunoVersion) — env=$Env, target=$($Target.Replace($ScriptDir + [IO.Path]::DirectorySeparatorChar, ''))"
& npx -y "@usebruno/cli@$BrunoVersion" run --env $Env $Target
exit $LASTEXITCODE
```

- [ ] **Step 4: Run contract test — expect PASS**

```powershell
pwsh -NoProfile -File tests/api-smoke/test/Test-RunSmokeHelp.ps1
```

Expected output: `PASS: run-smoke.ps1 -Help contract`.

- [ ] **Step 5: Commit**

```bash
git add tests/api-smoke/run-smoke.ps1 tests/api-smoke/test/Test-RunSmokeHelp.ps1
git commit -m "feat(smoke): #910 run-smoke.ps1 PowerShell runner with -Help/-Env/-Collection"
```

---

### Task 5: CI workflow `api-smoke.yml` (soft-launch)

**Files:**
- Create: `.github/workflows/api-smoke.yml`

**Rationale:** Distinto da `e2e-smoke-real-backend.yml` (nightly Playwright). Trigger su PR `main-dev → main-staging` only, matrix Win+Linux, `continue-on-error: true` per le prime 1-2 settimane (rimuovi dopo che 30 scenari Bruno sono stabili in CI).

- [ ] **Step 1: Write workflow file**

Crea `.github/workflows/api-smoke.yml`:

```yaml
name: API Smoke (Bruno) — PR main-dev → main-staging

on:
  pull_request:
    branches:
      - main-staging
    paths:
      # Trigger only when PR includes API/smoke-relevant changes.
      # Avoid running on docs-only / mockup-only PRs to main-staging.
      - 'apps/api/**'
      - 'tests/api-smoke/**'
      - 'tests/fixtures/smoke-test-users.sql'
      - 'infra/**'
      - '.github/workflows/api-smoke.yml'
  workflow_dispatch:

permissions:
  contents: read
  pull-requests: write   # for sticky comment on PR with summary

concurrency:
  group: api-smoke-${{ github.ref }}
  cancel-in-progress: true

jobs:
  smoke:
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest]
    timeout-minutes: 25
    # SOFT-LAUNCH: do NOT block PR merge while infra stabilizes.
    # Remove this after 30 scenarios green in CI for 2 consecutive weeks.
    # See issue #910 DoD checklist.
    continue-on-error: true

    steps:
      - uses: actions/checkout@v4

      # ─── Boot dev stack (postgres + redis + api) ──────────────────────────
      # Reuse the same approach as e2e-smoke-real-backend.yml but skip Playwright.

      - name: Set up Docker Buildx (Linux only)
        if: runner.os == 'Linux'
        uses: docker/setup-buildx-action@8d2750c68a42422c14e847fe6c8ac0403b4cbd6f  # v3

      - name: Generate placeholder secrets (CI cannot secrets-sync)
        if: runner.os == 'Linux'
        working-directory: infra
        run: make secrets-setup

      - name: Generate placeholder env files
        if: runner.os == 'Linux'
        working-directory: infra/env
        shell: bash
        run: |
          cat > api.env.dev <<'APIENV'
          POSTGRES_HOST=postgres
          POSTGRES_PORT=5432
          REDIS_HOST=redis
          REDIS_PORT=6379
          INITIAL_ADMIN_EMAIL=smoke-user@meepleai.test
          INITIAL_ADMIN_PASSWORD=SmokeUser1!!
          INITIAL_ADMIN_DISPLAY_NAME=Smoke User
          APIENV

      - name: Boot dev stack
        if: runner.os == 'Linux'
        working-directory: infra
        run: |
          make dev-core
          for i in {1..40}; do
            if curl -fs http://localhost:8080/health 2>/dev/null; then
              echo "API up"
              break
            fi
            sleep 5
          done

      - name: Apply smoke-aaron fixture
        if: runner.os == 'Linux'
        run: |
          docker compose -f infra/docker-compose.yml -f infra/compose.dev.yml \
            exec -T postgres psql -U postgres -d meepleai_dev \
            -f /tests/fixtures/smoke-test-users.sql

      # ─── Bruno CLI run ────────────────────────────────────────────────────

      - name: Setup Node 22
        uses: actions/setup-node@v4
        with:
          node-version: 22

      - name: Run smoke (Linux — bash)
        if: runner.os == 'Linux'
        env:
          SMOKE_AARON_PASSWORD: SmokeAaron1!!
        run: tests/api-smoke/run-smoke.sh --env local

      - name: Run smoke (Windows — PowerShell)
        if: runner.os == 'Windows'
        shell: pwsh
        env:
          SMOKE_AARON_PASSWORD: SmokeAaron1!!
        run: |
          # Windows runner: use staging API (not local Docker stack) since Win runners
          # boot dev stack is slow and we test runner cross-platform compat here.
          # Real assertion is on Linux runner.
          ./tests/api-smoke/run-smoke.ps1 -Env staging -Collection private-game

      - name: Dump API logs on failure (Linux)
        if: failure() && runner.os == 'Linux'
        working-directory: infra
        run: docker compose -f docker-compose.yml -f compose.dev.yml logs --tail=200 api postgres redis || true
```

> ⚠️ Linea `apply smoke-aaron fixture`: il path `/tests/fixtures/...` dentro container postgres dipende da come `compose.dev.yml` monta il repo. Verifica con `docker compose ... exec postgres ls /tests/fixtures` e adegua se il mount è diverso (potrebbe essere `/workspace/tests/...`). **Step 3 cattura questo aggiustamento.**

- [ ] **Step 2: Validate workflow YAML syntax**

Run:

```bash
yamllint -d "{rules: {line-length: disable}}" .github/workflows/api-smoke.yml
```

Expected: no errors. Se `yamllint` non installato:

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/api-smoke.yml'))" && echo "OK"
```

- [ ] **Step 3: Verify postgres mount path is correct**

Inspect `infra/compose.dev.yml` per la sezione `postgres:` `volumes:`:

```bash
grep -A5 "postgres:" infra/compose.dev.yml | head -20
```

Annota il path effettivo del repo dentro container (es. `/workspace`, `/var/lib/...`). Aggiorna il `docker compose ... exec` step in `api-smoke.yml` se il path differisce da `/tests/fixtures/...`.

> Se non c'è alcun mount del repo, valuta alternativa: `cat tests/fixtures/smoke-test-users.sql | docker compose ... exec -T postgres psql -U postgres -d meepleai_dev`.

- [ ] **Step 4: Test workflow via dummy PR (manual, post-commit)**

```bash
# After committing T5, open a draft PR main-dev -> main-staging to trigger workflow.
# This is a MANUAL verification step, NOT automated.
gh pr create --base main-staging --head main-dev --draft --title "[CI test] verify api-smoke.yml" --body "Smoke-launch verification only. Close without merging."
```

Verifica nella tab "Actions" GitHub:
- Workflow `API Smoke (Bruno) — PR main-dev → main-staging` parte
- Both runner (linux + windows) eseguono
- `continue-on-error: true` permette di chiudere PR anche se rosso
- Output Bruno mostra `No requests to run` (collection vuota)

Chiudi il draft PR senza mergiare.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/api-smoke.yml
git commit -m "feat(smoke): #910 api-smoke.yml CI workflow soft-launch (continue-on-error)"
```

---

### Task 6: README + docs link

**Files:**
- Create: `docs/for-developers/testing/api-smoke/README.md`
- Modify: `docs/for-developers/testing/README.md` (add link entry)

- [ ] **Step 1: Write README**

Crea `docs/for-developers/testing/api-smoke/README.md`:

```markdown
# API Smoke Tests (Bruno collection)

Bruno collection git-friendly per smoke test API CRUD, parte di EPIC #906.

## Setup locally

1. Install Bruno desktop: https://www.usebruno.com/downloads
2. Open the collection:
   - Bruno → Open Collection → seleziona `tests/api-smoke/bruno-collection/`
3. Select environment: `local` (default) o `staging`
4. Click Run.

## Setup CLI (per CI o headless)

```bash
# Pin version
cat tests/api-smoke/.bruno-version  # → 2.15.1

# Run all 4 sub-collections against local env (richiede `make dev-core` running)
tests/api-smoke/run-smoke.sh --env local

# Run only private-game collection against staging
tests/api-smoke/run-smoke.sh --env staging --collection private-game

# Windows
.\tests\api-smoke\run-smoke.ps1 -Env local
.\tests\api-smoke\run-smoke.ps1 -Env staging -Collection private-game
```

## CI policy

Il workflow `.github/workflows/api-smoke.yml` gira **solo su PR `main-dev → main-staging`** (no su PR feature → main-dev). Vedi commit `674f3c355` (issue #897) per la rationale CI policy 2026-05-09.

**Soft-launch attivo**: `continue-on-error: true` per le prime 1-2 settimane mentre l'infrastruttura stabilizza (issue #910 DoD). Rimuovere dopo che 30 scenari Bruno (#902-905) sono verdi in CI per 2 settimane consecutive.

### Distinct dal nightly E2E

Questo workflow è **distinto** da `e2e-smoke-real-backend.yml`:

| Aspetto | `api-smoke.yml` (questo) | `e2e-smoke-real-backend.yml` (nightly) |
|---|---|---|
| Trigger | PR `main-dev → main-staging` | Cron nightly + manual |
| Tooling | Bruno CLI (HTTP) | Playwright (browser) |
| Scope | API contracts CRUD 4 domini | E2E user journeys via UI |
| Persona | `smoke-aaron@meepleai.test` (free-tier) | `smoke-user@meepleai.test` (admin) |
| Failure action | continue-on-error (soft-launch) | Open P0 issue (`smoke-failure` label) |

I 4 issue P0 nightly recenti (#884, #877, #832, #821) NON impattano `api-smoke.yml` perché la pipeline è separata. Dettagli infra audit: vedi T7 di [docs/superpowers/plans/2026-05-09-sg0-foundation-implementation.md](../../../superpowers/plans/2026-05-09-sg0-foundation-implementation.md).

## Persona override mechanism

⚠️ **NON usare** Aaron `badsworm@alice.it` direttamente nei smoke test API. Aaron è **superadmin** nel DB reale, e `TierEnforcementService.GetLimitsAsync` lo risolve come `TierLimits.Unlimited` — bypassando proprio le tier-quota che vogliamo testare.

✅ **Usare** `smoke-aaron@meepleai.test` (creato da `tests/fixtures/smoke-test-users.sql`):

| Campo | Valore |
|---|---|
| Email | `smoke-aaron@meepleai.test` |
| Password | `SmokeAaron1!!` |
| Role | `user` (NON admin/editor/superadmin) |
| Tier | `free` |
| IsContributor | `false` (per evitare premium override) |
| EmailVerified | `true` (skip grace period) |
| IsDemoAccount | `true` |

### Quote free-tier validate dai test

| Risorsa | Limite free | Sub-issue |
|---|---|---|
| privateGame | 3 | SG1 #902 |
| Agent slots | 1 | SG1, SG3 #904 |
| ChatSession | 5 | SG4 #905 |
| RAPTOR rebuild | tier-locked (free → 403) | SG2 #903 |

### Discrepanza con seed Aaron reale

`docker-compose.dev.yml` + `INITIAL_ADMIN_EMAIL` seed l'admin di sistema (`smoke-user@meepleai.test`, NON Aaron `badsworm`). La fixture SQL `tests/fixtures/smoke-test-users.sql` aggiunge invece `smoke-aaron@meepleai.test` come secondo utente, idempotente (`ON CONFLICT DO NOTHING`).

## Naming disambiguation (preview ADR-054)

I 4 sub-collection mappano a 4 entità con naming ambiguo:

| Sub-collection | Entità target | Note |
|---|---|---|
| `private-game/` | `PrivateGame` | UserLibrary BC |
| `kb/` | KB di game (`vector_documents` + `pdf_documents`) | KnowledgeBase BC |
| `agents/` | `AgentDefinition` | KnowledgeBase BC |
| `sessions/` | 3 distinte: `GameSession` + `ChatSession` + `ChatThread` | Vedi tabella sotto |

### Tabella disambiguazione `sessions/`

| Concept | Backend entity | Endpoint prefix | Cosa rappresenta |
|---|---|---|---|
| Play session (live game) | `GameSession` (SessionTracking BC) | `/api/v1/game-sessions` | Una partita con scoring, participants, dice rolls |
| Chat history (persistent) | `ChatSession` (KnowledgeBase BC) | `/api/v1/chat/sessions` | Storia messaggi user↔agent per game+user |
| RAG Q&A thread | `ChatThread` (KnowledgeBase BC) | `/api/v1/chat/threads` | Thread RAG con citations, retrieval, agent invocation |
| Game night event | `GameNightEvent` (GameManagement BC) | `/api/v1/game-nights` | Evento programmato con RSVP + multi-session |

ADR-054 (post-MVP) proporrà di rinominare gli endpoint a `play-session/chat-thread/chat-history/game-night` per eliminare l'ambiguità. Out of scope per #906.

## Sub-collection consumers

- `private-game/` ← scenari implementati in #902 (SG1)
- `kb/` ← scenari implementati in #903 (SG2)
- `agents/` ← scenari implementati in #904 (SG3)
- `sessions/` ← scenari implementati in #905 (SG4)
```

- [ ] **Step 2: Verify `docs/for-developers/testing/README.md` exists, add link**

```bash
ls docs/for-developers/testing/README.md && echo "exists" || echo "missing"
```

If exists, append a link entry. Read first to find the right section:

```bash
head -50 docs/for-developers/testing/README.md
```

Add entry under appropriate section (e.g., "Test types" or "Smoke tests"):

```markdown
- **API smoke tests (Bruno)**: HTTP contract smoke per CRUD 4 domini → [api-smoke/README.md](./api-smoke/README.md). Trigger CI: PR `main-dev → main-staging`. Persona: `smoke-aaron@meepleai.test` (free-tier).
```

If `docs/for-developers/testing/README.md` doesn't exist, skip the link (T6 README is self-contained).

- [ ] **Step 3: Markdown lint**

```bash
npx -y markdownlint-cli2 "docs/for-developers/testing/api-smoke/README.md"
```

Expected: no errors. Fix any reported issues inline.

- [ ] **Step 4: Commit**

```bash
git add docs/for-developers/testing/api-smoke/README.md docs/for-developers/testing/README.md 2>/dev/null || git add docs/for-developers/testing/api-smoke/README.md
git commit -m "docs(smoke): #910 api-smoke README with persona, CI policy, naming disambiguation"
```

---

### Task 7: Audit nightly E2E vs api-smoke (no-overlap evidence)

**Files:**
- Modify: `docs/for-developers/testing/api-smoke/README.md` (sezione "Distinct dal nightly E2E", già scritta in T6)
- No code changes — audit report-only via comment su issue #884.

- [ ] **Step 1: Verifica triggers, scope, runners non overlappano**

Run:

```bash
# 1. Triggers — non si attivano sullo stesso evento
grep -A 5 "^on:" .github/workflows/e2e-smoke-real-backend.yml
grep -A 5 "^on:" .github/workflows/api-smoke.yml
```

Expected:
- `e2e-smoke-real-backend.yml`: `schedule: cron '0 3 * * *'` + `workflow_dispatch`
- `api-smoke.yml`: `pull_request: branches: [main-staging]` + `workflow_dispatch`

Nessun trigger condiviso ⇒ no race su scheduled run.

```bash
# 2. Test runners (concurrency group)
grep -A 2 "^concurrency:" .github/workflows/e2e-smoke-real-backend.yml || echo "(no concurrency)"
grep -A 2 "^concurrency:" .github/workflows/api-smoke.yml
```

Expected:
- `e2e-smoke-real-backend.yml`: nessun `concurrency` (run completo, no skip)
- `api-smoke.yml`: `group: api-smoke-${{ github.ref }}` (cancel in-progress su stesso PR)

⇒ Concurrency groups DISTINTI, no contention.

```bash
# 3. Tooling (Playwright vs Bruno)
grep -i "playwright" .github/workflows/api-smoke.yml | wc -l
```

Expected: `0` (nessuna menzione di Playwright in api-smoke.yml).

- [ ] **Step 2: Comment su issue #884 con audit findings**

```bash
gh issue comment 884 --repo meepleAi-app/meepleai-monorepo --body "$(cat <<'EOF'
**Cross-link from EPIC #906 SG0 audit (2026-05-09)**

Confermo che il nuovo workflow [.github/workflows/api-smoke.yml](https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/.github/workflows/api-smoke.yml) (introdotto da issue #910) è **distinto** da `e2e-smoke-real-backend.yml` (questa issue):

| Aspetto | api-smoke.yml | e2e-smoke-real-backend.yml |
|---|---|---|
| Trigger | PR main-dev → main-staging | Nightly cron + manual |
| Tooling | Bruno CLI (HTTP) | Playwright (browser) |
| Concurrency group | `api-smoke-${{ github.ref }}` | (none) |
| continue-on-error | true (soft-launch 1-2 settimane) | false (apre P0 issue) |
| Persona | smoke-aaron@meepleai.test (free) | smoke-user@meepleai.test (admin) |

Il fallimento ricorrente del nightly (questa issue + #877, #832, #821) **non si propaga** al nuovo api-smoke. La risoluzione di #884 può procedere indipendentemente.

Refs: #906, #910
EOF
)"
```

- [ ] **Step 3: Update README — mark audit completato**

Cerca in `docs/for-developers/testing/api-smoke/README.md` la sezione "Distinct dal nightly E2E" (già scritta in T6). Aggiungi a fine sezione:

```markdown
**Audit 2026-05-09**: confermato no overlap (vedi commento su #884). I 4 P0 nightly OPEN (#884, #877, #832, #821) non bloccano l'attivazione di api-smoke.yml.
```

Edita inline:

```bash
# Manual edit via Edit tool, NON sed (mantenere formatting consistente).
```

- [ ] **Step 4: Commit**

```bash
git add docs/for-developers/testing/api-smoke/README.md
git commit -m "docs(smoke): #910 audit confirms api-smoke distinct from nightly E2E"
```

---

### Task 8: PR draft + push

**Files:** none (git only)

- [ ] **Step 1: Push branch to origin**

```bash
git push -u origin feature/issue-910-sg0-foundation-bruno-setup
```

- [ ] **Step 2: Open draft PR to main-dev**

Detect parent branch:

```bash
git config --get branch.feature/issue-910-sg0-foundation-bruno-setup.parent
# Expected: main-dev
```

Open draft PR (HEREDOC body):

```bash
gh pr create --base main-dev --head feature/issue-910-sg0-foundation-bruno-setup --draft --title "feat(smoke): #910 SG0 Foundation — Bruno smoke test infrastructure" --body "$(cat <<'EOF'
## Summary

Implementa #910 (SG0 Foundation): infrastruttura comune per smoke test API CRUD via Bruno.

- **T1**: Bruno collection scaffold + 4 sub-cartelle + 2 env (local, staging)
- **T2**: Persona dedicata `smoke-aaron@meepleai.test` (free-tier, hash bcrypt pre-calcolato)
- **T3**: bash runner `run-smoke.sh` con `--env`, `--collection`, `--help`
- **T4**: PowerShell runner `run-smoke.ps1` cross-platform (Win + Linux + Mac)
- **T5**: CI workflow `api-smoke.yml` soft-launch (continue-on-error)
- **T6**: README guida + persona + naming disambiguation preview
- **T7**: Audit nightly E2E vs api-smoke confermato no-overlap

Blocca i 4 SG sub-issue (#902-905) e i 4 mockup (#911-914).

## Test plan

- [x] T1: `npx @usebruno/cli@2.15.1 run --env local tests/api-smoke/bruno-collection/` → "No requests to run"
- [x] T2: `dotnet test --filter SmokeAaronFixtureTests` → green
- [x] T3: `bash tests/api-smoke/test/run-smoke-help.test.sh` → PASS
- [x] T4: `pwsh -File tests/api-smoke/test/Test-RunSmokeHelp.ps1` → PASS
- [x] T5: dummy PR `main-dev → main-staging` triggera workflow su Linux + Windows runner, entrambi green con `No requests to run`
- [x] T6: `markdownlint` README pulito
- [x] T7: comment su #884 + verifica triggers/concurrency disjoint

## Refs

- Closes #910 (SG0 Foundation)
- Parent EPIC: #906
- Blocks: #902, #903, #904, #905

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Verify CI green su PR**

Aspetta workflow `validate-workflows.yml` + `ci.yml` (gli usuali su PR feature → main-dev). NON aspettare `api-smoke.yml` (gira solo su PR main-staging).

```bash
gh pr checks $(gh pr view --json number -q .number)
```

Expected: tutti i check verdi (o quelli irrilevanti come `visual-regression-*` skippati).

- [ ] **Step 4: Move PR to "Ready for review"**

```bash
gh pr ready
```

> A questo punto, fermati. Il PR è in review. NON mergiare automaticamente — l'utente decide.

---

## Self-Review

Plan completato. Review pre-commit:

### 1. Spec coverage (vs issue #910 body)

| Deliverable issue #910 | Task |
|---|---|
| `tests/api-smoke/bruno-collection/` 4 sub-dirs | T1 ✅ |
| `environments/staging.bru` | T1 ✅ |
| `collection.bru` config | T1 ✅ |
| `run-smoke.ps1` cross-platform | T4 ✅ |
| `run-smoke.sh` bash equivalent | T3 ✅ |
| Output JSON + summary | T3+T4 (`npx bruno run` lo stampa nativamente) ✅ |
| `.github/workflows/api-smoke.yml` | T5 ✅ |
| Trigger PR `main-dev → main-staging` | T5 ✅ |
| Soft-launch `continue-on-error: true` | T5 ✅ |
| Matrix Win + Linux | T5 ✅ |
| Distinct dal nightly E2E | T5+T7 ✅ |
| `tests/fixtures/smoke-test-users.sql` | T2 ✅ |
| `smoke-aaron@meepleai.test` free-tier | T2 ✅ |
| Quota privateGame=3, agent=1, ChatSession=5 | T2 (default ereditati da `Tier="free"` + `Role="user"`) ✅ — verifica `TierLimits.FreeTier` corrisponda |
| Auth password test fixed | T2 (`SmokeAaron1!!` con bcrypt hash) ✅ |
| Seeder integration NON polluisce prod | T2 (fixture SQL applicato manualmente, non in `Program.cs`) ✅ |
| README setup Bruno | T6 ✅ |
| README runner usage | T6 ✅ |
| README CI policy | T6 ✅ |
| README persona override mechanism | T6 ✅ |
| README tabella disambiguazione naming | T6 ✅ |
| Audit nightly E2E pre-go-live | T7 ✅ |
| Documentare relazione con #884 | T7 ✅ |
| CI workflow verde su PR test (anche con 0 scenari) | T5 step 4 manual verification ✅ |
| Persona dedicata seedata e documentata | T2 + T6 ✅ |

**Gap residuo**: nessuno.

### 2. Placeholder scan

- ❌ "TBD"/"TODO"/"implement later": 0 trovati ✅
- ⚠️ "REPLACE WITH HASH FROM STEP 1" in T2 step 4 — è un placeholder **intenzionale e gestito** (Step 1 lo genera, Step 4 lo usa). Acceptabile.
- ⚠️ Path postgres mount `/tests/fixtures/...` in T5 — esplicitamente flaggato come "verifica con `docker compose exec`" in T5 Step 3. Acceptabile.

### 3. Type/path consistency

- File path coerenti tra Task: `tests/api-smoke/run-smoke.sh` (T3) ↔ T5 step "Run smoke (Linux)" usa stesso path ✅
- Ambiente Bruno `local`/`staging` consistente ✅
- Versione Bruno `2.15.1` pinata in T1 e referenziata in T3, T4, T5, T6 ✅
- Persona email `smoke-aaron@meepleai.test` consistente ovunque ✅
- Issue numbers (#910, #906, #902-905, #884) corretti vs EPIC body ✅

### 4. Ambiguity check

- "Free-tier user" → esplicitato come `Role="user"` + `Tier="free"` + `IsContributor=false` (T2 step 2 assertion) ✅
- "Soft-launch" → esplicitato come `continue-on-error: true` con condizione di rimozione "30 scenari verdi 2 settimane" (T5, T6) ✅
- "Audit nightly E2E" → esplicitato come grep triggers/concurrency/playwright in T7 step 1 ✅

Plan pronto.

---

## Execution Handoff

Plan saved a `docs/superpowers/plans/2026-05-09-sg0-foundation-implementation.md`.

Per eseguirlo, due opzioni (vedi skill `superpowers:executing-plans` o `superpowers:subagent-driven-development`):

**1. Subagent-Driven (recommended)** — coordinatore principale dispatcha un subagent fresh per ogni task, review tra task, iteration veloce. Best per task indipendenti come T1, T2, T3, T4 (paralleli) e T5, T6, T7 (sequenziali per dipendenze).

**2. Inline Execution** — esegui i task in questa sessione con `superpowers:executing-plans`, batch con checkpoint per review. Best se vuoi vedere ogni step e intervenire on-the-fly.

L'utente sceglie nel turno successivo.
