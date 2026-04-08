# DB State Save/Restore for Migration Consolidation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build three PowerShell scripts (save / reset / restore) and a shared module to safely consolidate EF Core migrations on a local PostgreSQL dev database without losing data, including PDF volume preservation.

**Architecture:** Three independent PowerShell scripts orchestrated by the operator, sharing a `.psm1` PowerShell module for pure functions (parsing, normalization, drift classification). Each script writes its own immutable JSON result file. Pure functions are unit-tested with Pester. Side-effecting steps (`pg_dump`, `dotnet ef`, `docker run`) are smoke-tested manually.

**Tech Stack:** PowerShell 7+, Pester 5+ (testing), PostgreSQL 16 (`pg_dump`/`pg_restore`/`psql`), Docker (for `meepleai-postgres` container and `meepleai_pdf_uploads` volume), `dotnet ef` CLI, Make.

**Spec reference:** `docs/superpowers/specs/2026-04-08-db-state-save-restore-design.md`

---

## File Map

| Task | Create | Modify |
|---|---|---|
| 1 | `infra/db-snapshots/.gitignore`, `infra/scripts/db-snapshot-common.psm1`, `infra/scripts/tests/db-snapshot-common.Tests.ps1` | — |
| 2 | — | `db-snapshot-common.psm1`, `db-snapshot-common.Tests.ps1` (Test-LocalhostHost) |
| 3 | — | `db-snapshot-common.psm1`, `Tests.ps1` (ConvertFrom-SecretFile) |
| 4 | — | `db-snapshot-common.psm1`, `Tests.ps1` (Get-PostgresConfig) |
| 5 | — | `db-snapshot-common.psm1`, `Tests.ps1` (Assert-LocalhostOnly) |
| 6 | — | `db-snapshot-common.psm1`, `Tests.ps1` (Get-RequiredDiskSpaceBytes) |
| 7 | — | `db-snapshot-common.psm1`, `Tests.ps1` (Read-Manifest, Write-Manifest) |
| 8 | — | `db-snapshot-common.psm1`, `Tests.ps1` (Normalize-PgSchema) |
| 9 | — | `db-snapshot-common.psm1`, `Tests.ps1` (Test-SchemaDriftClass) |
| 10 | — | `db-snapshot-common.psm1` (I/O wrappers, no Pester) |
| 11 | `infra/scripts/db-save-state.ps1` | — |
| 12 | — | `db-save-state.ps1` (dump operations) |
| 13 | — | `db-save-state.ps1` (manifest + summary) |
| 14 | `infra/scripts/db-reset-migrations.ps1` | — |
| 15 | — | `db-reset-migrations.ps1` (destructive actions) |
| 16 | `infra/scripts/db-restore-state.ps1` | — |
| 17 | — | `db-restore-state.ps1` (data restore + sequences) |
| 18 | — | `db-restore-state.ps1` (-UseSafetyNet mode) |
| 19 | — | `infra/Makefile` |
| 20 | `docs/operations/migration-consolidation-runbook.md` | `infra/scripts/README.md` |
| 21 | — | — (final integration smoke test) |

---

## Conventions used in this plan

- All paths are relative to repo root unless noted.
- All PowerShell uses `pwsh` (PowerShell 7+), not legacy `powershell.exe`.
- Tests use Pester 5 syntax (`Describe`/`It`/`Should`).
- Each task ends with a commit. Commit messages follow the project format `feat|fix|docs|test|chore(scope): description`.
- Smoke tests assume a running `meepleai-postgres` container (check with `docker ps`).

---

## Task 1: Setup gitignore, module skeleton, and test harness

**Files:**
- Create: `infra/db-snapshots/.gitignore`
- Create: `infra/scripts/db-snapshot-common.psm1`
- Create: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Create `infra/db-snapshots/.gitignore`**

```
*
!.gitignore
```

- [ ] **Step 2: Create empty PowerShell module file `infra/scripts/db-snapshot-common.psm1`**

```powershell
# infra/scripts/db-snapshot-common.psm1
# Shared helpers for db-save-state, db-reset-migrations, db-restore-state.
# Imported via: Import-Module (Join-Path $PSScriptRoot 'db-snapshot-common.psm1') -Force

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Functions are added below by subsequent tasks ---

# --- Exports ---
Export-ModuleMember -Function @()
```

- [ ] **Step 3: Create test file `infra/scripts/tests/db-snapshot-common.Tests.ps1`**

```powershell
# infra/scripts/tests/db-snapshot-common.Tests.ps1
# Pester 5 tests for pure functions in db-snapshot-common.psm1
# Run: pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"

BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..' 'db-snapshot-common.psm1'
    Import-Module $modulePath -Force
}

Describe 'db-snapshot-common module loads' {
    It 'imports without errors' {
        Get-Module -Name 'db-snapshot-common' | Should -Not -BeNullOrEmpty
    }
}
```

- [ ] **Step 4: Verify Pester is installed and the placeholder test runs**

Run:
```bash
pwsh -c "if (-not (Get-Module -ListAvailable Pester | Where-Object Version -ge ([version]'5.0.0'))) { Install-Module Pester -Force -SkipPublisherCheck -Scope CurrentUser }; Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected output: `Tests Passed: 1, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add infra/db-snapshots/.gitignore infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): scaffold db-snapshot module and pester test harness"
```

---

## Task 2: `Test-LocalhostHost` (TDD)

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`
- Modify: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Add the failing tests**

Append to `db-snapshot-common.Tests.ps1`:

```powershell
Describe 'Test-LocalhostHost' {
    It 'returns true for "localhost"' {
        Test-LocalhostHost -PgHost 'localhost' | Should -BeTrue
    }
    It 'returns true for "127.0.0.1"' {
        Test-LocalhostHost -PgHost '127.0.0.1' | Should -BeTrue
    }
    It 'returns false for "staging.meepleai.app"' {
        Test-LocalhostHost -PgHost 'staging.meepleai.app' | Should -BeFalse
    }
    It 'returns false for "postgres" (docker internal name)' {
        Test-LocalhostHost -PgHost 'postgres' | Should -BeFalse
    }
    It 'returns false for empty string' {
        Test-LocalhostHost -PgHost '' | Should -BeFalse
    }
    It 'is case-insensitive for "LOCALHOST"' {
        Test-LocalhostHost -PgHost 'LOCALHOST' | Should -BeTrue
    }
}
```

- [ ] **Step 2: Run the test, expect failure**

Run:
```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: 6 failures with `CommandNotFoundException: Test-LocalhostHost`.

- [ ] **Step 3: Implement `Test-LocalhostHost` in the module**

Insert into `db-snapshot-common.psm1` before the `Export-ModuleMember` line:

```powershell
function Test-LocalhostHost {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$PgHost
    )
    if ([string]::IsNullOrWhiteSpace($PgHost)) { return $false }
    $normalized = $PgHost.Trim().ToLowerInvariant()
    return ($normalized -eq 'localhost') -or ($normalized -eq '127.0.0.1')
}
```

Update the `Export-ModuleMember` line:

```powershell
Export-ModuleMember -Function @('Test-LocalhostHost')
```

- [ ] **Step 4: Run the test, expect pass**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: `Tests Passed: 7, Failed: 0` (1 module-load test + 6 new ones).

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): add Test-LocalhostHost helper with pester tests"
```

---

## Task 3: `ConvertFrom-SecretFile` (TDD)

Parses a `*.secret` file (KEY=VALUE format, one per line, supports comments) into a hashtable. Used by all scripts to read `database.secret` and `storage.secret`.

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`
- Modify: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Add the failing tests**

Append to `db-snapshot-common.Tests.ps1`:

```powershell
Describe 'ConvertFrom-SecretFile' {
    BeforeEach {
        $script:tmpFile = New-TemporaryFile
    }
    AfterEach {
        if (Test-Path $script:tmpFile) { Remove-Item $script:tmpFile -Force }
    }

    It 'parses simple KEY=VALUE pairs' {
        @(
            'POSTGRES_USER=meepleai',
            'POSTGRES_PASSWORD=secret123',
            'POSTGRES_DB=meepleai_db'
        ) | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result['POSTGRES_USER'] | Should -Be 'meepleai'
        $result['POSTGRES_PASSWORD'] | Should -Be 'secret123'
        $result['POSTGRES_DB'] | Should -Be 'meepleai_db'
    }

    It 'ignores blank lines and comments starting with #' {
        @(
            '# This is a comment',
            '',
            'KEY=value',
            '   # indented comment',
            ''
        ) | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result.Count | Should -Be 1
        $result['KEY'] | Should -Be 'value'
    }

    It 'handles values containing equals signs' {
        'CONN=Host=localhost;Port=5432' | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result['CONN'] | Should -Be 'Host=localhost;Port=5432'
    }

    It 'trims whitespace around keys and values' {
        '  KEY  =  value  ' | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result['KEY'] | Should -Be 'value'
    }

    It 'throws if the file does not exist' {
        { ConvertFrom-SecretFile -Path '/nonexistent/file.secret' } | Should -Throw -ErrorId '*'
    }

    It 'returns an empty hashtable for an empty file' {
        '' | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result.Count | Should -Be 0
    }
}
```

- [ ] **Step 2: Run tests, expect failure**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: 6 failures with `CommandNotFoundException: ConvertFrom-SecretFile`.

- [ ] **Step 3: Implement `ConvertFrom-SecretFile`**

Insert into `db-snapshot-common.psm1` before `Export-ModuleMember`:

```powershell
function ConvertFrom-SecretFile {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Secret file not found: $Path"
    }
    $result = @{}
    Get-Content -LiteralPath $Path | ForEach-Object {
        $line = $_.Trim()
        if ([string]::IsNullOrWhiteSpace($line)) { return }
        if ($line.StartsWith('#')) { return }
        $eqIndex = $line.IndexOf('=')
        if ($eqIndex -lt 1) { return }
        $key = $line.Substring(0, $eqIndex).Trim()
        $value = $line.Substring($eqIndex + 1).Trim()
        $result[$key] = $value
    }
    return $result
}
```

Update exports:

```powershell
Export-ModuleMember -Function @('Test-LocalhostHost', 'ConvertFrom-SecretFile')
```

- [ ] **Step 4: Run tests, expect pass**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: `Tests Passed: 13, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): add ConvertFrom-SecretFile parser with pester tests"
```

---

## Task 4: `Get-PostgresConfig` (TDD)

Returns a config object with `Host`, `Port`, `Db`, `User`, `Password`, applying defaults (`localhost`, `5432`, `meepleai_db`).

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`
- Modify: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Add the failing tests**

Append to `db-snapshot-common.Tests.ps1`:

```powershell
Describe 'Get-PostgresConfig' {
    BeforeEach {
        $script:tmpFile = New-TemporaryFile
    }
    AfterEach {
        if (Test-Path $script:tmpFile) { Remove-Item $script:tmpFile -Force }
    }

    It 'returns config with defaults when only credentials are present' {
        @(
            'POSTGRES_USER=meepleai',
            'POSTGRES_PASSWORD=secret'
        ) | Set-Content $script:tmpFile
        $cfg = Get-PostgresConfig -SecretPath $script:tmpFile
        $cfg.Host | Should -Be 'localhost'
        $cfg.Port | Should -Be 5432
        $cfg.Db | Should -Be 'meepleai_db'
        $cfg.User | Should -Be 'meepleai'
        $cfg.Password | Should -Be 'secret'
    }

    It 'overrides defaults with explicit secret values' {
        @(
            'POSTGRES_HOST=otherhost',
            'POSTGRES_PORT=6543',
            'POSTGRES_DB=other_db',
            'POSTGRES_USER=user',
            'POSTGRES_PASSWORD=pw'
        ) | Set-Content $script:tmpFile
        $cfg = Get-PostgresConfig -SecretPath $script:tmpFile
        $cfg.Host | Should -Be 'otherhost'
        $cfg.Port | Should -Be 6543
        $cfg.Db | Should -Be 'other_db'
        $cfg.User | Should -Be 'user'
        $cfg.Password | Should -Be 'pw'
    }

    It 'throws when POSTGRES_USER is missing' {
        @(
            'POSTGRES_PASSWORD=secret'
        ) | Set-Content $script:tmpFile
        { Get-PostgresConfig -SecretPath $script:tmpFile } | Should -Throw '*POSTGRES_USER*'
    }

    It 'throws when POSTGRES_PASSWORD is missing' {
        @(
            'POSTGRES_USER=meepleai'
        ) | Set-Content $script:tmpFile
        { Get-PostgresConfig -SecretPath $script:tmpFile } | Should -Throw '*POSTGRES_PASSWORD*'
    }
}
```

- [ ] **Step 2: Run tests, expect failure**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: 4 failures with `CommandNotFoundException: Get-PostgresConfig`.

- [ ] **Step 3: Implement `Get-PostgresConfig`**

Insert into `db-snapshot-common.psm1`:

```powershell
function Get-PostgresConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SecretPath
    )
    $secrets = ConvertFrom-SecretFile -Path $SecretPath
    if (-not $secrets.ContainsKey('POSTGRES_USER')) {
        throw "Missing POSTGRES_USER in $SecretPath"
    }
    if (-not $secrets.ContainsKey('POSTGRES_PASSWORD')) {
        throw "Missing POSTGRES_PASSWORD in $SecretPath"
    }
    return [pscustomobject]@{
        Host     = if ($secrets.ContainsKey('POSTGRES_HOST') -and -not [string]::IsNullOrWhiteSpace($secrets['POSTGRES_HOST'])) { $secrets['POSTGRES_HOST'] } else { 'localhost' }
        Port     = if ($secrets.ContainsKey('POSTGRES_PORT') -and -not [string]::IsNullOrWhiteSpace($secrets['POSTGRES_PORT'])) { [int]$secrets['POSTGRES_PORT'] } else { 5432 }
        Db       = if ($secrets.ContainsKey('POSTGRES_DB') -and -not [string]::IsNullOrWhiteSpace($secrets['POSTGRES_DB'])) { $secrets['POSTGRES_DB'] } else { 'meepleai_db' }
        User     = $secrets['POSTGRES_USER']
        Password = $secrets['POSTGRES_PASSWORD']
    }
}
```

Update exports:

```powershell
Export-ModuleMember -Function @('Test-LocalhostHost', 'ConvertFrom-SecretFile', 'Get-PostgresConfig')
```

- [ ] **Step 4: Run tests, expect pass**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: `Tests Passed: 17, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): add Get-PostgresConfig with defaults and validation"
```

---

## Task 5: `Assert-LocalhostOnly` (TDD)

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`
- Modify: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Add the failing tests**

Append to `db-snapshot-common.Tests.ps1`:

```powershell
Describe 'Assert-LocalhostOnly' {
    It 'does not throw for localhost config' {
        $cfg = [pscustomobject]@{ Host = 'localhost'; Port = 5432; Db = 'meepleai_db'; User = 'u'; Password = 'p' }
        { Assert-LocalhostOnly -Config $cfg } | Should -Not -Throw
    }
    It 'does not throw for 127.0.0.1 config' {
        $cfg = [pscustomobject]@{ Host = '127.0.0.1'; Port = 5432; Db = 'meepleai_db'; User = 'u'; Password = 'p' }
        { Assert-LocalhostOnly -Config $cfg } | Should -Not -Throw
    }
    It 'throws for staging.meepleai.app config' {
        $cfg = [pscustomobject]@{ Host = 'staging.meepleai.app'; Port = 5432; Db = 'meepleai_db'; User = 'u'; Password = 'p' }
        { Assert-LocalhostOnly -Config $cfg } | Should -Throw '*staging.meepleai.app*'
    }
    It 'throws and includes the host name in the error' {
        $cfg = [pscustomobject]@{ Host = 'prod.example.com'; Port = 5432; Db = 'meepleai_db'; User = 'u'; Password = 'p' }
        { Assert-LocalhostOnly -Config $cfg } | Should -Throw '*prod.example.com*'
    }
}
```

- [ ] **Step 2: Run tests, expect failure**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: 4 failures with `CommandNotFoundException: Assert-LocalhostOnly`.

- [ ] **Step 3: Implement `Assert-LocalhostOnly`**

Insert into `db-snapshot-common.psm1`:

```powershell
function Assert-LocalhostOnly {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config
    )
    if (-not (Test-LocalhostHost -PgHost $Config.Host)) {
        throw "REFUSED: target host is '$($Config.Host)' but only 'localhost' or '127.0.0.1' are allowed for safety. This script is for LOCAL DEV ONLY."
    }
}
```

Update exports:

```powershell
Export-ModuleMember -Function @('Test-LocalhostHost', 'ConvertFrom-SecretFile', 'Get-PostgresConfig', 'Assert-LocalhostOnly')
```

- [ ] **Step 4: Run tests, expect pass**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: `Tests Passed: 21, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): add Assert-LocalhostOnly safety guard"
```

---

## Task 6: `Get-RequiredDiskSpaceBytes` (TDD)

Pure calculation: given DB size in bytes, optional volume size in bytes, return required free space with 10% safety margin and 64 MB overhead.

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`
- Modify: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Add the failing tests**

Append:

```powershell
Describe 'Get-RequiredDiskSpaceBytes' {
    It 'returns DB size + 64 MB overhead + 10% safety margin when no volume' {
        $required = Get-RequiredDiskSpaceBytes -DbSizeBytes 100000000 -VolumeSizeBytes 0
        $expected = [math]::Ceiling((100000000 + 67108864) * 1.1)
        $required | Should -Be $expected
    }
    It 'includes volume size when provided' {
        $required = Get-RequiredDiskSpaceBytes -DbSizeBytes 100000000 -VolumeSizeBytes 50000000
        $expected = [math]::Ceiling((100000000 + 50000000 + 67108864) * 1.1)
        $required | Should -Be $expected
    }
    It 'returns at least the overhead + safety margin for zero db' {
        $required = Get-RequiredDiskSpaceBytes -DbSizeBytes 0 -VolumeSizeBytes 0
        $expected = [math]::Ceiling((0 + 67108864) * 1.1)
        $required | Should -Be $expected
    }
    It 'throws for negative DB size' {
        { Get-RequiredDiskSpaceBytes -DbSizeBytes -1 -VolumeSizeBytes 0 } | Should -Throw '*non-negative*'
    }
}
```

- [ ] **Step 2: Run tests, expect failure**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

- [ ] **Step 3: Implement `Get-RequiredDiskSpaceBytes`**

```powershell
function Get-RequiredDiskSpaceBytes {
    [CmdletBinding()]
    [OutputType([long])]
    param(
        [Parameter(Mandatory)]
        [long]$DbSizeBytes,
        [Parameter(Mandatory)]
        [long]$VolumeSizeBytes
    )
    if ($DbSizeBytes -lt 0) { throw "DbSizeBytes must be non-negative, got $DbSizeBytes" }
    if ($VolumeSizeBytes -lt 0) { throw "VolumeSizeBytes must be non-negative, got $VolumeSizeBytes" }
    $overheadBytes = 64L * 1024 * 1024
    $rawTotal = $DbSizeBytes + $VolumeSizeBytes + $overheadBytes
    return [long][math]::Ceiling($rawTotal * 1.1)
}
```

Update exports to include `Get-RequiredDiskSpaceBytes`.

- [ ] **Step 4: Run tests, expect pass**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: `Tests Passed: 25`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): add Get-RequiredDiskSpaceBytes pure calculator"
```

---

## Task 7: `Read-Manifest` and `Write-Manifest` (TDD)

JSON read/write helpers with atomic write (write to temp file then rename).

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`
- Modify: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Add the failing tests**

Append:

```powershell
Describe 'Manifest read/write' {
    BeforeEach {
        $script:tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tmpDir | Out-Null
        $script:tmpFile = Join-Path $script:tmpDir 'manifest.json'
    }
    AfterEach {
        Remove-Item $script:tmpDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'round-trips a simple manifest' {
        $obj = [pscustomobject]@{
            schemaVersion = 1
            createdAt     = '2026-04-08T14:35:22Z'
            database      = 'meepleai_db'
            sanitize      = $false
        }
        Write-Manifest -Path $script:tmpFile -Object $obj
        $loaded = Read-Manifest -Path $script:tmpFile
        $loaded.schemaVersion | Should -Be 1
        $loaded.database | Should -Be 'meepleai_db'
        $loaded.sanitize | Should -Be $false
    }

    It 'preserves nested objects' {
        $obj = [pscustomobject]@{
            files = [pscustomobject]@{
                safetyNet = [pscustomobject]@{ name = 'a'; sha256 = 'b'; bytes = 123 }
            }
        }
        Write-Manifest -Path $script:tmpFile -Object $obj
        $loaded = Read-Manifest -Path $script:tmpFile
        $loaded.files.safetyNet.name | Should -Be 'a'
        $loaded.files.safetyNet.sha256 | Should -Be 'b'
        $loaded.files.safetyNet.bytes | Should -Be 123
    }

    It 'Read-Manifest throws when file does not exist' {
        { Read-Manifest -Path (Join-Path $script:tmpDir 'missing.json') } | Should -Throw '*not found*'
    }

    It 'Read-Manifest throws on invalid JSON' {
        'not json' | Set-Content $script:tmpFile
        { Read-Manifest -Path $script:tmpFile } | Should -Throw
    }

    It 'Write-Manifest is atomic (no partial file on success)' {
        $obj = [pscustomobject]@{ a = 1 }
        Write-Manifest -Path $script:tmpFile -Object $obj
        Test-Path $script:tmpFile | Should -BeTrue
        $tmpSibling = "$script:tmpFile.tmp"
        Test-Path $tmpSibling | Should -BeFalse
    }
}
```

- [ ] **Step 2: Run tests, expect failure**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

- [ ] **Step 3: Implement `Read-Manifest` and `Write-Manifest`**

Add to module:

```powershell
function Read-Manifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Manifest not found: $Path"
    }
    $raw = Get-Content -LiteralPath $Path -Raw
    return $raw | ConvertFrom-Json
}

function Write-Manifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [object]$Object
    )
    $tmpPath = "$Path.tmp"
    $json = $Object | ConvertTo-Json -Depth 10
    Set-Content -LiteralPath $tmpPath -Value $json -Encoding UTF8
    Move-Item -LiteralPath $tmpPath -Destination $Path -Force
}
```

Update exports to include `Read-Manifest`, `Write-Manifest`.

- [ ] **Step 4: Run tests, expect pass**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: `Tests Passed: 30`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): add Read-Manifest and Write-Manifest with atomic write"
```

---

## Task 8: `Normalize-PgSchema` (TDD)

Normalizes a `pg_dump -s` output for diff: strips comments, `SET ...`, `SELECT pg_catalog.set_config(...)`, version headers; collapses whitespace; sorts top-level statements.

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`
- Modify: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Add the failing tests**

Append:

```powershell
Describe 'Normalize-PgSchema' {
    It 'strips line comments starting with --' {
        $input = @"
-- Dumped from database version 16.3
CREATE TABLE foo (id int);
"@
        $result = Normalize-PgSchema -Sql $input
        $result | Should -Not -Match '--'
        $result | Should -Match 'CREATE TABLE foo'
    }

    It 'strips SET statements' {
        $input = @"
SET client_min_messages = warning;
SET statement_timeout = 0;
CREATE TABLE foo (id int);
"@
        $result = Normalize-PgSchema -Sql $input
        $result | Should -Not -Match 'SET '
        $result | Should -Match 'CREATE TABLE foo'
    }

    It 'strips SELECT pg_catalog.set_config calls' {
        $input = @"
SELECT pg_catalog.set_config('search_path', '', false);
CREATE TABLE foo (id int);
"@
        $result = Normalize-PgSchema -Sql $input
        $result | Should -Not -Match 'set_config'
    }

    It 'collapses multiple blank lines into one' {
        $input = "CREATE TABLE foo (id int);`n`n`n`nCREATE TABLE bar (id int);"
        $result = Normalize-PgSchema -Sql $input
        ($result -split "`n").Count | Should -BeLessThan 4
    }

    It 'sorts CREATE TABLE statements alphabetically by name' {
        $input = @"
CREATE TABLE zebra (id int);
CREATE TABLE alpha (id int);
"@
        $result = Normalize-PgSchema -Sql $input
        $alphaIdx = $result.IndexOf('CREATE TABLE alpha')
        $zebraIdx = $result.IndexOf('CREATE TABLE zebra')
        $alphaIdx | Should -BeLessThan $zebraIdx
    }

    It 'is idempotent (normalize twice = normalize once)' {
        $input = @"
SET client_min_messages = warning;
-- comment
CREATE TABLE foo (id int);
CREATE TABLE bar (id int);
"@
        $once = Normalize-PgSchema -Sql $input
        $twice = Normalize-PgSchema -Sql $once
        $twice | Should -Be $once
    }

    It 'preserves CREATE TABLE column definitions' {
        $input = "CREATE TABLE foo (id int NOT NULL, name text);"
        $result = Normalize-PgSchema -Sql $input
        $result | Should -Match 'id int NOT NULL'
        $result | Should -Match 'name text'
    }
}
```

- [ ] **Step 2: Run tests, expect failure**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

- [ ] **Step 3: Implement `Normalize-PgSchema`**

```powershell
function Normalize-PgSchema {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Sql
    )
    if ([string]::IsNullOrWhiteSpace($Sql)) { return '' }

    # Phase 1: line-by-line filter
    $kept = New-Object System.Collections.Generic.List[string]
    foreach ($rawLine in ($Sql -split "`r?`n")) {
        $line = $rawLine.TrimEnd()
        if ($line -match '^\s*--') { continue }                        # comment
        if ($line -match '^\s*SET\s') { continue }                     # SET statements
        if ($line -match '^\s*SELECT\s+pg_catalog\.set_config') { continue }
        $kept.Add($line)
    }

    # Phase 2: collapse runs of blank lines into a single blank
    $compact = New-Object System.Collections.Generic.List[string]
    $prevBlank = $false
    foreach ($line in $kept) {
        $isBlank = [string]::IsNullOrWhiteSpace($line)
        if ($isBlank -and $prevBlank) { continue }
        $compact.Add($line)
        $prevBlank = $isBlank
    }

    # Phase 3: sort top-level statements alphabetically
    # Statements are separated by lines ending in `;` followed by blank.
    # We split into chunks, then sort by first non-empty line.
    $joined = ($compact -join "`n").Trim()
    if ($joined.Length -eq 0) { return '' }

    # Split into statements by semicolon-then-newline boundary
    $statements = New-Object System.Collections.Generic.List[string]
    $current = New-Object System.Text.StringBuilder
    foreach ($ch in $joined.ToCharArray()) {
        [void]$current.Append($ch)
        if ($ch -eq ';') {
            $stmt = $current.ToString().Trim()
            if ($stmt.Length -gt 0) { $statements.Add($stmt) }
            [void]$current.Clear()
        }
    }
    $tail = $current.ToString().Trim()
    if ($tail.Length -gt 0) { $statements.Add($tail) }

    $sorted = $statements | Sort-Object { $_ }
    return ($sorted -join "`n`n")
}
```

Update exports to include `Normalize-PgSchema`.

- [ ] **Step 4: Run tests, expect pass**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: `Tests Passed: 37`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): add Normalize-PgSchema for diff-friendly schema comparison"
```

