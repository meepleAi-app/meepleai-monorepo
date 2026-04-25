# Mechanic Extractor — AI Comprehension Validation Sprint 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Sprint 2 of ADR-051 M2.0 — second seeded golden game (Puerto Rico), threshold/BGG/recalc admin UIs, async mass-recalc via BackgroundService, calibration spike, and feature-flag rollout to dev+staging.

**Architecture:** Extends existing `SharedGameCatalog` BC. Replaces synchronous `RecalculateAllMechanicMetricsCommand` loop with a DB-persisted job pattern (mirroring `BggImportQueueBackgroundService`) — *not* Hangfire (not in repo, would require ADR for new dep). Frontend wires three new admin forms onto existing `/admin/(dashboard)/knowledge-base/mechanic-extractor/*` pages. Calibration spike is a research artifact (markdown + CSV fixture), no production threshold change.

**Tech Stack:** .NET 9 + EF Core + MediatR, Microsoft.Extensions.Hosting BackgroundService, FluentValidation, xUnit + Testcontainers; Next.js 16 + React Query + Zod + Vitest + Playwright.

**Resolved deferred decisions (spec §14):**
- §14.1 Recalibration cadence → **manual only** in Sprint 2 (button + drawer). Automated cron deferred to Sprint 3 if needed.
- §14.2 Embedding fallback → **fail-on-error confirmed** (already enforced in Sprint 1 matching engine). Circuit breaker added to mass-recalc service: ≥5 consecutive embedding failures abort the job (state=`Failed`, reason=`EmbeddingCircuitBreakerOpen`).
- §14.3 Formula config → **stays hardcoded** in Sprint 2 (`0.4*coverage + 0.2*pageAccuracy + 0.4*bgg`). UI exposes only thresholds (4 numeric inputs). Future ADR-052 if telemetry shows weights need tuning.
- §14.4 Project A wiring → **out of scope**.

---

## File Structure

### Backend — new files
| Path | Responsibility |
|---|---|
| `apps/api/src/Api/Infrastructure/Migrations/M2_1_MechanicRecalcJobs.cs` | EF migration: `mechanic_recalc_jobs` table |
| `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicRecalcJobEntity.cs` | EF persistence POCO |
| `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicRecalcJobConfiguration.cs` | EF mapping |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicRecalcJob.cs` | Domain aggregate (enum `RecalcJobStatus`, factory `Enqueue`, `MarkRunning/RecordSuccess/RecordFailure/RecordSkip/RequestCancellation/Complete/Fail/Heartbeat`) |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/IMechanicRecalcJobRepository.cs` | Repo interface |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicRecalcJobRepository.cs` | EF repo impl |
| `apps/api/src/Api/Infrastructure/BackgroundServices/MechanicRecalcBackgroundService.cs` | `BackgroundService` worker (5s poll, claim oldest Pending) |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/Validation/EnqueueRecalculateAllMechanicMetricsCommand.cs` + Handler + Validator | Replaces sync send |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/Validation/CancelRecalcJobCommand.cs` + Handler + Validator | Cancellation |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/Validation/GetRecalcJobStatusQuery.cs` + Handler + Validator | Status polling |
| `apps/api/src/Api/Telemetry/MeepleAiMetrics.MechanicRecalc.cs` | Prometheus counters/histograms |
| `data/rulebook/golden/puerto-rico/golden-claims.json` | Curated claims fixture |
| `data/rulebook/golden/puerto-rico/bgg-tags.json` | BGG tag fixture |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Seeding/PuertoRicoGoldenSeeder.cs` | Idempotent seeder loaded by `MeepleDbContext` initializer |
| `tests/fixtures/calibration-2026-04-XX.csv` | 20 manual {claim, ai_extraction, gold_match} rows for spike |
| `docs/research/mechanic-validation-calibration-spike-2026-04-XX.md` | Spike report |

### Backend — modified files
| Path | Change |
|---|---|
| `apps/api/src/Api/Routing/AdminMechanicExtractorValidationEndpoints.cs` | `POST /metrics/recalculate-all` → 202 Accepted + jobId; new `GET /metrics/recalc-jobs/{id}`, `POST /metrics/recalc-jobs/{id}/cancel` |
| `apps/api/src/Api/Infrastructure/MeepleDbContext.cs` | `DbSet<MechanicRecalcJobEntity> MechanicRecalcJobs` |
| `apps/api/src/Api/Program.cs` | `builder.Services.AddHostedService<MechanicRecalcBackgroundService>()` + repo registration |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/Validation/RecalculateAllMechanicMetricsHandler.cs` | Add `circuit breaker` + per-id `try/catch` already exists; add `MeepleAiMetrics.MechanicRecalc.JobsProcessed.Inc()` instrumentation |

