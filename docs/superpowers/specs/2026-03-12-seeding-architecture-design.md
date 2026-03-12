# Hybrid Layered Seeding Architecture

**Date**: 2026-03-12
**Status**: Approved (revised after spec-panel review)
**Scope**: Backend seeding system redesign — multi-environment, YAML manifests, dump/restore
**Review**: 2026-03-12 — Expert panel review (Wiegers, Fowler, Nygard, Adzic, Crispin) — Score 7.4/10

## Problem

The current seeding system works but has grown organically:
- 8 infrastructure seeders + 7 MediatR commands, all in flat `Infrastructure/Seeders/`
- No environment differentiation (same seed for dev, staging, prod)
- No dump/restore capability between environments
- PDF seeding creates DB entries but does not trigger the RAG pipeline (extract → chunk → embed → Qdrant)
- No "lived-in" test data for staging demos

## Requirements

| Requirement | Description |
|-------------|-------------|
| **Multi-environment** | Three profiles: dev (light, fast), staging (full + lived-in data), prod (full, no test data) |
| **RAG-ready after seed** | Games + PDFs uploaded + chunking done + embeddings in Qdrant + agent configured |
| **Dump/restore** | Create snapshots (PostgreSQL + Qdrant) and restore to any environment |
| **Bidirectional flow** | prod → staging → dev (pull down) and dev → staging → prod (build up) |
| **No sanitization** | All environments trusted, no PII anonymization needed (V1 scope limitation — revisit if real user data enters staging/prod) |
| **Dev auto-seed** | Dev profile runs automatically on startup (like today) |
| **Staging/prod via scripts** | PowerShell scripts for dump/restore operations |
| **Catalog size** | Dev: 3-5 games, 1-2 PDFs. Staging/Prod: 20-30 games, 5-10 PDFs |

## Design

### Profiles

Controlled by `SEED_PROFILE` environment variable (default: `dev`):

| Profile | Layers | Method | Auto on startup |
|---------|--------|--------|-----------------|
| `dev` | Core + Catalog(dev.yml) | On-the-fly pipeline | Yes |
| `staging` | Core + Catalog(staging.yml) + LivedIn | Restore from dump | No (script) |
| `prod` | Core + Catalog(prod.yml) | Restore from dump | No (script) |
| `none` | None | — | Skips seeding |

### Layer Architecture

Three composable layers:

**Layer 1 — Core** (always runs):
- Admin user (from `admin.secret`)
- Test users + E2E test users
- AI models (OpenRouter + Ollama)
- Feature flags (12 flags with tier-based access)
- Rate limit configurations (Free/Premium/Pro)
- Predefined badges