---

## Task 9: `Test-SchemaDriftClass` (TDD)

Classifies the diff between two normalized schemas as `identical | minor | significant`.

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`
- Modify: `infra/scripts/tests/db-snapshot-common.Tests.ps1`

- [ ] **Step 1: Add the failing tests**

Append:

```powershell
Describe 'Test-SchemaDriftClass' {
    It 'returns identical for byte-equal schemas' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'identical'
    }
    It 'returns identical for whitespace-only differences (already normalized)' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'identical'
    }
    It 'returns significant for an added column' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int, name text);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for a removed column' {
        $a = "CREATE TABLE foo (id int, name text);"
        $b = "CREATE TABLE foo (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for a renamed column' {
        $a = "CREATE TABLE foo (id int, name text);"
        $b = "CREATE TABLE foo (id int, full_name text);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for an added table' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int);`n`nCREATE TABLE bar (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for a removed table' {
        $a = "CREATE TABLE foo (id int);`n`nCREATE TABLE bar (id int);"
        $b = "CREATE TABLE foo (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for a column type change' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id bigint);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns minor for trailing-whitespace-only diff that survived normalization' {
        # Normalization handles this, so we simulate the rare case where one has a trailing space
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int);   "
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -BeIn @('identical','minor')
    }
}
```

- [ ] **Step 2: Run tests, expect failure**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

- [ ] **Step 3: Implement `Test-SchemaDriftClass`**

```powershell
function Test-SchemaDriftClass {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$PreSchema,
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$PostSchema
    )

    # Byte-equal short circuit
    if ($PreSchema -ceq $PostSchema) { return 'identical' }

    # Whitespace-only difference → minor (or identical after trim)
    $preTrimmed = ($PreSchema -replace '\s+', ' ').Trim()
    $postTrimmed = ($PostSchema -replace '\s+', ' ').Trim()
    if ($preTrimmed -ceq $postTrimmed) { return 'minor' }

    # Anything else is significant
    return 'significant'
}
```

Update exports to include `Test-SchemaDriftClass`.

- [ ] **Step 4: Run tests, expect pass**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: `Tests Passed: 46`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1 infra/scripts/tests/db-snapshot-common.Tests.ps1
git commit -m "feat(infra): add Test-SchemaDriftClass classifier"
```

---

## Task 10: I/O wrapper functions in module

Add the side-effecting helpers used by the three scripts. These wrap native commands and Docker calls; they are smoke-tested at the script level rather than unit-tested.

**Files:**
- Modify: `infra/scripts/db-snapshot-common.psm1`

- [ ] **Step 1: Add `Write-Timestamped`, `Get-FileSha256`, and `Confirm-UserAction`**

Insert into the module:

```powershell
function Write-Timestamped {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        [string]$LogFile = $null
    )
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $Message"
    Write-Host $line
    if ($LogFile) {
        Add-Content -LiteralPath $LogFile -Value $line
    }
}

function Get-FileSha256 {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "File not found: $Path"
    }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Confirm-UserAction {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Prompt,
        [switch]$Force
    )
    if ($Force) { return $true }
    Write-Host ''
    Write-Host $Prompt -ForegroundColor Yellow
    Write-Host ''
    Write-Host -NoNewline "Type 'yes' to continue: "
    $answer = Read-Host
    return ($answer -ceq 'yes')
}
```

- [ ] **Step 2: Add `Invoke-PgDump`, `Invoke-PgRestore`, `Invoke-Psql`**

```powershell
function Invoke-PgDump {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )
    $env:PGPASSWORD = $Config.Password
    try {
        $allArgs = @(
            '-h', $Config.Host,
            '-p', $Config.Port.ToString(),
            '-U', $Config.User,
            '-d', $Config.Db
        ) + $Arguments
        & pg_dump @allArgs
        if ($LASTEXITCODE -ne 0) {
            throw "pg_dump failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Invoke-PgRestore {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )
    $env:PGPASSWORD = $Config.Password
    try {
        $allArgs = @(
            '-h', $Config.Host,
            '-p', $Config.Port.ToString(),
            '-U', $Config.User,
            '-d', $Config.Db
        ) + $Arguments
        & pg_restore @allArgs
        if ($LASTEXITCODE -ne 0) {
            throw "pg_restore failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Invoke-Psql {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config,
        [Parameter(Mandatory)]
        [string]$Sql,
        [string]$Database = $null
    )
    $env:PGPASSWORD = $Config.Password
    try {
        $db = if ($Database) { $Database } else { $Config.Db }
        $output = & psql -h $Config.Host -p $Config.Port -U $Config.User -d $db -t -A -c $Sql
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed with exit code $LASTEXITCODE"
        }
        return $output
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}
```

- [ ] **Step 3: Add Docker volume helpers**

```powershell
function Test-DockerVolumeExists {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$VolumeName
    )
    $null = & docker volume inspect $VolumeName 2>$null
    return ($LASTEXITCODE -eq 0)
}

function Get-DockerVolumeStats {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$VolumeName
    )
    if (-not (Test-DockerVolumeExists -VolumeName $VolumeName)) {
        return [pscustomobject]@{ Exists = $false; FileCount = 0; SizeBytes = 0L }
    }
    # Use a tiny alpine container to du -sb the volume contents
    $sizeRaw = & docker run --rm -v "${VolumeName}:/data:ro" alpine sh -c 'du -sb /data 2>/dev/null | cut -f1'
    $countRaw = & docker run --rm -v "${VolumeName}:/data:ro" alpine sh -c 'find /data -type f 2>/dev/null | wc -l'
    return [pscustomobject]@{
        Exists    = $true
        FileCount = [int]($countRaw.Trim())
        SizeBytes = [long]($sizeRaw.Trim())
    }
}

function Get-StorageProvider {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$StorageSecretPath
    )
    if (-not (Test-Path -LiteralPath $StorageSecretPath)) {
        return 'local'  # default if no secret file
    }
    $secrets = ConvertFrom-SecretFile -Path $StorageSecretPath
    if ($secrets.ContainsKey('STORAGE_PROVIDER')) {
        return $secrets['STORAGE_PROVIDER'].ToLowerInvariant()
    }
    return 'local'
}
```

- [ ] **Step 4: Add DB introspection helpers**

```powershell
function Get-DbSizeBytes {
    [CmdletBinding()]
    [OutputType([long])]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config
    )
    $sql = "SELECT pg_database_size('$($Config.Db)');"
    $result = Invoke-Psql -Config $Config -Sql $sql
    return [long]($result.Trim())
}

function Get-XactCommitCount {
    [CmdletBinding()]
    [OutputType([long])]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config
    )
    $sql = "SELECT xact_commit FROM pg_stat_database WHERE datname = '$($Config.Db)';"
    $result = Invoke-Psql -Config $Config -Sql $sql
    return [long]($result.Trim())
}

function Get-ActiveBackendConnections {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config
    )
    $sql = @"
SELECT pid || '|' || COALESCE(application_name,'') || '|' || COALESCE(client_addr::text,'') || '|' || state
FROM pg_stat_activity
WHERE datname = '$($Config.Db)'
  AND pid <> pg_backend_pid()
  AND (application_name LIKE 'Npgsql%' OR application_name LIKE '%api%' OR usename = '$($Config.User)')
"@
    $output = Invoke-Psql -Config $Config -Sql $sql
    if ([string]::IsNullOrWhiteSpace($output)) { return @() }
    return ($output -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}
```

- [ ] **Step 5: Update `Export-ModuleMember` to export everything**

Replace the final `Export-ModuleMember` line with:

```powershell
Export-ModuleMember -Function @(
    'Test-LocalhostHost',
    'ConvertFrom-SecretFile',
    'Get-PostgresConfig',
    'Assert-LocalhostOnly',
    'Get-RequiredDiskSpaceBytes',
    'Read-Manifest',
    'Write-Manifest',
    'Normalize-PgSchema',
    'Test-SchemaDriftClass',
    'Write-Timestamped',
    'Get-FileSha256',
    'Confirm-UserAction',
    'Invoke-PgDump',
    'Invoke-PgRestore',
    'Invoke-Psql',
    'Test-DockerVolumeExists',
    'Get-DockerVolumeStats',
    'Get-StorageProvider',
    'Get-DbSizeBytes',
    'Get-XactCommitCount',
    'Get-ActiveBackendConnections'
)
```

- [ ] **Step 6: Verify the module imports cleanly and Pester still passes**

```bash
pwsh -c "Import-Module ./infra/scripts/db-snapshot-common.psm1 -Force; Get-Command -Module db-snapshot-common | Select-Object -ExpandProperty Name"
```

Expected: list of all 21 exported functions.

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: still `Tests Passed: 46, Failed: 0`.

- [ ] **Step 7: Smoke test `Get-DbSizeBytes` against the running local DB**

Prerequisite: `meepleai-postgres` container running.

```bash
pwsh -c "Import-Module ./infra/scripts/db-snapshot-common.psm1 -Force; \$cfg = Get-PostgresConfig -SecretPath ./infra/secrets/database.secret; Get-DbSizeBytes -Config \$cfg"
```

Expected: a positive integer (DB size in bytes).

- [ ] **Step 8: Commit**

```bash
git add infra/scripts/db-snapshot-common.psm1
git commit -m "feat(infra): add I/O wrapper functions to db-snapshot-common module"
```

---

## Task 11: `db-save-state.ps1` — skeleton + pre-flight checks

**Files:**
- Create: `infra/scripts/db-save-state.ps1`

- [ ] **Step 1: Create the script with parameters and pre-flight only**

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Saves the local PostgreSQL state as a timestamped snapshot before a migration consolidation.
.DESCRIPTION
    Creates infra/db-snapshots/<timestamp>/ containing safety-net.dump (full),
    data.dump (data-only), schema-pre.sql, rowcounts-pre.tsv, optional pdf_uploads.tar.gz,
    and manifest.json. Refuses to run against non-localhost hosts.
.PARAMETER Sanitize
    Exclude sensitive tables (Users, Sessions, RefreshTokens, ApiKeys, AuditLogs)
    from data.dump only. The safety-net.dump always contains all data.
.PARAMETER IncludePdfVolume
    Include the meepleai_pdf_uploads Docker volume. Defaults to true if local storage
    is detected, false if S3 storage.
.PARAMETER SnapshotRoot
    Directory under which the timestamped snapshot is created.
.EXAMPLE
    pwsh infra/scripts/db-save-state.ps1
.EXAMPLE
    pwsh infra/scripts/db-save-state.ps1 -Sanitize
#>
[CmdletBinding()]
param(
    [switch]$Sanitize,
    [Nullable[bool]]$IncludePdfVolume = $null,
    [string]$SnapshotRoot = (Join-Path $PSScriptRoot '..' 'db-snapshots'),
    [string]$SecretFile = (Join-Path $PSScriptRoot '..' 'secrets' 'database.secret'),
    [string]$StorageSecretFile = (Join-Path $PSScriptRoot '..' 'secrets' 'storage.secret'),
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'db-snapshot-common.psm1') -Force

$SENSITIVE_TABLES = @(
    'Users', 'Sessions', 'RefreshTokens', 'ApiKeys', 'AuditLogs'
)

# ============================================================================
# Pre-flight
# ============================================================================

Write-Timestamped '=== db-save-state ==='

# Check 1: pg_dump and psql available
foreach ($tool in @('pg_dump', 'psql')) {
    $cmd = Get-Command $tool -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "Required tool not found in PATH: $tool"
    }
}
Write-Timestamped '[1/9] pg_dump and psql found'

# Check 2-3: secret file + load config
if (-not (Test-Path -LiteralPath $SecretFile)) {
    throw "Secret file not found: $SecretFile"
}
$cfg = Get-PostgresConfig -SecretPath $SecretFile
Write-Timestamped "[2/9] Loaded config for db '$($cfg.Db)' on $($cfg.Host):$($cfg.Port)"