### Frontend — new files
| Path | Responsibility |
|---|---|
| `apps/web/src/components/admin/mechanic-extractor/validation/ThresholdsConfigForm.tsx` | 4 numeric inputs + audit display + dirty/save/reset |
| `apps/web/src/components/admin/mechanic-extractor/validation/BggImporterPasteDialog.tsx` | TSV paste + parse + preview + duplicates highlight + commit |
| `apps/web/src/components/admin/mechanic-extractor/validation/RecalcAllButton.tsx` | Trigger button (admin-only) |
| `apps/web/src/components/admin/mechanic-extractor/validation/RecalcProgressDrawer.tsx` | Slide-over with poll + cancel + last completed |
| `apps/web/src/components/admin/mechanic-extractor/validation/FeatureFlagGate.tsx` | Renders children only when `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true` |
| `apps/web/src/lib/api/mechanic-validation.ts` (extended) | `enqueueRecalcAll`, `getRecalcJobStatus`, `cancelRecalcJob`, `updateThresholds`, `bulkInsertBggTags` clients |
| `apps/web/e2e/admin-mechanic-extractor-validation/thresholds.spec.ts` | E2E |
| `apps/web/e2e/admin-mechanic-extractor-validation/bgg-import.spec.ts` | E2E |
| `apps/web/e2e/admin-mechanic-extractor-validation/recalc-all.spec.ts` | E2E (mock 202 + completion poll) |

### Frontend — modified files
| Path | Change |
|---|---|
| `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/dashboard/page.tsx` | Mount `<RecalcAllButton/>` + `<RecalcProgressDrawer/>` (admin gate) |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx` | Mount `<ThresholdsConfigForm/>` (admin section) |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/golden/[gameId]/page.tsx` | Mount `<BggImporterPasteDialog/>` trigger button |
| `apps/web/src/lib/feature-flags/mechanic-validation.ts` (new — single helper) | `isMechanicValidationEnabled()` reads `process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'` |

### Infra — modified files
| Path | Change |
|---|---|
| `infra/.env.development` | `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true` |
| `infra/secrets/api-staging.secret.example` (and staging) | Add `MECHANIC_VALIDATION_ENABLED=true` |
| `infra/docker-compose.staging.yml` | Pass `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true` to web build args |

---

## Phases

- **Phase 1** — Puerto Rico golden seed (Tasks 1–4)
- **Phase 2** — Async recalc backend (Tasks 5–13)
- **Phase 3** — Threshold config UI (Tasks 14–16)
- **Phase 4** — BGG importer UI (Tasks 17–19)
- **Phase 5** — Recalc-all UI (Tasks 20–22)
- **Phase 6** — Calibration spike (Tasks 23–24)
- **Phase 7** — Feature flag rollout (Tasks 25–26)
- **Phase 8** — E2E + cleanup (Tasks 27–29)

---

## Phase 1 — Puerto Rico Golden Seed

### Task 1: Curate Puerto Rico claim fixture (≥50 claims, ≥1/section)

**Files:**
- Create: `data/rulebook/golden/puerto-rico/golden-claims.json`
- Create: `data/rulebook/golden/puerto-rico/bgg-tags.json`

**Acceptance:**
- ≥50 claims spread across all 6 `MechanicSection` enum values (Summary=0, Mechanics=1, Victory=2, Resources=3, Phases=4, Faq=5)
- Each claim: `statement` (1–500 chars), `expectedPage` (1-based, ≤rulebook page count), `sourceQuote` (1–1000 chars verbatim from EN PDF), `keywords` (3–8 lower-snake), `section` int.
- ≥80% of rulebook pages cited in `expectedPage` distribution (run audit script in Task 4).
- ≥10 BGG tags in `bgg-tags.json` from BGG game id 3076 (Puerto Rico, Andreas Seyfarth, 2002 Alea/Ravensburger 1st edition).

- [ ] **Step 1: Create directory and JSON skeletons**

```bash
mkdir -p data/rulebook/golden/puerto-rico
```

```json
// data/rulebook/golden/puerto-rico/golden-claims.json
{
  "sharedGameSlug": "puerto-rico",
  "edition": "1st edition (Alea 2002, Rio Grande Games EN reprint)",
  "rulebookPdfSha256": "TBD-fill-after-pdf-ingest",
  "claims": [
    {
      "section": 1,
      "statement": "Each player owns one San Juan tile providing one indigo plant slot at game start.",
      "expectedPage": 3,
      "sourceQuote": "Each player receives a San Juan tile and one Indigo Plant tile…",
      "keywords": ["san_juan", "indigo", "starting_position"]
    }
  ]
}
```

```json
// data/rulebook/golden/puerto-rico/bgg-tags.json
{
  "sharedGameSlug": "puerto-rico",
  "bggId": 3076,
  "tags": [
    { "category": "Mechanism", "name": "Role Selection" },
    { "category": "Mechanism", "name": "Variable Phase Order" }
  ]
}
```

- [ ] **Step 2: Hand-curate ≥50 claims (manual research session)**

Source: Rio Grande Games English rulebook v1.1 (2002). Reference also <https://boardgamegeek.com/boardgame/3076/puerto-rico> for tag taxonomy. Use the existing seeded Catan structure at `data/rulebook/golden/catan/golden-claims.json` as a shape reference.

- [ ] **Step 3: Commit fixtures (no code yet)**

```bash
git add data/rulebook/golden/puerto-rico/
git commit -m "feat(mechanic-validation): seed Puerto Rico golden fixture (50+ claims, 10+ BGG tags)"
```

---

### Task 2: Write coverage audit unit test for fixture