**Layer 2 — Catalog** (driven by YAML manifest):
- Shared games from BGG API → creates `SharedGameEntity` records
- Game entities in GameManagement context (bridged from SharedGame, as `CatanPocAgentSeeder` does today)
- PDF rulebook documents (file copy + DB entry) — FK links to `GameEntity.Id` (GameManagement), not `SharedGameEntity.Id`
- RAG pipeline trigger (deferred: PdfSeeder creates docs in `Pending` state → existing `PdfProcessingQuartzJob` processes automatically)
- Strategy patterns for AI agent decision-making (gated by `Seeding:EnableStrategyPatterns` config, requires `IEmbeddingService`)
- Agent configurations (merges DefaultAgentSeeder + CatanPocAgentSeeder + SeedAgentDefinitionsCommand — see [Agent Merge Strategy](#agent-merge-strategy))

**Layer 3 — LivedIn** (staging only):
- User libraries: 5-15 games per user (mix of owned/wishlist/played-before)
- Play records: 10-50 per user, realistic scores, distributed over trailing 3 months
- Chat history: 3-5 conversations per user using real game context (not lorem-ipsum)
- Badge assignments: subset of predefined badges matching play history

### File Structure

```
apps/api/src/Api/Infrastructure/Seeders/
├── Core/
│   ├── CoreSeeder.cs                ← Orchestrates all core seeders
│   ├── AdminUserSeeder.cs           ← From SeedAdminUserCommandHandler
│   ├── TestUserSeeder.cs            ← From SeedTestUserCommandHandler
│   ├── AiModelSeeder.cs             ← From SeedAiModelsCommandHandler
│   ├── FeatureFlagSeeder.cs         ← Existing (moved)
│   ├── RateLimitConfigSeeder.cs     ← Existing (moved)
│   └── BadgeSeeder.cs               ← Existing (moved)
│
├── Catalog/
│   ├── CatalogSeeder.cs             ← Engine that reads YAML manifest
│   ├── GameSeeder.cs                ← From SharedGameSeeder (manifest-driven, creates SharedGameEntity + GameEntity bridge)
│   ├── PdfSeeder.cs                 ← From PdfRulebookSeeder (manifest-driven, FK → GameEntity.Id)
│   ├── (RagPipelineSeeder removed — existing PdfProcessingQuartzJob handles RAG automatically)
│   ├── StrategyPatternSeeder.cs     ← Existing (moved, config-gated)
│   ├── AgentSeeder.cs               ← Merges DefaultAgent + CatanPoc + AgentDefinitions
│   └── Manifests/
│       ├── dev.yml                  ← 3-5 games
│       ├── staging.yml              ← 20-30 games
│       └── prod.yml                 ← 20-30 games
│
├── LivedIn/
│   ├── LivedInSeeder.cs             ← Orchestrates lived-in seeders
│   ├── UserLibrarySeeder.cs
│   ├── PlayRecordSeeder.cs
│   ├── ChatHistorySeeder.cs
│   └── GamificationSeeder.cs
│
├── SeedProfile.cs                   ← Enum: Dev | Staging | Prod | None
├── SeedOrchestrator.cs              ← Entry point (replaces seeding in AutoConfigurationService)
└── SeedManifest.cs                  ← YAML deserialization model

scripts/
├── seed-dump.ps1                    ← pg_dump + Qdrant snapshot → .tar.gz
├── seed-restore.ps1                 ← Restore from dump to target env
└── seed-pull.ps1                    ← Pull dump from remote env
```

### YAML Manifest Format

```yaml
profile: dev
catalog:
  games:
    - title: "Catan"
      bggId: 13
      language: en
      pdf: "catan_rulebook.pdf"
      seedAgent: true
      fallbackImageUrl: "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/IwRwEpu1I6YfkyYjFIekCh80ntc=/0x0/filters:format(jpeg)/pic2419375.jpg"
      fallbackThumbnailUrl: "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__small/img/7a0LOL48K-2lC3IG0HyYT3XxJBs=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg"
    - title: "Wingspan"
      bggId: 266192
      language: en
      pdf: "wingspan_en_rulebook.pdf"
      seedAgent: false
      fallbackImageUrl: "https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__original/img/cI782Zis9cT66j2MjSHKJGnFPNw=/0x0/filters:format(jpeg)/pic4458123.jpg"
      fallbackThumbnailUrl: "https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__small/img/VNToqgS2-pOGU6MuvIkMPKn_y-s=/fit-in/200x150/filters:strip_icc()/pic4458123.jpg"
    - title: "Azul"
      bggId: 230802
      language: en
      pdf: "azul_rulebook.pdf"
      seedAgent: false
      fallbackImageUrl: "https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__original/img/AkbtYVc6xXJF3c9EUrakklcclKw=/0x0/filters:format(png)/pic6973671.png"
      fallbackThumbnailUrl: "https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__small/img/ccsXKrdGJw-YSClWwzVUwk5Nh9Y=/fit-in/200x150/filters:strip_icc()/pic6973671.png"

  defaultAgent:
    name: "MeepleAssistant POC"
    model: "anthropic/claude-3-haiku"
    temperature: 0.3
    maxTokens: 2048
```

Fields `fallbackImageUrl` and `fallbackThumbnailUrl` are optional. Used when BGG API is unavailable to avoid placeholder images. Values ported from the existing `SharedGameSeeder.GameMappings`.

#### Manifest Validation Rules

Validated at parse time (before any seeding begins). Invalid manifests abort the Catalog layer.

| Field | Required | Validation | On Failure |
|-------|----------|------------|------------|
| `profile` | Yes | Must match `SEED_PROFILE` env var | Fatal — manifest/env mismatch |
| `games[].title` | Yes | Non-empty string | Fatal — skip entire game entry |
| `games[].bggId` | Yes | Positive integer, unique within manifest | Fatal — duplicate bggId |
| `games[].language` | Yes | ISO 639-1 code (2 chars) | Warn, default `"en"` |
| `games[].pdf` | No | File exists in `data/rulebook/` | Warn, skip PDF seeding for this game |
| `games[].seedAgent` | No | Boolean | Default `false` |
| `games[].fallbackImageUrl` | No | Valid URL format | Warn, use placeholder |
| `games[].fallbackThumbnailUrl` | No | Valid URL format | Warn, use placeholder |
| `defaultAgent.name` | Yes (if any `seedAgent: true`) | Non-empty string | Fatal |
| `defaultAgent.model` | Yes (if any `seedAgent: true`) | Non-empty string | Fatal |

### SeedOrchestrator

```csharp
internal sealed class SeedOrchestrator
{
    private readonly SeedProfile _profile;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeedOrchestrator> _logger;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (_profile == SeedProfile.None) return;

        _logger.LogInformation("Seeding with profile: {Profile}", _profile);
        var sw = Stopwatch.StartNew();

        // Layer 1: Core (always) — isolated scope for ChangeTracker safety
        using (var coreScope = _scopeFactory.CreateScope())
        {
            var sp = coreScope.ServiceProvider;
            await CoreSeeder.SeedAsync(
                sp.GetRequiredService<IMediator>(),
                sp.GetRequiredService<MeepleAiDbContext>(),
                _logger, ct);
        }

        // Layer 2: Catalog (from YAML manifest) — isolated scope
        using (var catalogScope = _scopeFactory.CreateScope())
        {
            var sp = catalogScope.ServiceProvider;
            await CatalogSeeder.SeedAsync(_profile,
                sp.GetRequiredService<MeepleAiDbContext>(),
                sp.GetRequiredService<IBggApiService>(),
                sp.GetRequiredService<IPdfProcessingPipelineService>(),
                sp.GetRequiredService<IEmbeddingService>(),
                _logger, ct);
        }

        // Layer 3: LivedIn (staging only) — isolated scope
        if (_profile == SeedProfile.Staging)
        {
            using var livedInScope = _scopeFactory.CreateScope();
            await LivedInSeeder.SeedAsync(
                livedInScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
                _logger, ct);
        }

        _logger.LogInformation("Seeding completed in {Elapsed}ms", sw.ElapsedMilliseconds);
    }
}
```

### RAG Pipeline Integration (No Dedicated Seeder Needed)

The existing `PdfProcessingQuartzJob` automatically processes any PDF in `Pending` state. The only change needed is in `PdfSeeder`: create documents with `ProcessingState = nameof(PdfProcessingState.Pending)` instead of `"Ready"`.

```csharp
// PdfSeeder creates docs in Pending state (Phase 3 change):
ProcessingState = nameof(PdfProcessingState.Pending),  // was "Ready"
ProcessingStatus = "pending",                           // was "completed"
```

The existing background jobs handle the rest:
1. `PdfProcessingQuartzJob` (every 10s) picks up `Pending` PDFs → processes through `IPdfProcessingPipelineService`
2. On failure → `RetryFailedPdfsJob` (every 5min) retries with exponential backoff
3. `[DisallowConcurrentExecution]` on both jobs prevents double-processing

**No `RagPipelineSeeder` class needed.** This is the simplest possible integration.

### Idempotency Contracts

Each catalog sub-seeder must be idempotent for safe re-runs on every dev startup:

| Sub-Seeder | Skip Condition | On Conflict |
|------------|----------------|-------------|
| `GameSeeder` | SharedGame with matching `BggId` exists | Update fallback images if changed in manifest |
| `PdfSeeder` | PDF with matching `GameId` + `FileName` exists | Skip (no re-upload). New PDFs created in `Pending` state for background processing. |
| `AgentSeeder` | Agent with matching `Name` exists | Skip (preserve manual config changes) |
| `StrategyPatternSeeder` | Strategy pattern with matching game + name exists | Skip |

### Agent Merge Strategy

The `AgentSeeder` merges three existing sources with different responsibilities:

| Source | What It Creates | When Applied |
|--------|----------------|-------------|
| `DefaultAgentSeeder` | Global `MeepleAssistant POC` agent (from `defaultAgent` YAML section) | Always — one per environment |
| `CatanPocAgentSeeder` | Full domain stack: GameEntity + AgentTypology + Agent + AgentConfiguration + GameSession + AgentSession | Only for games with `seedAgent: true` |
| `SeedAgentDefinitionsCommand` | Agent definitions for Playground POC | Merged into AgentSeeder, applied globally |

**Per-game `seedAgent: true` behavior**: Creates the full 6-entity chain (matching `CatanPocAgentSeeder` pattern) — GameEntity bridge from SharedGame, AgentTypology "Rules Expert", Agent with RAG type, AgentConfiguration with game-specific system prompt (includes `{RAG_CONTEXT}` placeholder), GameSession, and AgentSession.

**Per-game `seedAgent: false` behavior**: Creates only GameEntity bridge from SharedGame. No agent, no session.

### RAG Pipeline (Deferred to Background Jobs)

RAG processing is **not part of the seeding startup path**. Instead, it leverages existing Quartz jobs:

| Job | Schedule | Responsibility | Config |
|-----|----------|----------------|--------|
| `PdfProcessingQuartzJob` | Every 10s | Picks up `Pending` PDFs, processes through pipeline | `MaxConcurrentWorkers`, `[DisallowConcurrentExecution]` |
| `RetryFailedPdfsJob` | Every 5min | Retries `Failed` PDFs with exponential backoff (1s, 2s, 4s) | Max 3 retries, transient errors only |

This means dev startup completes in seconds. RAG processing happens asynchronously after the app is serving requests.

### Error Isolation

Each layer runs in its own `DbContext` scope to prevent `ChangeTracker` leaks between layers:

```csharp
// Layer 1
using (var coreScope = _scopeFactory.CreateScope())
{
    var db = coreScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    await CoreSeeder.SeedAsync(db, ...);
}  // scope disposed — ChangeTracker cleared

// Layer 2
using (var catalogScope = _scopeFactory.CreateScope())
{
    var db = catalogScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    await CatalogSeeder.SeedAsync(db, ...);
}
```

This prevents partially-tracked entities from Layer 1 failures corrupting Layer 2 operations.

### Program.cs Integration

```csharp
// Replace:
await autoConfigService.InitializeAsync(cancellationToken);

// With (SeedOrchestrator is singleton — creates its own scopes internally per layer):
var seedOrchestrator = app.Services.GetRequiredService<SeedOrchestrator>();
await seedOrchestrator.ExecuteAsync(cancellationToken);
```

DI registration:

```csharp
services.AddSingleton(_ =>
    Enum.TryParse<SeedProfile>(
        configuration["SEED_PROFILE"] ?? "dev",
        ignoreCase: true,
        out var profile) ? profile : SeedProfile.Dev);

// SeedOrchestrator depends on IServiceScopeFactory (singleton-safe) and SeedProfile (singleton).
// Each layer creates its own scope internally for ChangeTracker isolation.
services.AddSingleton<SeedOrchestrator>();
```

Note: `SeedOrchestrator` is now `Singleton` because it no longer depends directly on scoped services. Instead, it uses `IServiceScopeFactory` to create isolated scopes per layer. This simplifies the `Program.cs` integration (no explicit `CreateScope()` needed).

### PowerShell Scripts

#### seed-dump.ps1

Creates a complete snapshot (PostgreSQL + Qdrant):

```powershell
param(
    [ValidateSet("dev", "staging", "prod")]
    [string]$Env = "dev",
    [string]$Output = "./dumps",
    [switch]$IncludeQdrant = $true
)

# 1. pg_dump (excludes session tokens and migration history)
docker exec meepleai-postgres pg_dump -U postgres -d meepleai `
    --exclude-table="session_tokens" `
    --exclude-table="__EFMigrationsHistory" `
    -F c -f /tmp/meepleai-dump.backup
docker cp meepleai-postgres:/tmp/meepleai-dump.backup "$Output/"

# 2. Qdrant snapshot via REST API
if ($IncludeQdrant) {
    Invoke-RestMethod -Method POST -Uri "http://localhost:6333/collections/meepleai/snapshots"
    $snapshot = Invoke-RestMethod "http://localhost:6333/collections/meepleai/snapshots"
    $latest = $snapshot.result | Sort-Object creation_time -Descending | Select-Object -First 1
    Invoke-WebRequest "http://localhost:6333/collections/meepleai/snapshots/$($latest.name)" `
        -OutFile "$Output/qdrant-snapshot.snapshot"
}

# 3. Generate checksum for integrity verification
$hash = Get-FileHash "$Output/meepleai-dump.backup" -Algorithm SHA256
$qdrantHash = if ($IncludeQdrant) { (Get-FileHash "$Output/qdrant-snapshot.snapshot" -Algorithm SHA256).Hash } else { $null }

# 4. Pack with metadata (includes checksums)
@{
    env = $Env
    date = Get-Date -Format "yyyy-MM-dd"
    version = "1.0"
    checksums = @{
        postgres = $hash.Hash
        qdrant = $qdrantHash
    }
} | ConvertTo-Json | Out-File "$Output/metadata.json"
tar -czf "$Output/meepleai-$Env-$(Get-Date -Format 'yyyy-MM-dd').tar.gz" `
    -C "$Output" meepleai-dump.backup qdrant-snapshot.snapshot metadata.json
```

#### seed-restore.ps1

Restores from dump to target environment:

```powershell
param(
    [Parameter(Mandatory)][string]$From,
    [ValidateSet("dev", "staging", "prod")][string]$To = "dev",
    [switch]$Force,
    [switch]$DryRun
)

# 1. Unpack and read metadata
tar -xzf $From -C ./tmp-restore/
$metadata = Get-Content ./tmp-restore/metadata.json | ConvertFrom-Json

# 2. Environment direction guard — block restoring lower env dumps to higher envs
$envRank = @{ "dev" = 0; "staging" = 1; "prod" = 2 }
if ($envRank[$metadata.env] -lt $envRank[$To] -and $To -ne "dev") {
    Write-Error "BLOCKED: Cannot restore '$($metadata.env)' dump to '$To' (lower → higher env). Use seed-dump from '$To' instead."
    Remove-Item -Recurse ./tmp-restore/
    exit 1
}

# 3. Verify checksums
if ($metadata.checksums) {
    $pgHash = (Get-FileHash ./tmp-restore/meepleai-dump.backup -Algorithm SHA256).Hash
    if ($pgHash -ne $metadata.checksums.postgres) {
        Write-Error "CHECKSUM MISMATCH: PostgreSQL dump corrupted during transfer."
        Remove-Item -Recurse ./tmp-restore/
        exit 1
    }
    Write-Host "Checksum verified for PostgreSQL dump."
}

# 4. Dry-run mode — show what would happen without executing
if ($DryRun) {
    Write-Host "DRY RUN — would restore '$($metadata.env)' dump (dated $($metadata.date)) to '$To'"
    Write-Host "  - PostgreSQL: dropdb + createdb + pg_restore"
    Write-Host "  - Qdrant: $(if (Test-Path ./tmp-restore/qdrant-snapshot.snapshot) { 'delete + upload snapshot' } else { 'no snapshot' })"
    Write-Host "  - EF Migrations: dotnet ef database update"
    Remove-Item -Recurse ./tmp-restore/
    exit 0
}

# 5. Confirm destructive action
if (-not $Force) {
    $confirm = Read-Host "This will REPLACE all data in '$To' with '$($metadata.env)' dump from $($metadata.date). Continue? (yes/no)"
    if ($confirm -ne "yes") { Remove-Item -Recurse ./tmp-restore/; exit }
}
docker exec meepleai-postgres dropdb -U postgres --if-exists meepleai
docker exec meepleai-postgres createdb -U postgres meepleai
docker cp ./tmp-restore/meepleai-dump.backup meepleai-postgres:/tmp/
docker exec meepleai-postgres pg_restore -U postgres -d meepleai /tmp/meepleai-dump.backup

if (Test-Path ./tmp-restore/qdrant-snapshot.snapshot) {
    Invoke-RestMethod -Method DELETE "http://localhost:6333/collections/meepleai"
    # Qdrant snapshot upload requires multipart/form-data (matches existing restore-qdrant.sh)
    Invoke-RestMethod -Method POST `
        "http://localhost:6333/collections/meepleai/snapshots/upload" `
        -Form @{ snapshot = Get-Item ./tmp-restore/qdrant-snapshot.snapshot }
}

# Connection string resolution: uses the environment's appsettings.json / env vars.
# For staging/prod, set ASPNETCORE_ENVIRONMENT or pass --connection explicitly.
# Dev default uses local Docker PostgreSQL (from appsettings.Development.json).
$envFlag = if ($To -ne "dev") { "--environment $To" } else { "" }
Invoke-Expression "dotnet ef database update --project apps/api/src/Api/ $envFlag"
Remove-Item -Recurse ./tmp-restore/
```

#### seed-pull.ps1

Pulls dump from remote environment:

```powershell
param(
    [Parameter(Mandatory)][ValidateSet("prod", "staging")][string]$Source,
    [ValidateSet("dev", "staging")][string]$Target = "dev"
)

scp "deploy@meepleai.app:/dumps/meepleai-$Source-latest.tar.gz" ./dumps/
& ./scripts/seed-restore.ps1 -From "./dumps/meepleai-$Source-latest.tar.gz" -To $Target
```

### Error Handling

| Layer | Error | Behavior |
|-------|-------|----------|
| Core | Admin user creation fails | **Fatal** — blocks startup |
| Core | Feature flags/badges/rate limits | **Log + continue** |
| Catalog | BGG API timeout | **Fallback** → minimal game from manifest data |
| Catalog | PDF file not found | **Log + skip** |
| Catalog | Embedding service down | **Not blocking**: PDFs stay in `Pending` state. `PdfProcessingQuartzJob` retries every 10s. `RetryFailedPdfsJob` retries failed with backoff (1s, 2s, 4s). |
| Catalog | Qdrant down | Same: non-blocking. Background jobs handle retry. |
| LivedIn | Any error | **Log + continue** — optional data |
| Dump/Restore | pg_restore fail | **Abort** with recovery instructions |
| Dump/Restore | Qdrant snapshot fail | **Warn** — DB ok but RAG unavailable |

Startup dependency check before Layer 2 (informational only — RAG is deferred to background jobs):

```csharp
var qdrantOk = await _healthCheck.CheckQdrantAsync(ct);
var embeddingOk = await _healthCheck.CheckEmbeddingServiceAsync(ct);

if (!qdrantOk || !embeddingOk)
{
    _logger.LogWarning("RAG services unavailable at startup. PDFs will be seeded in Pending state. " +
        "PdfProcessingQuartzJob will process them when services become available.");
}
```

### Testing

```
tests/Api.Tests/Infrastructure/Seeders/
├── Core/
│   └── CoreSeederTests.cs              ← All core seeders idempotent
├── Catalog/
│   ├── CatalogSeederTests.cs           ← Manifest parsing + game creation
│   ├── ManifestParserTests.cs          ← YAML parsing, validation, edge cases
│   ├── PdfSeederTests.cs              ← PDFs created in Pending state, idempotency
│   └── AgentSeederTests.cs            ← Agent merge strategy, seedAgent true/false
├── LivedIn/
│   └── LivedInSeederTests.cs           ← Lived-in data correctness
├── SeedOrchestratorTests.cs            ← Profiles, skip logic, dependency checks
└── Manifests/
    └── ManifestValidationTests.cs      ← Invalid YAML, duplicate games, missing PDFs
```

### Seeder Migration Map

Every existing seeder mapped to its new location:

| Current Seeder | New Location | Layer | Notes |
|----------------|-------------|-------|-------|
| `SeedAdminUserCommandHandler` | `Core/AdminUserSeeder.cs` | Core | Static method, no MediatR |
| `SeedTestUserCommandHandler` | `Core/TestUserSeeder.cs` | Core | Static method, no MediatR |
| `SeedE2ETestUsersCommandHandler` | `Core/TestUserSeeder.cs` | Core | Merged into TestUserSeeder |
| `SeedAiModelsCommandHandler` | `Core/AiModelSeeder.cs` | Core | Static method, no MediatR |
| `FeatureFlagSeeder` | `Core/FeatureFlagSeeder.cs` | Core | Move only |
| `RateLimitConfigSeeder` | `Core/RateLimitConfigSeeder.cs` | Core | Move only |
| `BadgeSeeder` | `Core/BadgeSeeder.cs` | Core | Move only |
| `SharedGameSeeder` | `Catalog/GameSeeder.cs` | Catalog | Manifest-driven, creates SharedGame + Game bridge |
| `PdfRulebookSeeder` | `Catalog/PdfSeeder.cs` | Catalog | Manifest-driven, creates PDFs in `Pending` state |
| `CatanPocAgentSeeder` | `Catalog/AgentSeeder.cs` | Catalog | Merged with DefaultAgent + AgentDefinitions |
| `DefaultAgentSeeder` | `Catalog/AgentSeeder.cs` | Catalog | Merged |
| `SeedAgentDefinitionsCommandHandler` | `Catalog/AgentSeeder.cs` | Catalog | Merged, static method |
| `StrategyPatternSeeder` | `Catalog/StrategyPatternSeeder.cs` | Catalog | Move, keep config gate |
| *(removed)* `RagPipelineSeeder` | — | — | Not needed: existing `PdfProcessingQuartzJob` processes `Pending` PDFs automatically |

### Migration Path

Seven phases. Phases 1-5 are independently deployable. Phases 6-7 depend on all prior phases.

1. **Phase 1**: Create `Core/`, `Catalog/`, `LivedIn/`, `SeedOrchestrator` structure. New files delegate to existing seeders.
2. **Phase 2**: Move logic from old seeders to new ones (refactor, not rewrite). `SharedGameSeeder.GameMappings` → `dev.yml` manifest. Port all seeders per migration map above.
3. **Phase 3**: Update `PdfSeeder` to create documents in `Pending` state (not `Ready`). Existing `PdfProcessingQuartzJob` will automatically pick up and process these PDFs through the RAG pipeline. No new `RagPipelineSeeder` needed.
4. **Phase 4**: Add `LivedIn/` seeders — user libraries, play records, chat history, gamification.
5. **Phase 5**: PowerShell scripts for dump/restore/pull.
6. **Phase 6** *(depends on Phases 1-5)*: Remove old seeders, update `Program.cs`, clean up `AutoConfigurationService`. Cannot ship until all new seeders are verified working.
7. **Phase 7** *(depends on Phases 1-5)*: Tests for all new seeders, manifest validation, orchestrator profiles. Minimum coverage required before Phase 6 can proceed.

### Docker Compose

```yaml
services:
  api:
    environment:
      - SEED_PROFILE=${SEED_PROFILE:-dev}
```

For dump/restore, SEED_PROFILE=none to skip auto-seeding when restoring from dump.

## Dependencies

- **YAML parsing**: `YamlDotNet` NuGet package (or similar)
- **Embedding service**: Must be running for RAG pipeline (dev profile)
- **Qdrant**: Must be running for RAG pipeline (dev profile)
- **BGG API**: Required for game metadata (fallback to minimal data if unavailable)
- **PDF files**: Must exist in `data/rulebook/` for referenced games

## Data Flow

```
Dev startup:
  SEED_PROFILE=dev → SeedOrchestrator (with pg_advisory_lock)
    → CoreSeeder (admin, flags, rates, badges, models)
    → CatalogSeeder reads dev.yml
      → GameSeeder (3-5 games from BGG)
      → PdfSeeder (1-2 PDFs copied + DB entry in "Pending" state)
      → AgentSeeder (MeepleAssistant + per-game agents)
    App starts accepting requests immediately
    → PdfProcessingQuartzJob (background, every 10s) picks up Pending PDFs
      → extract → chunk → embed → Qdrant (async, non-blocking)

Staging/Prod restore:
  SEED_PROFILE=none → skip auto-seed
  pwsh seed-restore.ps1 -From dump.tar.gz -To staging
    → pg_restore (full database)
    → Qdrant snapshot restore
    → dotnet ef database update (apply pending migrations)

Environment pull:
  pwsh seed-pull.ps1 -Source prod -Target dev
    → SCP dump from remote
    → seed-restore.ps1
```

## Open Questions (from spec-panel review)

| # | Question | Impact | Status | Resolution |
|---|----------|--------|--------|------------|
| 1 | `ProcessingState` type in `PdfDocumentEntity` | 🔴 | ✅ RESOLVED | **STRING** (`VARCHAR(32)`), default `"Pending"`. Enum `PdfProcessingState` exists but entity stores as string. Use `nameof(PdfProcessingState.Pending)` pattern for comparisons. See [ProcessingState Reference](#processingstate-reference). |
| 2 | Should RAG processing be deferred to background job? | 🟡 | ✅ RESOLVED | **Existing jobs available**: `PdfProcessingQuartzJob` (queue, every 10s) and `RetryFailedPdfsJob` (retry, every 5min). **Decision**: PdfSeeder creates docs in `Pending` state → existing `PdfProcessingQuartzJob` picks them up automatically. No synchronous RAG processing at startup needed. See [Deferred RAG Strategy](#deferred-rag-strategy). |
| 3 | Multi-replica seeding safety | 🟡 | ✅ RESOLVED | **No distributed lock exists** in current `AutoConfigurationService`. Seeders rely on idempotency only. **Decision**: Add PostgreSQL advisory lock in `SeedOrchestrator`. See [Concurrency Guard](#concurrency-guard). |
| 4 | seed-pull.ps1 SSH setup | 🟢 | TODO | Document key setup or use S3 pre-signed URL. |
| 5 | Test coverage gate for Phase 6 | 🟢 | TODO | All Phase 7 tests green + manual smoke test. |

### ProcessingState Reference

`PdfDocumentEntity.ProcessingState` is a **string** property (`VARCHAR(32)`) with enum `PdfProcessingState` for type-safe comparisons:

```csharp
// Entity property (Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs:26)
public string ProcessingState { get; set; } = "Pending";

// Enum (BoundedContexts/DocumentProcessing/Domain/Enums/PdfProcessingState.cs)
public enum PdfProcessingState
{
    Pending = 0, Uploading = 1, Extracting = 2, Chunking = 3,
    Embedding = 4, Indexing = 5, Ready = 6, Failed = 99
}

// ✅ CORRECT pattern (used by PdfProcessingPipelineService):
pdfDoc.ProcessingState = nameof(PdfProcessingState.Extracting);  // "Extracting"

// ✅ CORRECT query pattern (used by RetryFailedPdfsJob):
.Where(p => p.ProcessingState == PdfProcessingState.Failed.ToString())  // "Failed"

// ❌ WRONG: numeric comparison (never works — it's a string column)
.Where(p => p.ProcessingState == "0")  // BROKEN
```

### Deferred RAG Strategy

Instead of processing PDFs synchronously at startup (blocking dev startup for minutes), leverage the existing background job infrastructure:

```
PdfSeeder creates PDFs in "Pending" state
  → PdfProcessingQuartzJob (runs every 10s) picks them up automatically
  → Processes through IPdfProcessingPipelineService (extract → chunk → embed → Qdrant)
  → On failure → RetryFailedPdfsJob (runs every 5min) retries with exponential backoff (1s, 2s, 4s)
```

**Benefits**:
- Dev startup completes in seconds (no waiting for embedding service)
- Existing retry logic handles transient failures automatically
- SSE events published for real-time progress tracking
- `[DisallowConcurrentExecution]` on both jobs prevents double-processing

**Impact on RagPipelineSeeder**: `RagPipelineSeeder` is **no longer needed as a separate component**. The `PdfSeeder` creates docs in `Pending` state, and the existing Quartz jobs handle the rest. The `RagPipelineSeeder` class can be removed from the spec — its responsibility is absorbed by `PdfProcessingQuartzJob`.

**Updated Catalog layer flow**:
```
CatalogSeeder reads manifest
  → GameSeeder (SharedGame + GameEntity)
  → PdfSeeder (copy file + DB entry in "Pending" state)   ← triggers background RAG
  → AgentSeeder (agents + sessions)
  → StrategyPatternSeeder (if enabled)
```

### Concurrency Guard

Add PostgreSQL advisory lock to prevent concurrent seeding in multi-replica deployments:

```csharp
internal sealed class SeedOrchestrator
{
    private const long SeedingAdvisoryLockId = 0x4D65_6570_6C65_4149; // "MeepleAI" as long

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (_profile == SeedProfile.None) return;

        // Acquire advisory lock — only one replica seeds at a time
        await using var lockScope = _scopeFactory.CreateScope();
        var lockDb = lockScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var acquired = await lockDb.Database
            .ExecuteSqlRawAsync("SELECT pg_try_advisory_lock({0})", SeedingAdvisoryLockId);

        if (acquired == 0)
        {
            _logger.LogInformation("Another replica is seeding. Skipping.");
            return;
        }

        try
        {
            // ... layer execution (Core → Catalog → LivedIn) ...
        }
        finally
        {
            await lockDb.Database
                .ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", SeedingAdvisoryLockId);
        }
    }
}
```

## Spec-Panel Review Summary

**Panel**: Wiegers (requirements), Fowler (architecture), Nygard (reliability), Adzic (testability), Crispin (quality)
**Score**: 7.4/10 → estimated 9.0/10 after all revisions

**Round 1 revisions (spec-panel)**:
- Replaced `IPdfProcessingPipeline` (non-existent) with existing `IPdfProcessingPipelineService`
- Added idempotency contracts for all catalog sub-seeders
- Added agent merge strategy specification (what `seedAgent: true/false` creates)
- Added RAG pipeline configuration (timeouts, retry params)
- Added YAML manifest validation rules
- Added error isolation via per-layer `IServiceScopeFactory` scopes
- Added dump/restore safety guards (checksums, env direction lock, dry-run)
- Added LivedIn data quality requirements
- Fixed Phase 6/7 dependency documentation

**Round 2 revisions (codebase verification)**:
- Resolved `ProcessingState` type: STRING (`VARCHAR(32)`), uses `nameof()` pattern for comparisons
- Eliminated `RagPipelineSeeder` entirely — existing `PdfProcessingQuartzJob` handles RAG processing automatically when PdfSeeder creates docs in `Pending` state
- Added PostgreSQL advisory lock for multi-replica seeding safety
- Dev startup no longer blocks on embedding service — RAG processing is fully async
- Phase 3 scope further reduced: only change `PdfSeeder` to use `Pending` state instead of `Ready`