# Check 4: localhost-only enforcement
Assert-LocalhostOnly -Config $cfg
Write-Timestamped '[3/9] Localhost host check passed'

# Check 5: PostgreSQL reachable
$null = Invoke-Psql -Config $cfg -Sql 'SELECT 1;'
Write-Timestamped '[4/9] PostgreSQL reachable'

# Check 6: SnapshotRoot writable
if (-not (Test-Path -LiteralPath $SnapshotRoot)) {
    New-Item -ItemType Directory -Path $SnapshotRoot -Force | Out-Null
}
Write-Timestamped "[5/9] Snapshot root writable: $SnapshotRoot"

# Check 7: Storage provider detection + PDF volume probe
$storageProvider = Get-StorageProvider -StorageSecretPath $StorageSecretFile
$pdfVolumeStats = $null
if ($storageProvider -eq 'local') {
    $pdfVolumeStats = Get-DockerVolumeStats -VolumeName 'meepleai_pdf_uploads'
    Write-Timestamped "[6/9] Storage=local, PDF volume: exists=$($pdfVolumeStats.Exists), files=$($pdfVolumeStats.FileCount), size=$($pdfVolumeStats.SizeBytes) bytes"
} else {
    Write-Timestamped "[6/9] Storage=$storageProvider (PDFs on remote, not affected)"
}

# Resolve IncludePdfVolume default
if ($null -eq $IncludePdfVolume) {
    $IncludePdfVolume = ($storageProvider -eq 'local' -and $pdfVolumeStats -and $pdfVolumeStats.Exists -and $pdfVolumeStats.FileCount -gt 0)
}

# Check 8: Disk space
$dbSize = Get-DbSizeBytes -Config $cfg
$volSize = if ($IncludePdfVolume -and $pdfVolumeStats) { $pdfVolumeStats.SizeBytes } else { 0L }
$requiredBytes = Get-RequiredDiskSpaceBytes -DbSizeBytes $dbSize -VolumeSizeBytes $volSize
$drive = (Get-Item $SnapshotRoot).PSDrive
$freeBytes = $drive.Free
if ($freeBytes -lt $requiredBytes) {
    throw "Not enough free disk space on $($drive.Name): need $requiredBytes bytes, have $freeBytes bytes"
}
Write-Timestamped "[7/9] Disk space OK (need $requiredBytes bytes, have $freeBytes bytes)"

# Check 9: Active backend connections
$activeConns = Get-ActiveBackendConnections -Config $cfg
if ($activeConns.Count -gt 0) {
    Write-Host ''
    Write-Host '⚠ Active backend connections detected:' -ForegroundColor Yellow
    foreach ($c in $activeConns) { Write-Host "    $c" }
    Write-Host ''
    if (-not (Confirm-UserAction -Prompt "Concurrent writes during pg_dump can produce inconsistent business state. Stop the API container first, or continue at your own risk." -Force:$Force)) {
        throw 'Aborted by user'
    }
}
Write-Timestamped '[8/9] Backend connection check complete'

# All checks passed
Write-Timestamped '[9/9] Pre-flight checks complete'

# Placeholder for next task
Write-Timestamped 'TODO: dump operations (Task 12)'
```

- [ ] **Step 2: Make the script syntactically valid (parse-only check)**

```bash
pwsh -c "& { . infra/scripts/db-save-state.ps1 -ErrorAction SilentlyContinue; exit 0 } *> $null; pwsh -NoProfile -Command '\$null = [scriptblock]::Create((Get-Content infra/scripts/db-save-state.ps1 -Raw))'"
```

Expected: no parse errors.

A more reliable parse-only check:

```bash
pwsh -c "[System.Management.Automation.Language.Parser]::ParseFile('infra/scripts/db-save-state.ps1', [ref]\$null, [ref]\$null) | Out-Null; Write-Host 'Parse OK'"
```

Expected: `Parse OK`.

- [ ] **Step 3: Smoke test pre-flight on the local dev DB**

Prerequisite: `meepleai-postgres` container running, `database.secret` configured.

```bash
pwsh ./infra/scripts/db-save-state.ps1
```

Expected output: all 9 pre-flight checks pass, ends with `TODO: dump operations (Task 12)`.

- [ ] **Step 4: Commit**

```bash
git add infra/scripts/db-save-state.ps1
git commit -m "feat(infra): db-save-state.ps1 skeleton with pre-flight checks"
```

---

## Task 12: `db-save-state.ps1` — dump operations

**Files:**
- Modify: `infra/scripts/db-save-state.ps1`

- [ ] **Step 1: Replace the placeholder with the dump steps**

Replace the line `Write-Timestamped 'TODO: dump operations (Task 12)'` with the following block:

```powershell
# ============================================================================
# Snapshot directory
# ============================================================================
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$snap = Join-Path $SnapshotRoot $timestamp
New-Item -ItemType Directory -Path $snap -Force | Out-Null
$logFile = Join-Path $snap 'save.log'
Write-Timestamped "Created snapshot dir: $snap" -LogFile $logFile

# ============================================================================
# Step 1: Safety-net dump (full schema + data, ALWAYS complete)
# ============================================================================
$safetyNetPath = Join-Path $snap 'safety-net.dump'
Write-Timestamped '[1/6] Safety-net dump (full schema + data)...' -LogFile $logFile
try {
    Invoke-PgDump -Config $cfg -Arguments @(
        '-Fc', '--no-owner', '--no-acl',
        '-f', $safetyNetPath
    )
} catch {
    # Safety-net failure is fatal
    Remove-Item $snap -Recurse -Force -ErrorAction SilentlyContinue
    throw "Safety-net dump failed (FATAL): $_"
}
$safetyNetSize = (Get-Item $safetyNetPath).Length
Write-Timestamped "    safety-net.dump: $safetyNetSize bytes" -LogFile $logFile

# ============================================================================
# Step 2: Schema-pre dump
# ============================================================================
$schemaPrePath = Join-Path $snap 'schema-pre.sql'
Write-Timestamped '[2/6] Schema-pre dump...' -LogFile $logFile
Invoke-PgDump -Config $cfg -Arguments @(
    '-s', '--no-owner', '--no-acl',
    '-f', $schemaPrePath
)

# ============================================================================
# Step 3: Data-only dump
# ============================================================================
$dataDumpPath = Join-Path $snap 'data.dump'
Write-Timestamped '[3/6] Data-only dump...' -LogFile $logFile
$dataArgs = @(
    '-Fc', '--data-only', '--no-owner', '--no-acl',
    '--exclude-table', '*__EFMigrationsHistory*'
)
$excludedTables = @()
if ($Sanitize) {
    foreach ($table in $SENSITIVE_TABLES) {
        $dataArgs += @('--exclude-table-data', "*.$table")
        $excludedTables += $table
    }
    Write-Timestamped "    Sanitize: excluding data from $($SENSITIVE_TABLES -join ', ')" -LogFile $logFile
}
$dataArgs += @('-f', $dataDumpPath)
Invoke-PgDump -Config $cfg -Arguments $dataArgs
$dataDumpSize = (Get-Item $dataDumpPath).Length
Write-Timestamped "    data.dump: $dataDumpSize bytes" -LogFile $logFile

# ============================================================================
# Step 4: Row counts (exact COUNT(*), not n_live_tup)
# ============================================================================
$rowCountsPath = Join-Path $snap 'rowcounts-pre.tsv'
Write-Timestamped '[4/6] Row counts (exact)...' -LogFile $logFile

$tableListSql = @"
SELECT table_schema || '.' || table_name
FROM information_schema.tables
WHERE table_type = 'BASE TABLE'
  AND table_schema NOT IN ('pg_catalog','information_schema')
  AND table_name NOT LIKE '\_\_EFMigrationsHistory%' ESCAPE '\'
ORDER BY table_schema, table_name;
"@
$tables = Invoke-Psql -Config $cfg -Sql $tableListSql | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$rowCounts = @{}
$tsvLines = New-Object System.Collections.Generic.List[string]
foreach ($qualified in $tables) {
    $parts = $qualified -split '\.', 2
    $schema = $parts[0]
    $table = $parts[1]
    $countSql = "SELECT COUNT(*) FROM ""$schema"".""$table"";"
    $count = [long]((Invoke-Psql -Config $cfg -Sql $countSql).Trim())
    $rowCounts[$qualified] = $count
    $tsvLines.Add("$schema`t$table`t$count")
}
Set-Content -LiteralPath $rowCountsPath -Value $tsvLines -Encoding UTF8
Write-Timestamped "    $($rowCounts.Count) tables counted" -LogFile $logFile

# ============================================================================
# Step 5: PDF volume backup (optional)
# ============================================================================
$pdfDumpPath = $null
$pdfFileCount = 0
if ($IncludePdfVolume) {
    $pdfDumpPath = Join-Path $snap 'pdf_uploads.tar.gz'
    Write-Timestamped '[5/6] PDF volume backup...' -LogFile $logFile
    & docker run --rm -v "meepleai_pdf_uploads:/src:ro" -v "${snap}:/dst" alpine tar czf /dst/pdf_uploads.tar.gz -C /src .
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to tar PDF volume (exit $LASTEXITCODE)"
    }
    if ($pdfVolumeStats) { $pdfFileCount = $pdfVolumeStats.FileCount }
    $pdfDumpSize = (Get-Item $pdfDumpPath).Length
    Write-Timestamped "    pdf_uploads.tar.gz: $pdfDumpSize bytes ($pdfFileCount files)" -LogFile $logFile
} else {
    Write-Timestamped '[5/6] PDF volume backup: SKIPPED (S3 storage or empty volume)' -LogFile $logFile
}

# ============================================================================
# Step 6: xact_commit snapshot for drift check during reset
# ============================================================================
$xactAtSave = Get-XactCommitCount -Config $cfg
Write-Timestamped "[6/6] xact_commit at save: $xactAtSave" -LogFile $logFile

# Capture for next task
$script:snap = $snap
$script:safetyNetSize = $safetyNetSize
$script:dataDumpSize = $dataDumpSize
$script:dbSize = $dbSize
$script:storageProvider = $storageProvider
$script:pdfDumpPath = $pdfDumpPath
$script:pdfFileCount = $pdfFileCount
$script:rowCounts = $rowCounts
$script:xactAtSave = $xactAtSave
$script:excludedTables = $excludedTables
$script:logFile = $logFile

# Placeholder for next task
Write-Timestamped 'TODO: manifest + summary (Task 13)' -LogFile $logFile
```

- [ ] **Step 2: Smoke test on local DB**

```bash
pwsh ./infra/scripts/db-save-state.ps1
```

Expected: 6 dump steps complete, snapshot directory created with `safety-net.dump`, `data.dump`, `schema-pre.sql`, `rowcounts-pre.tsv`, optionally `pdf_uploads.tar.gz`.

- [ ] **Step 3: Verify the snapshot files**

```bash
ls -la infra/db-snapshots/$(ls -t infra/db-snapshots/ | head -1)/
```

Expected: at least `safety-net.dump`, `data.dump`, `schema-pre.sql`, `rowcounts-pre.tsv`, `save.log`.

- [ ] **Step 4: Verify safety-net dump is restorable (sanity check, no commit needed)**

```bash
pwsh -c "& pg_restore --list (ls infra/db-snapshots/ | sort -r | head -1 | ForEach-Object { Join-Path 'infra/db-snapshots' \$_ 'safety-net.dump' }) | Select-Object -First 5"
```

Expected: header showing toc entries from the dump.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-save-state.ps1
git commit -m "feat(infra): db-save-state dump operations (safety-net, data, schema, rowcounts, pdf)"
```

---

## Task 13: `db-save-state.ps1` — manifest, README, and summary

**Files:**
- Modify: `infra/scripts/db-save-state.ps1`

- [ ] **Step 1: Replace the placeholder with manifest writing**

Replace `Write-Timestamped 'TODO: manifest + summary (Task 13)' -LogFile $logFile` with:

```powershell
# ============================================================================
# Manifest
# ============================================================================
Write-Timestamped 'Writing manifest.json...' -LogFile $logFile

$files = @{
    safetyNet = [pscustomobject]@{
        name = 'safety-net.dump'
        sha256 = Get-FileSha256 -Path $safetyNetPath
        bytes = $safetyNetSize
    }
    dataDump = [pscustomobject]@{
        name = 'data.dump'
        sha256 = Get-FileSha256 -Path $dataDumpPath
        bytes = $dataDumpSize
    }
    schemaPre = [pscustomobject]@{
        name = 'schema-pre.sql'
        sha256 = Get-FileSha256 -Path $schemaPrePath
        bytes = (Get-Item $schemaPrePath).Length
    }
}
if ($pdfDumpPath) {
    $files['pdfVolume'] = [pscustomobject]@{
        name = 'pdf_uploads.tar.gz'
        sha256 = Get-FileSha256 -Path $pdfDumpPath
        bytes = (Get-Item $pdfDumpPath).Length
    }
}

$manifest = [pscustomobject]@{
    schemaVersion       = 1
    createdAt           = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    database            = $cfg.Db
    host                = $cfg.Host
    dbSizeBytes         = $dbSize
    xactCommitAtSave    = $xactAtSave
    sanitize            = [bool]$Sanitize
    excludedTables      = $excludedTables
    storageProvider     = $storageProvider
    pdfVolumeIncluded   = [bool]$pdfDumpPath
    pdfFileCount        = $pdfFileCount
    files               = [pscustomobject]$files
    rowCountsPre        = [pscustomobject]$rowCounts
}
$manifestPath = Join-Path $snap 'manifest.json'
Write-Manifest -Path $manifestPath -Object $manifest
Write-Timestamped "    manifest.json written" -LogFile $logFile

# ============================================================================
# README inside the snapshot
# ============================================================================
$readmePath = Join-Path $snap 'README.md'
$readmeContent = @"
# Snapshot $timestamp

Created by ``db-save-state.ps1`` on $((Get-Date).ToString('u')).

## Files

| File | Purpose |
|------|---------|
| ``manifest.json`` | Immutable snapshot metadata (hashes, row counts, sizes) |
| ``safety-net.dump`` | Full pg_dump (schema + data) — used by ``-UseSafetyNet`` rollback |
| ``data.dump`` | Data-only pg_dump — used by standard restore |
| ``schema-pre.sql`` | Schema dump before reset — used for drift detection |
| ``rowcounts-pre.tsv`` | Exact row counts per table — used for restore verification |
$(if ($pdfDumpPath) { "| ``pdf_uploads.tar.gz`` | Tar of meepleai_pdf_uploads volume |" })

## Next steps

1. Reset migrations:

   ``````
   pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath $snap
   ``````

2. Restore data into the fresh schema:

   ``````
   pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath $snap
   ``````

## Rollback

If anything goes wrong, restore the full state from the safety-net:

``````
pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath $snap -UseSafetyNet
``````
"@
Set-Content -LiteralPath $readmePath -Value $readmeContent -Encoding UTF8
Write-Timestamped "    README.md written" -LogFile $logFile

# ============================================================================
# Summary
# ============================================================================
$totalSize = (Get-ChildItem $snap -File | Measure-Object -Property Length -Sum).Sum
Write-Timestamped '' -LogFile $logFile
Write-Timestamped "=== Snapshot ready: $snap ===" -LogFile $logFile
Write-Timestamped "    Total: $totalSize bytes ($($files.Count + $(if($pdfDumpPath){1}else{0})) dump files + manifest + README)" -LogFile $logFile
Write-Timestamped '' -LogFile $logFile
Write-Timestamped 'Next: pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath ' + $snap -LogFile $logFile
```

- [ ] **Step 2: Smoke test the full save flow**

```bash
pwsh ./infra/scripts/db-save-state.ps1
```

Expected: all 6 dump steps + manifest + README + summary line printed.

- [ ] **Step 3: Verify manifest is valid JSON and contains expected fields**

```bash
pwsh -c "Get-Content (Join-Path (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName 'manifest.json') | ConvertFrom-Json | ConvertTo-Json -Depth 4"
```

Expected: well-formed JSON with `schemaVersion`, `createdAt`, `files.safetyNet.sha256`, `rowCountsPre`, etc.

- [ ] **Step 4: Verify file SHA256 hashes match the manifest**

```bash
pwsh -c "
\$snapDir = (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
Import-Module ./infra/scripts/db-snapshot-common.psm1 -Force
\$m = Read-Manifest -Path (Join-Path \$snapDir 'manifest.json')
foreach (\$prop in \$m.files.PSObject.Properties) {
    \$f = \$prop.Value
    \$actual = Get-FileSha256 -Path (Join-Path \$snapDir \$f.name)
    Write-Host \"\$(\$f.name): manifest=\$(\$f.sha256), actual=\$actual\"
    if (\$actual -ne \$f.sha256) { throw 'MISMATCH' }
}
"
```

Expected: all files report manifest SHA matching actual SHA.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-save-state.ps1
git commit -m "feat(infra): db-save-state writes immutable manifest.json and README"
```

---

## Task 14: `db-reset-migrations.ps1` — skeleton + pre-flight + confirmation

**Files:**
- Create: `infra/scripts/db-reset-migrations.ps1`

- [ ] **Step 1: Create the script**

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Wipes the local DB, deletes apps/api/src/Api/Migrations/, and creates a fresh Initial migration.
.DESCRIPTION
    Step 2 of the migration consolidation workflow. Requires a snapshot already
    created by db-save-state.ps1. All destructive actions are gated behind
    explicit confirmation. Refuses to run if dotnet build fails.
.PARAMETER SnapshotPath
    Required. Path to a snapshot directory created by db-save-state.ps1.
.PARAMETER InitialName
    Name of the new consolidated migration. Default: "Initial".
.PARAMETER ApiProjectPath
    Path to the API .csproj root. Default: apps/api/src/Api.
.PARAMETER Force
    Skip the confirmation prompt.
.PARAMETER DryRun
    Print intended commands without executing destructive actions.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SnapshotPath,
    [string]$InitialName = 'Initial',
    [string]$ApiProjectPath = (Join-Path $PSScriptRoot '..' '..' 'apps' 'api' 'src' 'Api'),
    [switch]$Force,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'db-snapshot-common.psm1') -Force

Write-Timestamped '=== db-reset-migrations ==='

# ============================================================================
# Pre-flight
# ============================================================================

# Check 1: snapshot is present and valid
if (-not (Test-Path -LiteralPath $SnapshotPath)) {
    throw "Snapshot path not found: $SnapshotPath"
}
$manifestPath = Join-Path $SnapshotPath 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "manifest.json not found in $SnapshotPath — is this a valid snapshot?"
}
$manifest = Read-Manifest -Path $manifestPath

# Check 1b: re-verify dump file hashes
foreach ($prop in $manifest.files.PSObject.Properties) {
    $f = $prop.Value
    $filePath = Join-Path $SnapshotPath $f.name
    if (-not (Test-Path -LiteralPath $filePath)) {
        throw "Snapshot file missing: $($f.name)"
    }
    $actual = Get-FileSha256 -Path $filePath
    if ($actual -ne $f.sha256) {
        throw "Snapshot file CORRUPTED: $($f.name) hash mismatch (expected $($f.sha256), got $actual)"
    }
}
Write-Timestamped "[1/7] Snapshot validated: $SnapshotPath"

# Check 2: localhost-only enforcement
$secretFile = Join-Path $PSScriptRoot '..' 'secrets' 'database.secret'
$cfg = Get-PostgresConfig -SecretPath $secretFile
Assert-LocalhostOnly -Config $cfg
Write-Timestamped '[2/7] Localhost host check passed'

# Check 3: DB drift since save (xact_commit delta, warning only)
try {
    $xactNow = Get-XactCommitCount -Config $cfg
    $delta = $xactNow - $manifest.xactCommitAtSave
    if ($delta -gt 10) {
        Write-Host "⚠ The DB has had $delta committed transactions since the snapshot was taken." -ForegroundColor Yellow
        Write-Host "   The snapshot may be stale. Consider creating a fresh snapshot first." -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠ Could not check xact_commit drift: $_" -ForegroundColor Yellow
}
Write-Timestamped '[3/7] DB drift check complete'

# Check 4: dotnet and dotnet ef present
foreach ($tool in @('dotnet')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool not found in PATH"
    }
}
$efCheck = & dotnet ef --version 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet ef tool not installed. Run: dotnet tool install --global dotnet-ef"
}
Write-Timestamped "[4/7] dotnet ef found: $($efCheck -join ' ')"

# Check 5: API project exists
if (-not (Test-Path -LiteralPath $ApiProjectPath)) {
    throw "API project path not found: $ApiProjectPath"
}
$migrationsFolder = Join-Path $ApiProjectPath 'Migrations'
if (-not (Test-Path -LiteralPath $migrationsFolder)) {
    throw "Migrations folder not found in $ApiProjectPath. Nothing to reset."
}
$migrationFiles = @(Get-ChildItem -LiteralPath $migrationsFolder -File -Recurse)
Write-Timestamped "[5/7] API project found, $($migrationFiles.Count) files in Migrations/"

# Check 6: dotnet build sanity check (HARD BLOCK if fails)
Write-Timestamped '[6/7] Running dotnet build sanity check...'
$buildOutput = & dotnet build $ApiProjectPath -c Debug --nologo --verbosity quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host '❌ dotnet build failed:' -ForegroundColor Red
    $buildOutput | ForEach-Object { Write-Host "    $_" }
    throw 'Refusing to drop the database while the project does not compile. Fix the build first.'
}
Write-Timestamped '    Build OK'

# Check 7: git status (warning only)
$gitDirty = & git -C $ApiProjectPath status --porcelain Migrations/ 2>$null
if ($gitDirty) {
    Write-Host "⚠ Migrations/ has uncommitted changes:" -ForegroundColor Yellow
    $gitDirty | ForEach-Object { Write-Host "    $_" }
    Write-Host "   These changes will be LOST. Consider committing first." -ForegroundColor Yellow
}
Write-Timestamped '[7/7] Pre-flight checks complete'

# ============================================================================
# Confirmation
# ============================================================================
$safetyNetSize = $manifest.files.safetyNet.bytes
$prompt = @"
This will:
  1. DROP database '$($cfg.Db)' on $($cfg.Host):$($cfg.Port)
  2. DELETE folder $migrationsFolder ($($migrationFiles.Count) files)
  3. CREATE new migration '$InitialName'
  4. APPLY it to a fresh database

Snapshot safety net: $($manifest.files.safetyNet.name) ($safetyNetSize bytes)

To rollback if anything goes wrong:
  pwsh db-restore-state.ps1 -SnapshotPath $SnapshotPath -UseSafetyNet
"@
if (-not (Confirm-UserAction -Prompt $prompt -Force:$Force)) {
    Write-Timestamped 'Aborted by user. Snapshot intact, DB intact.'
    exit 0
}

# Placeholder for next task
Write-Timestamped 'TODO: destructive actions (Task 15)'
```

- [ ] **Step 2: Parse-only check**

```bash
pwsh -c "[System.Management.Automation.Language.Parser]::ParseFile('infra/scripts/db-reset-migrations.ps1', [ref]\$null, [ref]\$null) | Out-Null; Write-Host 'Parse OK'"
```

Expected: `Parse OK`.

- [ ] **Step 3: Smoke test pre-flight only (decline confirmation)**

Use the most recent snapshot from Task 13:

```bash
pwsh -c "
\$snap = (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
Write-Host 'no' | pwsh ./infra/scripts/db-reset-migrations.ps1 -SnapshotPath \$snap
"
```

Expected: 7 pre-flight checks pass, confirmation prompt appears, "Aborted by user" printed, exit 0.

- [ ] **Step 4: Commit**

```bash
git add infra/scripts/db-reset-migrations.ps1
git commit -m "feat(infra): db-reset-migrations skeleton with pre-flight checks and dotnet build guard"
```

---

## Task 15: `db-reset-migrations.ps1` — destructive actions

**Files:**
- Modify: `infra/scripts/db-reset-migrations.ps1`

- [ ] **Step 1: Replace the placeholder with destructive steps**

Replace `Write-Timestamped 'TODO: destructive actions (Task 15)'` with:

```powershell
# ============================================================================
# Destructive actions
# ============================================================================
$logFile = Join-Path $SnapshotPath 'reset.log'
Write-Timestamped '' -LogFile $logFile

if ($DryRun) {
    Write-Timestamped '[DRY RUN] Would execute:' -LogFile $logFile
    Write-Timestamped "  dotnet ef database drop --force --project $ApiProjectPath" -LogFile $logFile
    Write-Timestamped "  Copy-Item -Recurse $migrationsFolder $SnapshotPath/migrations-backup/" -LogFile $logFile
    Write-Timestamped "  Remove-Item -Recurse -Force $migrationsFolder" -LogFile $logFile
    Write-Timestamped "  dotnet ef migrations add $InitialName --project $ApiProjectPath" -LogFile $logFile
    Write-Timestamped "  dotnet ef database update --project $ApiProjectPath" -LogFile $logFile
    Write-Timestamped 'Dry run complete. No changes made.' -LogFile $logFile
    exit 0
}

# Step 1: Drop DB
Write-Timestamped '[1/6] dotnet ef database drop --force...' -LogFile $logFile
& dotnet ef database drop --force --project $ApiProjectPath 2>&1 | Tee-Object -FilePath $logFile -Append
if ($LASTEXITCODE -ne 0) {
    throw "dotnet ef database drop failed (exit $LASTEXITCODE). DB may still exist. No changes to Migrations/."
}

# Step 2: Backup Migrations folder
$migrationsBackupDir = Join-Path $SnapshotPath 'migrations-backup'
Write-Timestamped "[2/6] Backup Migrations/ to $migrationsBackupDir..." -LogFile $logFile
Copy-Item -LiteralPath $migrationsFolder -Destination $migrationsBackupDir -Recurse -Force

# Step 3: Delete Migrations folder
Write-Timestamped '[3/6] Deleting Migrations/...' -LogFile $logFile
try {
    Remove-Item -LiteralPath $migrationsFolder -Recurse -Force
} catch {
    throw "Failed to delete $migrationsFolder. DB is dropped. Restore Migrations from $migrationsBackupDir manually."
}

# Step 4: dotnet ef migrations add Initial
Write-Timestamped "[4/6] dotnet ef migrations add $InitialName..." -LogFile $logFile
& dotnet ef migrations add $InitialName --project $ApiProjectPath 2>&1 | Tee-Object -FilePath $logFile -Append
if ($LASTEXITCODE -ne 0) {
    Write-Host '' -ForegroundColor Red
    Write-Host '❌ dotnet ef migrations add failed.' -ForegroundColor Red
    Write-Host "   The DB is DROPPED and Migrations/ is DELETED." -ForegroundColor Red
    Write-Host '   To recover:' -ForegroundColor Yellow
    Write-Host "     Copy-Item -Recurse $migrationsBackupDir $migrationsFolder" -ForegroundColor Yellow
    Write-Host "   Then fix the model code and retry, OR rollback via:" -ForegroundColor Yellow
    Write-Host "     pwsh db-restore-state.ps1 -SnapshotPath $SnapshotPath -UseSafetyNet" -ForegroundColor Yellow
    throw "Migration generation failed"
}

# Step 5: dotnet ef database update
Write-Timestamped '[5/6] dotnet ef database update...' -LogFile $logFile
& dotnet ef database update --project $ApiProjectPath 2>&1 | Tee-Object -FilePath $logFile -Append
if ($LASTEXITCODE -ne 0) {
    Write-Host '' -ForegroundColor Red
    Write-Host '❌ dotnet ef database update failed.' -ForegroundColor Red
    Write-Host "   The new Initial migration was generated but failed to apply." -ForegroundColor Red
    Write-Host '   Check the migration SQL in Migrations/, fix, and retry.' -ForegroundColor Yellow
    Write-Host "   Or rollback via -UseSafetyNet." -ForegroundColor Yellow
    throw 'Migration apply failed'
}

# Step 6: Dump preliminary schema-post + write reset-result.json
Write-Timestamped '[6/6] Dumping schema-post and writing reset-result.json...' -LogFile $logFile
$schemaPostPath = Join-Path $SnapshotPath 'schema-post.sql'
Invoke-PgDump -Config $cfg -Arguments @('-s', '--no-owner', '--no-acl', '-f', $schemaPostPath)
$schemaPostHash = Get-FileSha256 -Path $schemaPostPath

$resetResult = [pscustomobject]@{
    schemaVersion           = 1
    completedAt             = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    initialName             = $InitialName
    migrationsBackupPath    = 'migrations-backup/'
    migrationsBackedUpCount = $migrationFiles.Count
    schemaPostFile          = [pscustomobject]@{
        name = 'schema-post.sql'
        sha256 = $schemaPostHash
        bytes = (Get-Item $schemaPostPath).Length
    }
    buildSanityCheckPassed  = $true
}
Write-Manifest -Path (Join-Path $SnapshotPath 'reset-result.json') -Object $resetResult

# Summary
Write-Timestamped '' -LogFile $logFile
Write-Timestamped "=== Reset complete. DB has new schema, no data. ===" -LogFile $logFile
Write-Timestamped "Next: pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath $SnapshotPath" -LogFile $logFile
```

- [ ] **Step 2: Smoke test against the latest snapshot (REAL execution)**

⚠ **This is destructive.** It will drop the local dev DB. Make sure you have a snapshot first.

```bash
pwsh -c "
\$snap = (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
Write-Host 'yes' | pwsh ./infra/scripts/db-reset-migrations.ps1 -SnapshotPath \$snap
"
```

Expected: 6 destructive steps complete, fresh schema applied, `migrations-backup/`, `schema-post.sql`, and `reset-result.json` created in the snapshot dir.

- [ ] **Step 3: Verify reset-result.json**

```bash
pwsh -c "Get-Content (Join-Path (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName 'reset-result.json') | ConvertFrom-Json | ConvertTo-Json"
```

Expected: object with `schemaVersion`, `completedAt`, `initialName`, `schemaPostFile.sha256`, `buildSanityCheckPassed: true`.

- [ ] **Step 4: Verify the DB has only 1 migration in `__EFMigrationsHistory`**

```bash
pwsh -c "
Import-Module ./infra/scripts/db-snapshot-common.psm1 -Force
\$cfg = Get-PostgresConfig -SecretPath ./infra/secrets/database.secret
Invoke-Psql -Config \$cfg -Sql 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1;'
"
```

Expected: a single row containing `<timestamp>_Initial`.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-reset-migrations.ps1
git commit -m "feat(infra): db-reset-migrations destructive actions with safe rollback paths"
```

---

## Task 16: `db-restore-state.ps1` — skeleton + diff classification

**Files:**
- Create: `infra/scripts/db-restore-state.ps1`

- [ ] **Step 1: Create the script with parameters, pre-flight, schema diff**

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Restores data from a snapshot into the freshly-rebuilt schema.
.DESCRIPTION
    Step 3 of the migration consolidation workflow. Computes schema drift
    between the saved schema-pre.sql and the current schema-post.sql.
    If identical or minor, restores data.dump. If significant, STOPs unless
    -AllowDrift is set. Supports -UseSafetyNet for full rollback.
.PARAMETER SnapshotPath
    Required. Snapshot directory created by db-save-state.ps1.
.PARAMETER UseSafetyNet
    Use safety-net.dump (full restore, drops and recreates DB) instead of data.dump.
.PARAMETER RestorePdfVolume
    Restore the Docker PDF volume from pdf_uploads.tar.gz. Defaults to true if present.
.PARAMETER AllowDrift
    Proceed with restore even if schema drift is classified as significant.
.PARAMETER Force
    Skip confirmation prompts.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SnapshotPath,
    [switch]$UseSafetyNet,
    [Nullable[bool]]$RestorePdfVolume = $null,
    [switch]$AllowDrift,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'db-snapshot-common.psm1') -Force

Write-Timestamped '=== db-restore-state ==='

# ============================================================================
# Pre-flight
# ============================================================================
if (-not (Test-Path -LiteralPath $SnapshotPath)) {
    throw "Snapshot path not found: $SnapshotPath"
}
$manifestPath = Join-Path $SnapshotPath 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "manifest.json not found in $SnapshotPath"
}
$manifest = Read-Manifest -Path $manifestPath

# Re-verify dump file hashes (Nygard F3)
foreach ($prop in $manifest.files.PSObject.Properties) {
    $f = $prop.Value
    $filePath = Join-Path $SnapshotPath $f.name
    if (-not (Test-Path -LiteralPath $filePath)) {
        throw "Snapshot file missing: $($f.name)"
    }
    $actual = Get-FileSha256 -Path $filePath
    if ($actual -ne $f.sha256) {
        throw "Snapshot file CORRUPTED: $($f.name)"
    }
}
Write-Timestamped '[1/9] Snapshot validated'

# Localhost-only enforcement
$secretFile = Join-Path $PSScriptRoot '..' 'secrets' 'database.secret'
$cfg = Get-PostgresConfig -SecretPath $secretFile
Assert-LocalhostOnly -Config $cfg
Write-Timestamped '[2/9] Localhost host check passed'

# Resolve RestorePdfVolume default
if ($null -eq $RestorePdfVolume) {
    $RestorePdfVolume = ($manifest.pdfVolumeIncluded -eq $true)
}

# Branch on mode
if ($UseSafetyNet) {
    Write-Timestamped '[3/9] Mode: SAFETY-NET ROLLBACK'
    Write-Timestamped 'TODO: safety-net mode (Task 18)'
    exit 0
}

Write-Timestamped '[3/9] Mode: standard data restore'

# Standard mode requires the new schema in place
$schemaPostPath = Join-Path $SnapshotPath 'schema-post.sql'
Write-Timestamped '[4/9] Re-dumping fresh schema-post.sql...'
Invoke-PgDump -Config $cfg -Arguments @('-s', '--no-owner', '--no-acl', '-f', $schemaPostPath)

# Read both schemas, normalize
$schemaPrePath = Join-Path $SnapshotPath 'schema-pre.sql'
$preSql = Get-Content -LiteralPath $schemaPrePath -Raw
$postSql = Get-Content -LiteralPath $schemaPostPath -Raw
$preNormalized = Normalize-PgSchema -Sql $preSql
$postNormalized = Normalize-PgSchema -Sql $postSql
Set-Content -LiteralPath (Join-Path $SnapshotPath 'schema-pre.normalized.sql') -Value $preNormalized -Encoding UTF8
Set-Content -LiteralPath (Join-Path $SnapshotPath 'schema-post.normalized.sql') -Value $postNormalized -Encoding UTF8
Write-Timestamped '[5/9] Schema normalized'

# Classify drift
$driftClass = Test-SchemaDriftClass -PreSchema $preNormalized -PostSchema $postNormalized
Write-Timestamped "[6/9] Drift classification: $driftClass"

# Compute and save diff
$diffPath = Join-Path $SnapshotPath 'schema-diff.txt'
& git diff --no-index --no-color (Join-Path $SnapshotPath 'schema-pre.normalized.sql') (Join-Path $SnapshotPath 'schema-post.normalized.sql') 2>$null | Out-File -LiteralPath $diffPath -Encoding UTF8

if ($driftClass -eq 'significant' -and -not $AllowDrift) {
    Write-Host '' -ForegroundColor Red
    Write-Host "❌ Schema drift detected: classification = significant" -ForegroundColor Red
    Write-Host "" -ForegroundColor Red
    Write-Host "Diff saved to: $diffPath"
    Write-Host ""
    Write-Host "First 30 lines of diff:"
    Get-Content -LiteralPath $diffPath -TotalCount 30 | ForEach-Object { Write-Host "    $_" }
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  a) Fix your model code and re-run:"
    Write-Host "     pwsh db-reset-migrations.ps1 -SnapshotPath $SnapshotPath"
    Write-Host "  b) Rollback to safety net:"
    Write-Host "     pwsh db-restore-state.ps1 -SnapshotPath $SnapshotPath -UseSafetyNet"
    Write-Host "  c) Force restore (will likely fail on missing/renamed columns):"
    Write-Host "     re-run with -AllowDrift"
    Write-Host ""
    Write-Timestamped 'Aborting. DB unchanged (fresh schema, empty).'
    exit 2
}

if ($driftClass -eq 'minor') {
    Write-Host "⚠ Minor schema drift detected (whitespace/cosmetic). Proceeding." -ForegroundColor Yellow
}

# Placeholder for next task
Write-Timestamped 'TODO: data restore + sequences (Task 17)'
```

- [ ] **Step 2: Parse-only check**

```bash
pwsh -c "[System.Management.Automation.Language.Parser]::ParseFile('infra/scripts/db-restore-state.ps1', [ref]\$null, [ref]\$null) | Out-Null; Write-Host 'Parse OK'"
```

- [ ] **Step 3: Smoke test against the snapshot from Task 15**

Prerequisite: Task 15 has been run (DB has new Initial schema, snapshot has `schema-post.sql`).

```bash
pwsh -c "
\$snap = (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
pwsh ./infra/scripts/db-restore-state.ps1 -SnapshotPath \$snap
"
```