**Files:**
- Create: `apps/api/tests/Api.Tests/SharedGameCatalog/Seeding/PuertoRicoGoldenFixtureTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Seeding;

public class PuertoRicoGoldenFixtureTests
{
    private const string FixturePath = "../../../../../../data/rulebook/golden/puerto-rico/golden-claims.json";

    [Fact]
    public void Fixture_HasMinClaimsAndAllSections()
    {
        var json = File.ReadAllText(FixturePath);
        using var doc = JsonDocument.Parse(json);
        var claims = doc.RootElement.GetProperty("claims");

        claims.GetArrayLength().Should().BeGreaterOrEqualTo(50, "spec §9 requires ≥50 claims for Puerto Rico");

        var sections = claims.EnumerateArray()
            .Select(c => c.GetProperty("section").GetInt32())
            .Distinct()
            .ToHashSet();
        sections.Should().BeEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 },
            "every MechanicSection enum value must appear at least once");
    }

    [Fact]
    public void Fixture_PageCoverageAtLeast80Percent()
    {
        var json = File.ReadAllText(FixturePath);
        using var doc = JsonDocument.Parse(json);
        var claims = doc.RootElement.GetProperty("claims");

        var pages = claims.EnumerateArray()
            .Select(c => c.GetProperty("expectedPage").GetInt32())
            .ToHashSet();

        // Puerto Rico EN rulebook is 12 pages
        var expectedCoverage = (double)pages.Count / 12;
        expectedCoverage.Should().BeGreaterOrEqualTo(0.80, "spec §9 requires ≥80% page coverage");
    }
}
```

- [ ] **Step 2: Run test — should fail if fixture incomplete**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~PuertoRicoGoldenFixtureTests"
```

- [ ] **Step 3: Adjust fixture until both tests pass**

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/SharedGameCatalog/Seeding/PuertoRicoGoldenFixtureTests.cs
git commit -m "test(mechanic-validation): assert Puerto Rico fixture coverage invariants"
```

---

### Task 3: Implement idempotent seeder

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Seeding/PuertoRicoGoldenSeeder.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleDbContext.cs` (or its initializer — find it first via `grep -rn "EnsureCreated\|MigrateAsync\|SeedAsync" apps/api/src/Api/Infrastructure/`)

- [ ] **Step 1: Find existing seeder pattern**

```bash
grep -rln "GoldenSeeder\|GoldenClaimSeeder" apps/api/src/Api/
```

If a Catan seeder exists, copy/adapt. Otherwise create from scratch.

- [ ] **Step 2: Write seeder skeleton (idempotent — checks `MechanicGoldenClaim` existence by `SharedGameId`)**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Seeding;

internal sealed class PuertoRicoGoldenSeeder
{
    private readonly MeepleDbContext _db;
    private readonly ILogger<PuertoRicoGoldenSeeder> _logger;

    public async Task SeedAsync(CancellationToken ct)
    {
        var sharedGame = await _db.SharedGames
            .FirstOrDefaultAsync(g => g.Slug == "puerto-rico", ct);
        if (sharedGame is null)
        {
            _logger.LogWarning("Puerto Rico SharedGame missing — skip golden seed");
            return;
        }

        var alreadySeeded = await _db.MechanicGoldenClaims
            .AnyAsync(c => c.SharedGameId == sharedGame.Id, ct);
        if (alreadySeeded)
        {
            _logger.LogInformation("Puerto Rico golden already seeded — skip");
            return;
        }

        // Load JSON, deserialize, build domain aggregates via factory, persist.
    }
}
```

- [ ] **Step 3: Wire into startup seeding pipeline (mirror Catan seeder registration)**

- [ ] **Step 4: Run seeder integration test (Testcontainers)**

```csharp
[Fact]
public async Task SeedAsync_PopulatesAllClaims_AndIsIdempotent()
{
    await _seeder.SeedAsync(default);
    var firstCount = await _db.MechanicGoldenClaims.CountAsync();
    firstCount.Should().BeGreaterOrEqualTo(50);

    await _seeder.SeedAsync(default); // second call no-op
    (await _db.MechanicGoldenClaims.CountAsync()).Should().Be(firstCount);
}
```

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(mechanic-validation): seed Puerto Rico golden claims idempotently"
```

---

### Task 4: BGG tags seed

Same pattern as Task 3, target table `mechanic_golden_bgg_tags`. Idempotent on `(SharedGameId, Name)` unique constraint.

- [ ] **Step 1: Extend seeder** to also load `bgg-tags.json` and persist via `MechanicGoldenBggTag` aggregate.
- [ ] **Step 2: Test ≥10 tags inserted**, second run no-op.
- [ ] **Step 3: Commit.**

---

## Phase 2 — Async Recalc Backend

### Task 5: EF migration `mechanic_recalc_jobs`

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/M2_1_MechanicRecalcJobs.cs`

- [ ] **Step 1: Add EF migration via CLI**

```bash
cd apps/api/src/Api
dotnet ef migrations add M2_1_MechanicRecalcJobs
```

- [ ] **Step 2: Edit `Up` to include constraints**

```csharp
migrationBuilder.Sql(@"
    CREATE TABLE mechanic_recalc_jobs (
        id uuid PRIMARY KEY,
        status integer NOT NULL DEFAULT 0,                    -- 0=Pending,1=Running,2=Completed,3=Failed,4=Cancelled
        triggered_by_user_id uuid NOT NULL REFERENCES users(""Id"") ON DELETE RESTRICT,
        total integer NOT NULL DEFAULT 0,
        processed integer NOT NULL DEFAULT 0,
        failed integer NOT NULL DEFAULT 0,
        skipped integer NOT NULL DEFAULT 0,
        consecutive_failures integer NOT NULL DEFAULT 0,
        last_error text NULL,
        last_processed_analysis_id uuid NULL,
        cancellation_requested boolean NOT NULL DEFAULT false,
        created_at timestamptz NOT NULL DEFAULT now(),
        started_at timestamptz NULL,
        completed_at timestamptz NULL,
        heartbeat_at timestamptz NULL
    );
    CREATE INDEX ix_mechanic_recalc_jobs_status_created
        ON mechanic_recalc_jobs(status, created_at)
        WHERE status IN (0, 1);
");
```

- [ ] **Step 3: `Down` drops table**
- [ ] **Step 4: Run `dotnet ef database update` against dev compose, verify**
- [ ] **Step 5: Commit**

---

### Task 6: Domain aggregate + entity + EF mapping

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicRecalcJob.cs`
- Create: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicRecalcJobEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicRecalcJobConfiguration.cs`

- [ ] **Step 1: Write aggregate skeleton**

```csharp
public sealed class MechanicRecalcJob
{
    public Guid Id { get; }
    public RecalcJobStatus Status { get; private set; }
    public Guid TriggeredByUserId { get; }
    public int Total { get; private set; }
    public int Processed { get; private set; }
    public int Failed { get; private set; }
    public int Skipped { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public string? LastError { get; private set; }
    public Guid? LastProcessedAnalysisId { get; private set; }
    public bool CancellationRequested { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? HeartbeatAt { get; private set; }

    public static MechanicRecalcJob Enqueue(Guid triggeredBy) => new(...);
    public void MarkRunning(int total) { ... }
    public void RecordSuccess(Guid analysisId) { ... }
    public void RecordFailure(Guid analysisId, string error) { ... } // increments ConsecutiveFailures
    public void RecordSkip() { ... }
    public void RequestCancellation() { ... }
    public void Complete() { ... }
    public void Fail(string reason) { ... }
    public void Heartbeat() { ... }
}

public enum RecalcJobStatus { Pending=0, Running=1, Completed=2, Failed=3, Cancelled=4 }
```

- [ ] **Step 2: Write 12 domain unit tests covering invariants** (Enqueue → Running, RecordSuccess resets ConsecutiveFailures, RecordFailure ≥5 leaves CB-eligible, idempotent Cancel from Running)
- [ ] **Step 3: Plain POCO entity + EF mapping**, mirror existing `MechanicAnalysisMetricsConfiguration.cs`
- [ ] **Step 4: Add `DbSet<MechanicRecalcJobEntity> MechanicRecalcJobs` to `MeepleDbContext`**
- [ ] **Step 5: Commit**

---

### Task 7: Repository + DI registration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/IMechanicRecalcJobRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicRecalcJobRepository.cs`
- Modify: `apps/api/src/Api/Program.cs`

```csharp
public interface IMechanicRecalcJobRepository
{
    Task AddAsync(MechanicRecalcJob job, CancellationToken ct);
    Task<MechanicRecalcJob?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<MechanicRecalcJob?> ClaimNextPendingAsync(CancellationToken ct); // SELECT ... FOR UPDATE SKIP LOCKED, sets Running+StartedAt
    Task UpdateAsync(MechanicRecalcJob job, CancellationToken ct);
    Task<IReadOnlyList<MechanicRecalcJob>> ListRecentAsync(int limit, CancellationToken ct);
}
```

- [ ] **Step 1: Implement using EF + raw SQL for `FOR UPDATE SKIP LOCKED` claim semantics**
- [ ] **Step 2: Integration test against Postgres Testcontainer** verifying two concurrent claims yield exactly one job each
- [ ] **Step 3: Register `IMechanicRecalcJobRepository` + impl in `Program.cs`**
- [ ] **Step 4: Commit**

---

### Task 8: BackgroundService worker

**Files:**
- Create: `apps/api/src/Api/Infrastructure/BackgroundServices/MechanicRecalcBackgroundService.cs`
- Modify: `apps/api/src/Api/Program.cs`

Read `apps/api/src/Api/Infrastructure/BackgroundServices/BggImportQueueBackgroundService.cs` as the canonical template.

- [x] **Step 1: Skeleton**