Expected: pre-flight passes, schema is re-dumped, diff classification printed (likely `identical` since the model didn't change), placeholder line "TODO: data restore + sequences (Task 17)" printed.

- [ ] **Step 4: Commit**

```bash
git add infra/scripts/db-restore-state.ps1
git commit -m "feat(infra): db-restore-state skeleton with schema diff classification"
```

---

## Task 17: `db-restore-state.ps1` — data restore, sequences, verify

**Files:**
- Modify: `infra/scripts/db-restore-state.ps1`

- [ ] **Step 1: Replace the placeholder with restore + verify**

Replace `Write-Timestamped 'TODO: data restore + sequences (Task 17)'` with:

```powershell
# ============================================================================
# Restore data
# ============================================================================
$dataDumpPath = Join-Path $SnapshotPath $manifest.files.dataDump.name
Write-Timestamped "[7/9] Restoring data from $($manifest.files.dataDump.name)..."
$restoreStart = Get-Date
Invoke-PgRestore -Config $cfg -Arguments @(
    '--data-only', '--no-owner', '--no-acl',
    '--disable-triggers', '--single-transaction',
    $dataDumpPath
)
$restoreSeconds = ((Get-Date) - $restoreStart).TotalSeconds
Write-Timestamped "    Data restored in $([math]::Round($restoreSeconds,1))s"

# ============================================================================
# Reset sequences (via pg_depend join, owns table+column for each sequence)
# ============================================================================
Write-Timestamped '[8/9] Resetting sequences...'
$seqDiscoverySql = @"
SELECT
  ns.nspname || '|' || s.relname || '|' || tns.nspname || '|' || t.relname || '|' || a.attname
FROM pg_class s
JOIN pg_namespace ns ON ns.oid = s.relnamespace
JOIN pg_depend d ON d.objid = s.oid AND d.deptype = 'a'
JOIN pg_class t ON t.oid = d.refobjid
JOIN pg_namespace tns ON tns.oid = t.relnamespace
JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = d.refobjsubid
WHERE s.relkind = 'S'
  AND ns.nspname NOT IN ('pg_catalog','information_schema');
"@
$seqRows = Invoke-Psql -Config $cfg -Sql $seqDiscoverySql | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$sequencesReset = 0
foreach ($row in $seqRows) {
    $parts = $row -split '\|'
    if ($parts.Count -ne 5) { continue }
    $seqSchema = $parts[0]
    $seqName = $parts[1]
    $tblSchema = $parts[2]
    $tblName = $parts[3]
    $colName = $parts[4]
    $setvalSql = "SELECT setval('""$seqSchema"".""$seqName""', (SELECT COALESCE(MAX(""$colName""),0) + 1 FROM ""$tblSchema"".""$tblName""), false);"
    $null = Invoke-Psql -Config $cfg -Sql $setvalSql
    $sequencesReset++
}
Write-Timestamped "    $sequencesReset sequences reset"

# ============================================================================
# Verify row counts
# ============================================================================
Write-Timestamped '[9/9] Verifying row counts...'
$mismatches = New-Object System.Collections.Generic.List[pscustomobject]
$matched = 0
foreach ($prop in $manifest.rowCountsPre.PSObject.Properties) {
    $qualified = $prop.Name
    $expected = [long]$prop.Value
    $parts = $qualified -split '\.', 2
    $schema = $parts[0]
    $table = $parts[1]
    $sql = "SELECT COUNT(*) FROM ""$schema"".""$table"";"
    try {
        $actualRaw = Invoke-Psql -Config $cfg -Sql $sql
        $actual = [long]($actualRaw.Trim())
    } catch {
        $actual = -1
    }
    # If sanitize was used and the table is in excludedTables, expect 0
    $isExcluded = $false
    if ($manifest.sanitize) {
        foreach ($ex in $manifest.excludedTables) {
            if ($table -eq $ex) { $isExcluded = $true; break }
        }
    }
    $expectedActual = if ($isExcluded) { 0 } else { $expected }
    if ($actual -eq $expectedActual) {
        $matched++
    } else {
        $mismatches.Add([pscustomobject]@{
            Table = $qualified
            Expected = $expectedActual
            Actual = $actual
        })
    }
}
$rowCountMatch = ($mismatches.Count -eq 0)
if (-not $rowCountMatch) {
    Write-Host "⚠ Row count mismatches:" -ForegroundColor Yellow
    foreach ($m in $mismatches) {
        Write-Host "    $($m.Table): expected=$($m.Expected), actual=$($m.Actual)" -ForegroundColor Yellow
    }
}
Write-Timestamped "    $matched/$($matched + $mismatches.Count) tables match"

# ============================================================================
# Restore PDF volume (if applicable)
# ============================================================================
$pdfRestored = $false
$pdfFilesRestored = 0
if ($RestorePdfVolume -and $manifest.pdfVolumeIncluded) {
    $pdfTarPath = Join-Path $SnapshotPath 'pdf_uploads.tar.gz'
    if (Test-Path -LiteralPath $pdfTarPath) {
        Write-Timestamped 'Restoring PDF volume...'
        & docker run --rm -v "meepleai_pdf_uploads:/dst" -v "${SnapshotPath}:/src:ro" alpine sh -c 'rm -rf /dst/* && tar xzf /src/pdf_uploads.tar.gz -C /dst'
        if ($LASTEXITCODE -ne 0) {
            Write-Host "⚠ PDF volume restore failed (exit $LASTEXITCODE)" -ForegroundColor Yellow
        } else {
            $stats = Get-DockerVolumeStats -VolumeName 'meepleai_pdf_uploads'
            $pdfFilesRestored = $stats.FileCount
            $pdfRestored = $true
            Write-Timestamped "    $pdfFilesRestored PDF files restored"
        }
    }
}

# ============================================================================
# Write restore-result.json
# ============================================================================
$restoreResult = [pscustomobject]@{
    schemaVersion          = 1
    completedAt            = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    mode                   = 'data-only'
    schemaDriftClass       = $driftClass
    schemaDiffFile         = [pscustomobject]@{
        name = 'schema-diff.txt'
        sha256 = (Get-FileSha256 -Path $diffPath)
        bytes = (Get-Item $diffPath).Length
    }
    rowCountMatch          = $rowCountMatch
    rowCountMismatches     = @($mismatches)
    pdfVolumeRestored      = $pdfRestored
    pdfFilesRestoredCount  = $pdfFilesRestored
    sequencesReset         = $sequencesReset
    durationSeconds        = [math]::Round($restoreSeconds, 1)
}
Write-Manifest -Path (Join-Path $SnapshotPath 'restore-result.json') -Object $restoreResult

# ============================================================================
# Summary
# ============================================================================
Write-Timestamped ''
Write-Timestamped '=== Restore complete ==='
Write-Timestamped "    Schema:    $driftClass"
Write-Timestamped "    Rows:      $matched/$($matched + $mismatches.Count) tables match"
Write-Timestamped "    Sequences: $sequencesReset reset"
if ($pdfRestored) {
    Write-Timestamped "    PDFs:      $pdfFilesRestored files restored"
}
Write-Timestamped "    Duration:  $([math]::Round($restoreSeconds,1))s"
```

- [ ] **Step 2: Smoke test the full restore**

⚠ This will populate the dev DB with the snapshot data.

```bash
pwsh -c "
\$snap = (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
pwsh ./infra/scripts/db-restore-state.ps1 -SnapshotPath \$snap
"
```

Expected: schema diff = `identical`, all rows restored, sequences reset, `restore-result.json` created. Exit 0.

- [ ] **Step 3: Verify row counts match the rowcounts-pre.tsv from the snapshot**

```bash
pwsh -c "
\$snap = (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
\$result = Get-Content (Join-Path \$snap 'restore-result.json') | ConvertFrom-Json
\$result.rowCountMatch | Should -BeTrue
Write-Host 'Row count match: OK'
"
```

Expected: `Row count match: OK`.

- [ ] **Step 4: Commit**

```bash
git add infra/scripts/db-restore-state.ps1
git commit -m "feat(infra): db-restore-state data restore with sequence reset and verification"
```

---

## Task 18: `db-restore-state.ps1` — `-UseSafetyNet` rollback mode

**Files:**
- Modify: `infra/scripts/db-restore-state.ps1`

- [ ] **Step 1: Replace the safety-net placeholder**

Find this block in the script (added in Task 16):

```powershell
if ($UseSafetyNet) {
    Write-Timestamped '[3/9] Mode: SAFETY-NET ROLLBACK'
    Write-Timestamped 'TODO: safety-net mode (Task 18)'
    exit 0
}
```

Replace with:

```powershell
if ($UseSafetyNet) {
    Write-Timestamped '[3/9] Mode: SAFETY-NET ROLLBACK'

    $safetyNetPath = Join-Path $SnapshotPath $manifest.files.safetyNet.name
    if (-not (Test-Path -LiteralPath $safetyNetPath)) {
        throw "safety-net.dump not found at $safetyNetPath"
    }

    # Confirmation
    $prompt = @"
This will:
  1. DROP database '$($cfg.Db)' (current contents will be LOST)
  2. RESTORE from $($manifest.files.safetyNet.name) (full schema + data)

After rollback, the DB will be in the state it had at $($manifest.createdAt).

NOTE: If you have already run db-reset-migrations and the Migrations/ folder
has been replaced, the restored DB will be INCONSISTENT with the code.
You will need to also restore Migrations/ from $SnapshotPath/migrations-backup/.
"@
    if (-not (Confirm-UserAction -Prompt $prompt -Force:$Force)) {
        Write-Timestamped 'Aborted by user.'
        exit 0
    }

    # Drop and recreate DB
    Write-Timestamped '[4/9] Dropping and recreating DB...'
    $cfgPostgres = [pscustomobject]@{
        Host = $cfg.Host; Port = $cfg.Port; User = $cfg.User; Password = $cfg.Password; Db = 'postgres'
    }
    $null = Invoke-Psql -Config $cfgPostgres -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$($cfg.Db)' AND pid <> pg_backend_pid();"
    $null = Invoke-Psql -Config $cfgPostgres -Sql "DROP DATABASE IF EXISTS ""$($cfg.Db)"";"
    $null = Invoke-Psql -Config $cfgPostgres -Sql "CREATE DATABASE ""$($cfg.Db)"";"
    Write-Timestamped '    DB recreated'

    # Restore from safety-net
    Write-Timestamped '[5/9] pg_restore from safety-net...'
    $restoreStart = Get-Date
    Invoke-PgRestore -Config $cfg -Arguments @(
        '--clean', '--if-exists', '--no-owner', '--no-acl',
        $safetyNetPath
    )
    $restoreSeconds = ((Get-Date) - $restoreStart).TotalSeconds
    Write-Timestamped "    Restored in $([math]::Round($restoreSeconds,1))s"

    # Verify row counts (must all match)
    Write-Timestamped '[6/9] Verifying row counts...'
    $mismatches = New-Object System.Collections.Generic.List[pscustomobject]
    $matched = 0
    foreach ($prop in $manifest.rowCountsPre.PSObject.Properties) {
        $qualified = $prop.Name
        $expected = [long]$prop.Value
        $parts = $qualified -split '\.', 2
        $sql = "SELECT COUNT(*) FROM ""$($parts[0])"".""$($parts[1])"";"
        try {
            $actual = [long]((Invoke-Psql -Config $cfg -Sql $sql).Trim())
        } catch {
            $actual = -1
        }
        if ($actual -eq $expected) { $matched++ } else {
            $mismatches.Add([pscustomobject]@{ Table = $qualified; Expected = $expected; Actual = $actual })
        }
    }
    $rowCountMatch = ($mismatches.Count -eq 0)
    if (-not $rowCountMatch) {
        Write-Host '⚠ Row count mismatches after safety-net restore:' -ForegroundColor Yellow
        foreach ($m in $mismatches) { Write-Host "    $($m.Table): expected=$($m.Expected), actual=$($m.Actual)" -ForegroundColor Yellow }
    }
    Write-Timestamped "    $matched/$($matched + $mismatches.Count) tables match"

    # Restore PDF volume too if requested
    $pdfRestored = $false
    $pdfFilesRestored = 0
    if ($RestorePdfVolume -and $manifest.pdfVolumeIncluded) {
        $pdfTarPath = Join-Path $SnapshotPath 'pdf_uploads.tar.gz'
        if (Test-Path -LiteralPath $pdfTarPath) {
            Write-Timestamped '[7/9] Restoring PDF volume...'
            & docker run --rm -v "meepleai_pdf_uploads:/dst" -v "${SnapshotPath}:/src:ro" alpine sh -c 'rm -rf /dst/* && tar xzf /src/pdf_uploads.tar.gz -C /dst'
            if ($LASTEXITCODE -eq 0) {
                $stats = Get-DockerVolumeStats -VolumeName 'meepleai_pdf_uploads'
                $pdfFilesRestored = $stats.FileCount
                $pdfRestored = $true
                Write-Timestamped "    $pdfFilesRestored PDF files restored"
            }
        }
    }

    # Write restore-result.json
    $restoreResult = [pscustomobject]@{
        schemaVersion          = 1
        completedAt            = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        mode                   = 'safety-net'
        rowCountMatch          = $rowCountMatch
        rowCountMismatches     = @($mismatches)
        pdfVolumeRestored      = $pdfRestored
        pdfFilesRestoredCount  = $pdfFilesRestored
        durationSeconds        = [math]::Round($restoreSeconds, 1)
    }
    Write-Manifest -Path (Join-Path $SnapshotPath 'restore-result.json') -Object $restoreResult

    # Migrations folder warning
    $migrationsBackup = Join-Path $SnapshotPath 'migrations-backup'
    if (Test-Path -LiteralPath $migrationsBackup) {
        Write-Host ''
        Write-Host '⚠ DB restored to pre-reset state.' -ForegroundColor Yellow
        Write-Host '  Your Migrations/ folder may now conflict with the DB state.' -ForegroundColor Yellow
        Write-Host "  To fully rollback, restore Migrations/ from $migrationsBackup :" -ForegroundColor Yellow
        Write-Host "    Remove-Item -Recurse apps/api/src/Api/Migrations" -ForegroundColor Yellow
        Write-Host "    Copy-Item -Recurse $migrationsBackup apps/api/src/Api/Migrations" -ForegroundColor Yellow
    }

    Write-Timestamped ''
    Write-Timestamped '=== Safety-net rollback complete ==='
    exit 0
}
```

- [ ] **Step 2: Smoke test rollback path**

⚠ This will overwrite the dev DB with the snapshot's safety-net (the original pre-reset state).

```bash
pwsh -c "
\$snap = (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
Write-Host 'yes' | pwsh ./infra/scripts/db-restore-state.ps1 -SnapshotPath \$snap -UseSafetyNet
"
```

Expected: 6 steps complete, all row counts match, warning about Migrations/ folder conflict, `restore-result.json` written with `mode: safety-net`.

- [ ] **Step 3: Restore Migrations/ folder from backup as instructed**

```bash
pwsh -c "
\$snap = (Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
\$migrationsBackup = Join-Path \$snap 'migrations-backup'
\$migrationsTarget = 'apps/api/src/Api/Migrations'
if (Test-Path \$migrationsTarget) { Remove-Item -Recurse -Force \$migrationsTarget }
Copy-Item -Recurse \$migrationsBackup \$migrationsTarget
Write-Host 'Migrations/ restored from backup'
"
```

Expected: Migrations folder restored.

- [ ] **Step 4: Verify build still passes after full rollback**

```bash
dotnet build apps/api/src/Api -c Debug --nologo --verbosity quiet
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/db-restore-state.ps1
git commit -m "feat(infra): db-restore-state -UseSafetyNet full rollback mode"
```

---

## Task 19: Makefile targets

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Read the current Makefile to find the insertion point**

```bash
pwsh -c "Get-Content infra/Makefile | Select-Object -First 145 | Select-Object -Last 30"
```

Look for the end of the `# === Backup & Disaster Recovery ===` section (around line 130-140) and the start of the next section.

- [ ] **Step 2: Insert the new section after Backup & Disaster Recovery**

Use Edit to add this block after the last `backup-cron-show` target and before the next section header:

```makefile

# === DB Snapshot (local dev) ===

db-snapshot: ## Create local DB snapshot before migration reset
	pwsh scripts/db-save-state.ps1

db-snapshot-sanitized: ## Create DB snapshot without sensitive tables
	pwsh scripts/db-save-state.ps1 -Sanitize

db-reset-migrations: ## Reset migrations using snapshot (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-reset-migrations.ps1 -SnapshotPath $(SNAP)

db-restore-snapshot: ## Restore data from snapshot (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-restore-state.ps1 -SnapshotPath $(SNAP)

db-rollback-safetynet: ## Rollback using snapshot safety-net (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-restore-state.ps1 -SnapshotPath $(SNAP) -UseSafetyNet

```

- [ ] **Step 3: Add the new targets to the final `.PHONY` declaration**

Find the `.PHONY:` line near the end of the Makefile (around line 240), and append the new target names:

```
.PHONY: ... db-snapshot db-snapshot-sanitized db-reset-migrations db-restore-snapshot db-rollback-safetynet
```

- [ ] **Step 4: Verify `make help` shows the new targets**

```bash
cd infra && make help | grep "db-"
```

Expected: 5 lines starting with `db-snapshot`, `db-snapshot-sanitized`, `db-reset-migrations`, `db-restore-snapshot`, `db-rollback-safetynet`.

- [ ] **Step 5: Verify `make db-reset-migrations` without SNAP shows the error**

```bash
cd infra && make db-reset-migrations
```

Expected: `ERROR: SNAP=<snapshot-path> required`, exit 1.

- [ ] **Step 6: Commit**

```bash
git add infra/Makefile
git commit -m "feat(infra): add db-snapshot Makefile targets"
```

---

## Task 20: Runbook + scripts/README update

**Files:**
- Create: `docs/operations/migration-consolidation-runbook.md`
- Modify: `infra/scripts/README.md`

- [ ] **Step 1: Create the runbook**

Create `docs/operations/migration-consolidation-runbook.md`:

```markdown
# Migration Consolidation Runbook

> **When to use:** You have an EF Core project with many accumulated migrations and want to collapse them into a single new `Initial` migration without losing local dev data. Local dev only — never on staging or production.

## TL;DR

```bash
# 1. Save current state
pwsh infra/scripts/db-save-state.ps1

# 2. Note the snapshot path printed at the end, then reset migrations
pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath infra/db-snapshots/<timestamp>

# 3. Restore data into the fresh schema
pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath infra/db-snapshots/<timestamp>
```

If anything goes wrong at any step:

```bash
pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath infra/db-snapshots/<timestamp> -UseSafetyNet
# Then restore Migrations/ from <snapshot>/migrations-backup/ if needed
```

## Preconditions

| # | Check | How to verify |
|---|-------|--------------|
| P1 | `Migrations/` is committed in git | `git status apps/api/src/Api/Migrations` |
| P2 | No pending unapplied migrations | `dotnet ef migrations list --project apps/api/src/Api` |
| P3 | API project compiles | `dotnet build apps/api/src/Api` |
| P4 | API container is stopped (or expect a warning) | `docker ps \| grep meepleai-api` |
| P5 | No long-running queries on the dev DB | check active connections |

## Detailed steps

### Step 1 — Save state

```bash
pwsh infra/scripts/db-save-state.ps1
```

This creates `infra/db-snapshots/<timestamp>/` containing:

- `safety-net.dump` — full schema+data (your insurance policy)
- `data.dump` — data only, used for restore into the new schema
- `schema-pre.sql` — schema before reset, used for drift detection
- `rowcounts-pre.tsv` — exact row counts per table
- `pdf_uploads.tar.gz` — PDF volume backup (if local storage)
- `manifest.json` — immutable metadata with SHA256 hashes

Pass `-Sanitize` if you plan to share the snapshot off-machine (excludes `Users`, `Sessions`, `RefreshTokens`, `ApiKeys`, `AuditLogs` from `data.dump` only — `safety-net.dump` is always full).

### Step 2 — Reset migrations

```bash
pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath infra/db-snapshots/<timestamp>
```

This wraps:

1. `dotnet ef database drop --force`
2. Backup `Migrations/` to `<snapshot>/migrations-backup/`
3. Delete `Migrations/`
4. `dotnet ef migrations add Initial`
5. `dotnet ef database update`
6. Dump `schema-post.sql`, write `reset-result.json`

A pre-flight check runs `dotnet build` first and refuses to proceed if it fails (because you'd end up with a dropped DB and a deleted Migrations folder if the build is broken).

You will be asked to type `yes` to confirm.

### Step 3 — Restore data

```bash
pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath infra/db-snapshots/<timestamp>
```

This:

1. Re-dumps `schema-post.sql` from the live DB
2. Normalizes both `schema-pre` and `schema-post`
3. Diffs them and classifies as `identical`, `minor`, or `significant`
4. If `significant` → STOP, prints diff, exits with code 2
5. Otherwise restores `data.dump` via `pg_restore --data-only --disable-triggers --single-transaction`
6. Resets all sequences via `pg_depend`
7. Verifies row counts against `rowcounts-pre.tsv`
8. Restores PDF volume (if present)
9. Writes `restore-result.json`

## Failure recovery

### Build fails before drop

Pre-flight check rejects the operation. Fix compile errors and retry — DB and Migrations are unchanged.

### `dotnet ef migrations add` fails after drop

DB is dropped, Migrations/ is deleted. The script prints recovery instructions:

```bash
# Restore migrations from backup
Copy-Item -Recurse <snapshot>/migrations-backup <api>/Migrations
# Fix model code, retry
pwsh db-reset-migrations.ps1 -SnapshotPath <snapshot>
```

Or full rollback via safety net:

```bash
pwsh db-restore-state.ps1 -SnapshotPath <snapshot> -UseSafetyNet
```

### Schema drift = significant

Your model code generates a different schema than the saved snapshot. Options:

- **(a)** Fix the model code so the new schema matches the old one, re-run reset
- **(b)** Rollback via safety net
- **(c)** Force restore with `-AllowDrift` (will likely fail on missing/renamed columns)

### `pg_restore` data fails

Single-transaction restore: nothing is committed. DB stays in fresh empty state. Investigate the error, fix, retry — or rollback via safety net.

## Cleanup

Snapshots are stored in `infra/db-snapshots/<timestamp>/` and gitignored. To free space:

```bash
# Delete snapshots older than 30 days
Get-ChildItem infra/db-snapshots -Directory | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item -Recurse -Force
```

## Hard guarantees

- **Localhost only**: every script aborts if `POSTGRES_HOST` is anything other than `localhost` or `127.0.0.1`.
- **Atomic restores**: `pg_restore --single-transaction` means a failed restore leaves the DB in its pre-restore state, never partial.
- **Hash verification**: dump file SHA256 hashes are written to `manifest.json` at save time and re-verified before every restore operation.
- **Build sanity check**: `db-reset-migrations` refuses to drop the DB if `dotnet build` fails.
```

- [ ] **Step 2: Update `infra/scripts/README.md`**

Read the current `infra/scripts/README.md` to find the right place to insert the new entries:

```bash
pwsh -c "Get-Content infra/scripts/README.md"
```

Add a new section (or append to an existing scripts table) describing the three new scripts. Example block to insert (adjust to match the existing structure):

```markdown
## Migration Consolidation (local dev only)

| Script | Purpose |
|--------|---------|
| `db-save-state.ps1` | Save local DB state before consolidating EF migrations |
| `db-reset-migrations.ps1` | Drop DB, delete Migrations/, create fresh Initial |
| `db-restore-state.ps1` | Restore data into the fresh schema, with drift check and rollback |
| `db-snapshot-common.psm1` | Shared PowerShell module (helpers + pure functions) |
| `tests/db-snapshot-common.Tests.ps1` | Pester tests for module pure functions |

See [docs/operations/migration-consolidation-runbook.md](../../docs/operations/migration-consolidation-runbook.md) for the workflow.
```

- [ ] **Step 3: Verify the runbook renders cleanly**

```bash
pwsh -c "Get-Content docs/operations/migration-consolidation-runbook.md | Select-Object -First 20"
```

Expected: markdown header and TL;DR section visible.

- [ ] **Step 4: Commit**

```bash
git add docs/operations/migration-consolidation-runbook.md infra/scripts/README.md
git commit -m "docs(ops): migration consolidation runbook + scripts README update"
```

---

## Task 21: Final integration smoke test

This is the final end-to-end validation. No code commits — just verification that the whole system works.

**Files:** none (verification only)

- [ ] **Step 1: Stop the API container if it's running**

```bash
docker stop meepleai-api 2>/dev/null || true
```

- [ ] **Step 2: Run the full happy path**

```bash
# 1. Save
pwsh infra/scripts/db-save-state.ps1

# 2. Get the latest snapshot path
SNAP=$(pwsh -c "(Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName")
echo "Snapshot: $SNAP"

# 3. Reset (auto-confirm with yes)
echo "yes" | pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath "$SNAP"

# 4. Restore
pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath "$SNAP"
```

Expected:
- Step 1 produces a valid snapshot with safety-net, data, schema-pre, rowcounts, pdf (if local), manifest.json
- Step 3 produces a fresh schema, single Initial migration, reset-result.json, schema-post.sql
- Step 4 reports `identical` drift and `restore-result.json` with `rowCountMatch: true`

- [ ] **Step 3: Validate that the API container can start with the new schema**

```bash
docker start meepleai-api
sleep 5
curl -fsS http://localhost:8080/health
```

Expected: `200 OK` from `/health`.

- [ ] **Step 4: Test the rollback path**

```bash
# 1. Save again
pwsh infra/scripts/db-save-state.ps1
SNAP2=$(pwsh -c "(Get-ChildItem infra/db-snapshots -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName")

# 2. Reset
echo "yes" | pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath "$SNAP2"

# 3. Rollback via safety net
echo "yes" | pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath "$SNAP2" -UseSafetyNet

# 4. Restore Migrations/ from backup
pwsh -c "
Remove-Item -Recurse -Force apps/api/src/Api/Migrations
Copy-Item -Recurse '$SNAP2/migrations-backup' apps/api/src/Api/Migrations
"

# 5. Verify build still passes
dotnet build apps/api/src/Api -c Debug --nologo
```

Expected: rollback succeeds, row counts match, Migrations/ restored, build passes.

- [ ] **Step 5: Run the full Pester test suite one final time**

```bash
pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"
```

Expected: 46 tests pass, 0 failures.

- [ ] **Step 6: Verify no stray files were committed**

```bash
git status
```

Expected: clean working tree (or only intentional changes).

- [ ] **Step 7: (Optional) Open a PR**

```bash
git push -u origin <feature-branch>
gh pr create --base main-dev --title "feat(infra): db state save/restore for migration consolidation" --body "Implements docs/superpowers/specs/2026-04-08-db-state-save-restore-design.md"
```

---

## Spec coverage check (self-review)

| Spec section | Plan task(s) |
|---|---|
| Goal: 3-step PowerShell workflow | Tasks 11–18 |
| Non-goals (UI, R2, cross-env, etc.) | Honored throughout, no out-of-scope work |
| User Decisions Recorded | All 12 decisions implemented |
| Preconditions P1–P6 | Task 14 pre-flight + Task 20 runbook |
| Architecture overview (3 scripts + module + snapshot dir) | Tasks 1, 11, 14, 16 |
| Worked Examples (happy path, drift) | Task 20 runbook |
| Component 1 db-save-state | Tasks 11, 12, 13 |
| Component 2 db-reset-migrations | Tasks 14, 15 |
| Component 3 db-restore-state | Tasks 16, 17, 18 |
| Component 4 db-snapshot-common.psm1 | Tasks 1–10 |
| Component 5 Makefile | Task 19 |
| File changes summary | All files in Task File Map |
| Error matrix | Implemented across script error handlers |
| Testing strategy (Pester + smoke) | Pester in Tasks 2–9, smoke tests in 11–21 |
| Security (localhost, hash verify) | Task 5, Task 14 step 1, Task 16 step 1 |

All spec sections have at least one task. No gaps.