```csharp
internal sealed class MechanicRecalcBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MechanicRecalcBackgroundService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int CircuitBreakerThreshold = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // startup delay
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MechanicRecalcBackgroundService outer loop");
            }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessNextJobAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMechanicRecalcJobRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var analysisRepo = scope.ServiceProvider.GetRequiredService<IMechanicAnalysisRepository>();
        var metrics = scope.ServiceProvider.GetRequiredService<MeepleAiMetrics>();

        var job = await repo.ClaimNextPendingAsync(ct);
        if (job is null) return;

        var ids = await analysisRepo.GetAllPublishedIdsAsync(ct);
        job.MarkRunning(ids.Count);
        await repo.UpdateAsync(job, ct);

        foreach (var id in ids)
        {
            if (job.CancellationRequested) { job.Complete(); break; }
            if (job.ConsecutiveFailures >= CircuitBreakerThreshold)
            {
                job.Fail("EmbeddingCircuitBreakerOpen");
                metrics.MechanicRecalc.CircuitBreakerOpens.Inc();
                break;
            }
            try
            {
                await mediator.Send(new CalculateMechanicAnalysisMetricsCommand(id), ct);
                job.RecordSuccess(id);
                metrics.MechanicRecalc.AnalysesProcessed.Inc();
            }
            catch (NotFoundException)        { job.RecordSkip(); }
            catch (ConflictException)        { job.RecordSkip(); }
            catch (Exception ex)             { job.RecordFailure(id, ex.Message); metrics.MechanicRecalc.AnalysesFailed.Inc(); }

            job.Heartbeat();
            await repo.UpdateAsync(job, ct); // persist progress every iteration
        }

        if (job.Status == RecalcJobStatus.Running) job.Complete();
        await repo.UpdateAsync(job, ct);
        metrics.MechanicRecalc.JobsCompleted.WithLabels(job.Status.ToString()).Inc();
    }
}
```

- [x] **Step 2: Register in `Program.cs`:** `builder.Services.AddHostedService<MechanicRecalcBackgroundService>();`
- [x] **Step 3: Integration test** — enqueue job, run service one tick, assert progresses; cancel mid-flight, assert terminal `Completed` with `CancellationRequested == true` (cancellation is a flag, not a status — see Task 6 aggregate lifecycle). _(commits `800e61483`, `698725ef5`)_
- [x] **Step 4: Stale recovery** — on startup, mark jobs in `Running` with `HeartbeatAt < now-5min` as `Failed("StaleHeartbeat")`. Add `RecoverStaleJobsAsync` called in `ExecuteAsync` before main loop. _(implementation in `800e61483`; integration tests in `9841651e6`)_
- [x] **Step 5: Commit**

---

### Task 9: Enqueue / GetStatus / Cancel commands+queries

**Files:** see "File Structure" above.

- [x] **Step 1: `EnqueueRecalculateAllMechanicMetricsCommand(Guid TriggeredByUserId) : ICommand<Guid>`** — handler creates job, calls repo, returns id.
- [x] **Step 2: `GetRecalcJobStatusQuery(Guid JobId) : IQuery<RecalcJobStatusDto>`** — handler maps domain → DTO with `etaSeconds = (Total-Processed) * avgPerItem` if Running.
- [x] **Step 3: `CancelRecalcJobCommand(Guid JobId) : ICommand`** — handler loads, calls `RequestCancellation()`, persists.
- [x] **Step 4: Validators (FluentValidation)**
- [x] **Step 5: Unit tests** (28 tests across 3 handler classes — Enqueue 6, Cancel 9, GetStatus 11; integration tests for the worker cycle live in Task 8 since the cycle is driven by `MechanicRecalcBackgroundService`).
- [x] **Step 6: Commit**

---

### Task 10: Endpoint upgrade — 202 Accepted

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminMechanicExtractorValidationEndpoints.cs`

Replace the existing synchronous `MapPost("/metrics/recalculate-all")` (lines 252–271) with:

```csharp
group.MapPost("/metrics/recalculate-all", async (
    HttpContext ctx,
    IMediator mediator,
    CancellationToken ct) =>
{
    var (_, session) = ctx.RequireAdminSession();
    var jobId = await mediator.Send(
        new EnqueueRecalculateAllMechanicMetricsCommand(session!.User!.Id), ct);
    return Results.Accepted($"/api/v1/admin/mechanic-extractor/metrics/recalc-jobs/{jobId}",
        new { JobId = jobId });
})
.WithName("AdminEnqueueRecalculateAllMechanicMetrics")
.Produces(StatusCodes.Status202Accepted);

group.MapGet("/metrics/recalc-jobs/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
    Results.Ok(await mediator.Send(new GetRecalcJobStatusQuery(id), ct)))
    .WithName("AdminGetMechanicRecalcJobStatus")
    .Produces<RecalcJobStatusDto>();

group.MapPost("/metrics/recalc-jobs/{id:guid}/cancel", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    await mediator.Send(new CancelRecalcJobCommand(id), ct);
    return Results.NoContent();
})
    .WithName("AdminCancelMechanicRecalcJob")
    .Produces(StatusCodes.Status204NoContent);
```

- [x] **Step 1: Apply edit** — replaced sync 200 dispatcher with three endpoints (202/200/204). Used `(SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!` (locally consistent pattern) instead of `RequireAdminSession()` proposed in plan.
- [x] **Step 2: Endpoint tests** — 5 tests added to `AdminMechanicExtractorValidationEndpointsTests`: enqueue 202+Location+JobId+aggregate persisted Pending, GET 200 with DTO, GET unknown 404, cancel 204 sets `CancellationRequested`, cancel unknown 404. Plus 2 new `[InlineData]` rows on the auth Theory (15/15 pass).
- [x] **Step 3: Update OpenAPI/Scalar doc strings** — folded into Step 1 via `WithSummary`/`WithDescription` on each route.
- [ ] **Step 4: Commit**

---

### Task 11: Prometheus metrics

**Files:**
- Create: `apps/api/src/Api/Telemetry/MeepleAiMetrics.MechanicRecalc.cs`

```csharp
public partial class MeepleAiMetrics
{
    public sealed class MechanicRecalcMetrics
    {
        public Counter JobsEnqueued { get; }      = Metrics.CreateCounter("meepleai_mechanic_recalc_jobs_enqueued_total", "Recalc jobs enqueued");
        public Counter JobsCompleted { get; }     = Metrics.CreateCounter("meepleai_mechanic_recalc_jobs_completed_total", "Recalc jobs completed", "status");
        public Counter AnalysesProcessed { get; } = Metrics.CreateCounter("meepleai_mechanic_recalc_analyses_processed_total", "Analyses successfully recomputed");
        public Counter AnalysesFailed { get; }    = Metrics.CreateCounter("meepleai_mechanic_recalc_analyses_failed_total", "Analyses failed during recalc");
        public Counter CircuitBreakerOpens { get; }= Metrics.CreateCounter("meepleai_mechanic_recalc_circuit_breaker_opens_total", "Circuit breaker activations");
        public Histogram JobDuration { get; }     = Metrics.CreateHistogram("meepleai_mechanic_recalc_job_duration_seconds", "Job total duration");
    }
    public MechanicRecalcMetrics MechanicRecalc { get; } = new();
}
```

- [x] **Step 1: Create file**, verify scrape `/metrics` shows new series.
  - Extended existing `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.MechanicRecalc.cs` (Task 8 file) with `JobsEnqueued` Counter + `JobDuration` Histogram (status-tagged).
  - Wired `JobsEnqueued.Add(1)` after `SaveChangesAsync` in `EnqueueRecalculateAllMechanicMetricsHandler`.
  - Wired `JobDuration.Record(seconds, statusTag)` in `MechanicRecalcBackgroundService` terminal-transition block, computed from `job.StartedAt → DateTimeOffset.UtcNow`.
  - 12/12 affected tests passed (`EnqueueRecalculate*` + `MechanicRecalcBackgroundService*`); build clean.
- [ ] **Step 2: Commit**

---

### Task 12: Audit logger entries

Use existing audit infrastructure (`IAuditLogger` or equivalent — `grep -rn "AuditLogger\|IAuditService" apps/api/src/Api/`).

- [x] **Step 1: Emit audit entry on each `EnqueueRecalculateAllMechanicMetricsCommand`** (action=`mechanic_recalc.enqueued`, actor=user, payload={jobId}).
- [x] **Step 2: On `CancelRecalcJobCommand`** (action=`mechanic_recalc.cancelled`).
- [x] **Step 3: On `BackgroundService` job completion** (action=`mechanic_recalc.completed`, payload={jobId, processed, failed, skipped, status}).
- [x] **Step 4: Audit logger tests**
- [x] **Step 5: Commit**

---

### Task 13: HybridCache invalidation on job complete

Background service publishes `MechanicMetricsRecalculatedEvent` via existing event handler infrastructure → handler calls `IHybridCacheService.RemoveByTagAsync("mechanic-dashboard")` (mirror Sprint 1 pattern, see issue #2620 in CLAUDE.md).

- [ ] **Step 1: Add event + handler**
- [ ] **Step 2: Test cache key invalidated after recalc**
- [ ] **Step 3: Commit**

---

## Phase 3 — Threshold Config UI

### Task 14: Backend already exists — verify & test

Sprint 1 shipped `UpdateCertificationThresholdsCommand` and `PUT /thresholds`. Verify:

- [ ] **Step 1: Read** `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/Validation/UpdateCertificationThresholdsHandler.cs` — confirm validation + audit trail present.
- [ ] **Step 2: Add missing test if any**

---

### Task 15: ThresholdsConfigForm component

**Files:**
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/ThresholdsConfigForm.tsx`
- Modify: `apps/web/src/lib/api/mechanic-validation.ts` (add `updateThresholds` client)
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/__tests__/ThresholdsConfigForm.test.tsx`

- [ ] **Step 1: Write failing test (Vitest + RTL)**

```tsx
it('disables save when no field changed', () => {
  render(<ThresholdsConfigForm initial={{minCoverage:70,maxPageTolerance:10,minBgg:80,minOverall:60}} />);
  expect(screen.getByRole('button', {name:/save/i})).toBeDisabled();
});

it('blocks save when minCoverage > 100', async () => {
  render(<ThresholdsConfigForm initial={...} />);
  await userEvent.clear(screen.getByLabelText(/minimum coverage/i));
  await userEvent.type(screen.getByLabelText(/minimum coverage/i), '150');
  expect(screen.getByText(/must be between 0 and 100/i)).toBeInTheDocument();
});

it('calls updateThresholds on save and shows success toast', async () => {...});
```

- [ ] **Step 2: Implement component** with React Hook Form + Zod schema (4 fields, 0–100 + integer pageTolerance)
- [ ] **Step 3: Show audit display:** `Last updated by {updatedByEmail} on {updatedAt}` (data from existing `GET /thresholds`)
- [ ] **Step 4: Verify all tests pass**
- [ ] **Step 5: Commit**

---

### Task 16: Mount on dashboard page (admin gate)

- [ ] **Step 1: Modify** `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx`:

```tsx
{isMechanicValidationEnabled() && session.user.role === 'Admin' && (
  <Card>
    <CardHeader><CardTitle>Certification Thresholds</CardTitle></CardHeader>
    <CardContent><ThresholdsConfigForm initial={thresholds} /></CardContent>
  </Card>
)}
```

- [ ] **Step 2: Commit**

---

## Phase 4 — BGG Importer UI

### Task 17: Backend bulk insert command

The existing `POST /golden/{sharedGameId:guid}/bgg-tags` endpoint (line 157) handles single + bulk. Verify:

- [ ] **Step 1: Read existing handler** — confirm accepts `IReadOnlyList<{Category, Name}>` and returns inserted/skipped counts.
- [ ] **Step 2: Add missing duplicate-detection** if not present (existing unique constraint `(SharedGameId, Name)` raises; handler should swallow and report skipped).
- [ ] **Step 3: Test idempotency**
- [ ] **Step 4: Commit**

---

### Task 18: BggImporterPasteDialog component

**Files:**
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/BggImporterPasteDialog.tsx`
- Create: `apps/web/src/lib/parsers/bgg-tsv.ts` — `parseBggTsv(text: string): { rows: BggTagRow[]; errors: string[] }`
- Create: `apps/web/src/lib/parsers/__tests__/bgg-tsv.test.ts`

Spec by example (Adzic):

```
Given the user pastes:
  Mechanism\tRole Selection
  Mechanism\tVariable Phase Order
  Theme\tEconomic
When parser runs
Then rows = [{category:"Mechanism",name:"Role Selection"},{category:"Mechanism",name:"Variable Phase Order"},{category:"Theme",name:"Economic"}]
  And errors = []

Given the user pastes:
  Mechanism|Role Selection
When parser runs
Then errors = ["Line 1: expected TAB separator"]
```

- [ ] **Step 1: Write parser tests, then parser, then dialog tests, then dialog**
- [ ] **Step 2: Dialog flow:** open → paste textarea → live preview table → highlight duplicates (red text "already exists") → "Insert N tags" button → on success close + toast
- [ ] **Step 3: Test fixture inputs end-to-end**
- [ ] **Step 4: Commit**

---

### Task 19: Mount on golden detail page

- [ ] **Step 1: Modify** `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/golden/[gameId]/page.tsx` — add "Import BGG tags" trigger button next to existing claim list.
- [ ] **Step 2: Commit**

---

## Phase 5 — Recalc-All UI

### Task 20: API client extensions

**Files:**
- Modify: `apps/web/src/lib/api/mechanic-validation.ts`

```ts
export async function enqueueRecalcAll(): Promise<{ jobId: string }> {
  const res = await httpClient.post('/api/v1/admin/mechanic-extractor/metrics/recalculate-all');
  return res.data;
}
export async function getRecalcJobStatus(jobId: string): Promise<RecalcJobStatusDto> {...}
export async function cancelRecalcJob(jobId: string): Promise<void> {...}
```

- [ ] **Step 1: Add functions + types**
- [ ] **Step 2: Unit test mocked**
- [ ] **Step 3: Commit**

---

### Task 21: RecalcAllButton + RecalcProgressDrawer

**Files:**
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/RecalcAllButton.tsx`
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/RecalcProgressDrawer.tsx`
- Create: tests for both

Drawer behavior:
- Polls `getRecalcJobStatus` every 2s while `status ∈ {Pending, Running}`
- Shows progress bar = `processed / total`, `failed`, `skipped` counts, ETA
- Shows "Cancel" button (calls `cancelRecalcJob`, optimistic UI: button disabled until status updates to `Cancelled`)
- Shows last 5 completed jobs (from a future `GET /metrics/recalc-jobs` list endpoint — for Sprint 2 a single in-memory state of "current job" is sufficient)
- On `status === 'Completed'`: green toast "Recalculated N analyses"; on `Failed`: red toast with `lastError`

- [ ] **Step 1: Write component tests for each behavior**
- [ ] **Step 2: Implement**
- [ ] **Step 3: Verify tests pass**
- [ ] **Step 4: Commit**

---

### Task 22: Mount on dashboard page

- [ ] **Step 1: Modify** `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/dashboard/page.tsx`:

```tsx
{isMechanicValidationEnabled() && session.user.role === 'Admin' && (
  <RecalcAllButton onJobStarted={(id) => setActiveJobId(id)} />
)}
{activeJobId && <RecalcProgressDrawer jobId={activeJobId} onClose={() => setActiveJobId(null)} />}
```

- [ ] **Step 2: Commit**

---

## Phase 6 — Calibration Spike

### Task 23: Author calibration CSV fixture

**Files:**
- Create: `tests/fixtures/calibration-2026-04-XX.csv` (replace XX with actual date)

Format:
```csv
sharedGameSlug,claimId,claimStatement,aiExtractionId,aiSnippet,aiPage,humanLabel
catan,abc-123,"Each player begins with two settlements",ext-1,"Players start with 2 settlements and 2 roads",4,match
catan,abc-124,"Robber blocks resource production",ext-9,"During robbing, no resources collected",6,partial-match
puerto-rico,xyz-77,"San Juan tile provides indigo plant slot",ext-15,"",,no-match
```

- [ ] **Step 1: Run AI extractor over both Catan + Puerto Rico** (use admin UI evaluate flow).
- [ ] **Step 2: Manually label 20 rows** (10 each game) for `humanLabel ∈ {match, partial-match, no-match}` with notes column.
- [ ] **Step 3: Commit fixture**

---

### Task 24: Spike report

**Files:**
- Create: `docs/research/mechanic-validation-calibration-spike-2026-04-XX.md`

Sections:
1. Hypothesis: "Current threshold defaults (70/10/80/60) approximate human judgment."
2. Method: 20 manually labeled extractions, compare automated `OverallScore` vs human label.
3. Results: confusion matrix (auto-certified vs human-match), false-positive rate, false-negative rate.
4. Recommendations: keep / adjust thresholds. **No production threshold change in Sprint 2** — recommendation feeds Sprint 3 ADR-052 if needed.

- [ ] **Step 1: Author report**
- [ ] **Step 2: Commit**

---

## Phase 7 — Feature Flag Rollout

### Task 25: Feature-flag helper + gate component

**Files:**
- Create: `apps/web/src/lib/feature-flags/mechanic-validation.ts`

```ts
export const isMechanicValidationEnabled = (): boolean =>
  process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true';
```

- Create: `apps/web/src/components/admin/mechanic-extractor/validation/FeatureFlagGate.tsx`

```tsx
export function FeatureFlagGate({ children }: { children: ReactNode }) {
  if (!isMechanicValidationEnabled()) return null;
  return <>{children}</>;
}
```

- [ ] **Step 1: Add files**
- [ ] **Step 2: Unit tests** (env stub on / off)
- [ ] **Step 3: Wrap each Sprint 2 admin section** in `<FeatureFlagGate>`
- [ ] **Step 4: Commit**

---

### Task 26: Compose flag wiring

**Files:**
- Modify: `infra/.env.development`
- Modify: `infra/docker-compose.staging.yml`
- Modify: `infra/secrets/api-staging.secret.example`

- [ ] **Step 1: Add to `.env.development`:** `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true`
- [ ] **Step 2: Pass `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED` as build arg + runtime env to web service in staging compose**
- [ ] **Step 3: Document in `docs/operations/operations-manual.md`** under "Feature flags"
- [ ] **Step 4: Commit**

---

## Phase 8 — E2E + Cleanup

### Task 27: Playwright spec — thresholds form

**Files:**
- Create: `apps/web/e2e/admin-mechanic-extractor-validation/thresholds.spec.ts`

```ts
test('admin updates thresholds and persists', async ({ page }) => {
  await loginAsAdmin(page);
  await page.goto('/admin/knowledge-base/mechanic-extractor');
  await page.getByLabel(/minimum coverage/i).fill('75');
  await page.getByRole('button', { name: /save/i }).click();
  await expect(page.getByText(/thresholds updated/i)).toBeVisible();
  await page.reload();
  await expect(page.getByLabel(/minimum coverage/i)).toHaveValue('75');
});
```

- [ ] **Step 1: Author**, run locally with `pnpm test:e2e`, commit.

---

### Task 28: Playwright spec — BGG import

**Files:**
- Create: `apps/web/e2e/admin-mechanic-extractor-validation/bgg-import.spec.ts`

Scenario: open dialog, paste 3 valid + 1 duplicate row, preview shows duplicate highlighted, click Insert, success toast shows "2 inserted, 1 skipped".

- [ ] **Step 1: Author + commit**

---

### Task 29: Playwright spec — recalc-all (mocked)

**Files:**
- Create: `apps/web/e2e/admin-mechanic-extractor-validation/recalc-all.spec.ts`

Use `page.context().route()` to mock:
- `POST /metrics/recalculate-all` → 202 + `{jobId:"job-1"}`
- `GET /metrics/recalc-jobs/job-1` → progressively returns Pending → Running (50%) → Completed

Assert drawer renders progress bar transitions and final success toast.

- [ ] **Step 1: Author + commit**

---

## Self-Review Checklist (run after final task, before handoff)

- [ ] **Spec coverage:** Every §9 Sprint 2 item appears in a task: Puerto Rico seed (T1–4) ✓ thresholds UI (T15–16) ✓ override UI (already shipped — verify, no new task) ✓ mass recalc (T5–13, T20–22) ✓ BGG importer UI (T17–19) ✓ spike calibration (T23–24) ✓ feature flag (T25–26)
- [ ] **Placeholder scan:** grep for `TODO`, `TBD`, `implement later`, `add appropriate` in plan file
- [ ] **Type consistency:** `RecalcJobStatus` enum values used identically in domain, EF mapping, DTO, frontend type
- [ ] **Migration safety:** `M2_1_MechanicRecalcJobs.Down()` drops table cleanly
- [ ] **Background service safety:** stale-heartbeat recovery present (Task 8 Step 4), circuit breaker present (Task 8), cancellation respected (Task 8 + Task 9.3)
- [ ] **Decision documentation:** §14 resolutions section preserved in plan header

---

## Execution Handoff

After all tasks complete, run `superpowers:finishing-a-development-branch` to merge to parent branch (`main-dev`).
