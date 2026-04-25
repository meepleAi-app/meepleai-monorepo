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

- [x] **Step 1: Add event + handler**
  - Created `MechanicMetricsRecalculatedEvent` (sealed record `IDomainEvent`) carrying `JobId`, `TriggeredByUserId`, `Status`, counters, and `CompletedAt`.
  - Created `MechanicMetricsRecalculatedCacheInvalidationHandler` (`INotificationHandler<...>`) — invalidates **two** tags (per real `GetDashboardQueryHandler` / `GetTrendQueryHandler` registrations, not the single `mechanic-dashboard` placeholder originally written in this plan): `mechanic-validation-dashboard` and `mechanic-validation-trend`. Per-tag try/catch isolation so a transient outage on one bucket does not skip the other; failures logged at warning, dashboard 5 min TTL self-heals.
  - Wired the worker: `MechanicRecalcBackgroundService` resolves `IPublisher` from a fresh DI scope (mirroring its audit-write scope hop) and publishes the event after `WriteCompletionAuditAsync`. The publish call is wrapped in a guard so a misbehaving handler never escalates into a worker outage.
- [x] **Step 2: Test cache key invalidated after recalc**
  - Unit tests: `MechanicMetricsRecalculatedCacheInvalidationHandlerTests` (5 cases — both tags evicted, Failed status still evicts, first-tag throw isolation, both-tags throw still swallows, null-notification guard).
  - Integration test: `MechanicRecalcBackgroundServiceIntegrationTests.ProcessNextJobAsync_OnCompletion_InvalidatesDashboardAndTrendCacheTags` — overrides the default `Mock.Of<IHybridCacheService>()` with a captured singleton, runs a real Pending → Completed tick, and verifies `RemoveByTagAsync` was called once per tag.
- [x] **Step 3: Commit** — `be054885e` `feat(mechanic-validation): invalidate dashboard cache on recalc completion (Task 13)`

---

## Phase 3 — Threshold Config UI

### Task 14: Backend already exists — verify & test

Sprint 1 shipped `UpdateCertificationThresholdsCommand` and `PUT /thresholds`. Verify:

- [x] **Step 1: Read** `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/Validation/UpdateCertificationThresholdsHandler.cs` — confirm validation + audit trail present.
  - Handler resolves singleton via `ICertificationThresholdsConfigRepository`, builds new `CertificationThresholds` value object via factory (defense-in-depth bounds re-check), calls `config.Update(thresholds, userId)` — which mutates `Thresholds`, `UpdatedAt`, `UpdatedByUserId` on the aggregate — then `UpdateAsync` + `SaveChangesAsync`. Logs `MinCoverage/MaxPageTol/MinBgg/MinOverall + UserId` at Information.
  - `UpdateCertificationThresholdsValidator` enforces 0..100 on three percent fields, `>= 0` on `MaxPageTolerance`, `NotEmpty` on `UserId`. Existing tests: `UpdateCertificationThresholdsHandlerTests` (constructor null guards × 3 + null-request guard + happy-path persistence ordering + out-of-bounds ArgumentException → no persist).
- [x] **Step 2: Add missing test if any**
  - Validator surface had no dedicated test class. Added `UpdateCertificationThresholdsValidatorTests` (12 cases: valid baseline + range violations / boundaries for `MinCoveragePct`, `MaxPageTolerance`, `MinBggMatchPct`, `MinOverallScore`, plus `UserId` empty-guard). Run: `dotnet test --filter "FullyQualifiedName~UpdateCertificationThresholdsValidatorTests"` → 12/12 passed.

---

### Task 15: ThresholdsConfigForm component

**Files:**
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/ThresholdsConfigForm.tsx` ✅
- Create: `apps/web/src/hooks/admin/useUpdateThresholds.ts` ✅ (TanStack mutation hook; replaces the plan's "Modify `lib/api/mechanic-validation.ts`" — that file does not exist; the typed client `api.admin.updateThresholds` already lives in `adminMechanicExtractorValidationClient.ts` from Sprint 1 / Task 34)
- Modify: `apps/web/src/hooks/admin/mechanicValidationKeys.ts` ✅ (add `thresholds.all` query key for invalidation)
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/__tests__/ThresholdsConfigForm.test.tsx` ✅

- [x] **Step 1: Write failing test (Vitest + RTL)** — 6 tests covering: prefill from initial DTO, save disabled when pristine, blocks `minCoveragePct > 100` with "must be between 0 and 100", blocks `maxPageTolerance` negative with "must be at least 0", calls `updateThresholds` with dirty form state, accepts boundary values 0/100. Used the same `vi.hoisted` + `QueryClientProvider` pattern as `GoldenClaimForm.test.tsx`.
- [x] **Step 2: Implement component** with React Hook Form + Zod schema (4 fields: `minCoveragePct`/`minBggMatchPct`/`minOverallScore` 0–100, `maxPageTolerance` integer ≥ 0). Save+Reset both gated on `formState.isDirty`. On success the hook invalidates `thresholds.all` + `dashboard.all` + `trend.all` query keys and emits a success toast via Sonner; the form `reset(data)` rebases the dirty baseline.
- [ ] **Step 3 (deferred): Audit display** `Last updated by {updatedByEmail} on {updatedAt}` — **NOT IMPLEMENTED**. The plan's premise that this data is "from existing `GET /thresholds`" is wrong: `GetCertificationThresholdsQueryHandler` returns only the four-field `CertificationThresholds` value object, while `UpdatedByUserId`/`UpdatedAt` live on the `CertificationThresholdsConfig` aggregate and are never serialized. Surfacing them would require a new DTO + handler change, scope-creep for Task 15. Documented as a future enhancement in `ThresholdsConfigForm.tsx`'s file header.
- [x] **Step 4: Verify all tests pass** — `pnpm vitest run …/ThresholdsConfigForm.test.tsx` → 6/6 passed in 1.09s. `pnpm typecheck` clean (after dropping Zod v3-only `invalid_type_error` options — project is on Zod v4).
- [x] **Step 5: Commit** — `7cf37bea0` `feat(mechanic-validation): ThresholdsConfigForm admin component (Task 15)`

---

### Task 16: Mount on dashboard page (admin gate)

- [x] **Step 1: Modify** `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/dashboard/page.tsx`:

> **Plan deviation (from literal `mechanic-extractor/page.tsx`)**: the literal path is the
> Variant C human+AI workflow editor (PDF viewer + section tabs + AI assist), NOT a validation
> surface. The validation dashboard already lives at the `dashboard/` subroute and is gated by
> `process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'` via `notFound()`. Mounting
> the form there keeps both threshold-related surfaces (per-game scores + the bounds those
> scores are evaluated against) under the same flag, single page, single load.
>
> The admin gate is enforced at the parent `/admin/(dashboard)/` layout level via session
> middleware — no per-component `session.user.role === 'Admin'` check needed (consistent with
> how `useValidationDashboard` is mounted).

```tsx
// dashboard/page.tsx — wraps form in Card after existing summary + table
<Card data-testid="thresholds-card">
  <CardHeader><CardTitle>Certification Thresholds</CardTitle></CardHeader>
  <CardContent>
    {isThresholdsLoading && <Skeleton className="h-48 w-full" />}
    {thresholdsError && <ErrorBanner message={...} />}
    {thresholds && <ThresholdsConfigForm initial={thresholds} />}
  </CardContent>
</Card>
```

- [x] **Step 1.5 (added): Create** `apps/web/src/hooks/admin/useThresholds.ts` — query hook
  wrapping `api.admin.getThresholds()` with key `mechanicValidationKeys.thresholds.all`,
  60s staleTime mirroring `useValidationDashboard`. Symmetric with the invalidation list in
  `useUpdateThresholds` so PUT → GET refetches automatically.
- [x] **Step 2: Commit**

---

## Phase 4 — BGG Importer UI

### Task 17: Backend bulk insert command ✅

The existing `POST /golden/{sharedGameId:guid}/bgg-tags` endpoint (line 157) handles single + bulk. Verify:

- [x] **Step 1: Read existing handler** — `ImportBggTagsHandler` accepts `IReadOnlyList<BggTagDto>` but returned `Tags.Count` regardless of dedup outcome (contract gap discovered). Repository `MechanicGoldenBggTagRepository.UpsertBatchAsync` already silently dedups on `(SharedGameId, Name)` and within the same batch via `seenInBatch` HashSet, but discarded the count.
- [x] **Step 2: Plumb the count through the stack** — refactored to surface inserted/skipped split:
  - `IMechanicGoldenBggTagRepository.UpsertBatchAsync` signature: `Task` → `Task<int>` (returns count of rows newly added to change tracker).
  - New `BggImportResult(int Inserted, int Skipped)` record alongside `ImportBggTagsCommand`.
  - `ImportBggTagsCommand : ICommand<int>` → `ICommand<BggImportResult>`.
  - Handler computes `Skipped = requestedCount - insertedCount`; empty submissions short-circuit to `(0, 0)` with no repo/UoW/cache touch.
  - Endpoint (`AdminMechanicExtractorValidationEndpoints.cs:173`) returns `Results.Ok(new { result.Inserted, result.Skipped })`.
  - Frontend `ImportBggTagsResponseSchema` (`admin-mechanic-extractor-validation.schemas.ts`) updated from `{ upserted }` to `{ inserted, skipped }`.
- [x] **Step 3: Test idempotency** — 9/9 `ImportBggTagsHandlerTests` green:
  - `Handle_HappyPath_UpsertsBatchAndInvalidatesCache` — 3 new tags → `Inserted=3, Skipped=0`, cache invalidated.
  - `Handle_PartialDuplicates_SplitsInsertedAndSkipped` (new) — 4 submitted, repo reports 2 inserted → `Inserted=2, Skipped=2`.
  - `Handle_EmptyTagList_ReturnsZeroAndSkipsPersistence` — `(0, 0)` w/o touching repo/UoW/cache.
  - `Handle_PersistsBeforeCacheInvalidation` — sequence `upsert → save → cache` enforced.
  - `Handle_DuplicateNamesInInput_PassesAllToRepo` — handler passes all 3 tuples to repo (dedup is repo's responsibility); repo reports 2 inserted → `Inserted=2, Skipped=1`.
  - 4 constructor null-guard + 1 null-command test.
  - Integration tests: amended `UpsertBatchAsync_InsertsNewTags_AndReadsThemBack` to assert `insertedCount.Should().Be(3)`; amended `UpsertBatchAsync_IsIdempotent_OnDuplicateTagName` to assert `firstInserted=2, secondInserted=1`.
- [x] **Step 4: Commit**

---

### Task 18: BggImporterPasteDialog component ✅

**Files:**
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/BggImporterPasteDialog.tsx`
- Create: `apps/web/src/lib/parsers/bgg-tsv.ts` — `parseBggTsv(text: string): { rows: BggTagRow[]; errors: string[] }`
- Create: `apps/web/src/lib/parsers/__tests__/bgg-tsv.test.ts`
- Create: `apps/web/src/hooks/admin/useImportBggTags.ts`
- Create: `apps/web/src/hooks/admin/__tests__/useImportBggTags.test.tsx`
- Create: `apps/web/src/components/admin/mechanic-extractor/validation/__tests__/BggImporterPasteDialog.test.tsx`

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

- [x] **Step 1: Write parser tests, then parser, then dialog tests, then dialog**
  - Parser: 12 Vitest cases covering happy path, missing TAB, mixed valid+errors, blank-line skip, whitespace trim, Windows CRLF, empty category/name, in-paste dedup, ordinal case-sensitivity (Mechanism ≠ mechanism, mirrors server's `seenInBatch` HashSet w/ `StringComparer.Ordinal`), empty input variants, multi-error stable line numbers.
  - Hook `useImportBggTags`: 6 tests asserting `api.admin.importBggTags(sharedGameId, tags)` call shape, `mechanicValidationKeys.golden.byGame` invalidation, three pluralization variants of the success toast (skipped===0 / skipped===1 / skipped>1) — wired to the Task 17 `BggImportResult { Inserted, Skipped }` contract — and the failure-toast prefix.
  - Dialog: 8 tests covering disabled-by-default CTA, live preview render after paste, parser errors next to textarea, CTA stays disabled on whitespace-only / errors-only paste, `mutate` receives parsed rows (not raw text), close on success, stay open on error, disabled while pending.
- [x] **Step 2: Dialog flow:** open → paste textarea → live preview table → parser-error chips next to textarea (red) → "Insert N tag(s)" CTA disabled until at least one well-formed row → on success close + hook fires toast (`Imported N BGG tag(s) (M skipped as duplicate(s))`) → on error stay open + hook fires error toast.
  - Decision: in-paste duplicates are folded silently by the parser (so the live preview row count matches what the importer will actually send); duplicates against existing server rows surface only post-insert via `BggImportResult.Skipped` in the toast. Avoided a synchronous client-side "already exists" lookup against `golden.byGame` because (a) it would race the server, (b) the toast already names the count, (c) keeps the dialog stateless w.r.t. existing data.
  - Pending-state lockout: dialog refuses to close (Esc / outside-click / X) while `mutation.isPending`, mirroring `OverrideCertificationDialog`. Submit button label flips to "Importing…" with spinner.
- [x] **Step 3: Test fixture inputs end-to-end** — covered via the 26 unit tests above (TDD); E2E browser scenario lands in Phase 8.
- [x] **Step 4: Commit**

---

### Task 19: Mount on golden detail page ✅

- [x] **Step 1: Modify** `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/golden/[gameId]/page.tsx`
  - Added "Import BGG tags" outline `Button` with `ClipboardPasteIcon`, sitting next to "New claim" in a flex action group at the top-right of the header.
  - Renders `<BggImporterPasteDialog>` controlled by local `bggImporterOpen` state outside the header (so the dialog portal isn't tied to the action group's flex layout).
  - Trigger `data-testid="bgg-importer-trigger"` for the Phase 8 E2E.
  - Feature-flag gate already in place at the top of the page (`NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'` → `notFound()` otherwise) — no extra guard needed for the new button.
  - Typecheck clean (0 errors), no pre-existing-test regressions.
- [x] **Step 2: Commit**

---

## Phase 5 — Recalc-All UI

### Task 20: API client extensions ✅

**Files actually touched** (plan called out a legacy path; the modern client tree is structured differently):
- Modify: `apps/web/src/lib/api/clients/admin/adminMechanicExtractorValidationClient.ts`
- Modify: `apps/web/src/lib/api/schemas/admin-mechanic-extractor-validation.schemas.ts`

- [x] **Step 1: Add functions + types**
  - **Schemas** — added `RecalcJobStatusSchema` (string enum: Pending/Running/Completed/Failed/Cancelled), `RecalcJobStatusDtoSchema` (mirrors backend `RecalcJobStatusDto.cs` field-for-field — id, status, triggeredByUserId, total/processed/failed/skipped, consecutiveFailures, lastError, cancellationRequested, createdAt + nullable startedAt/completedAt/heartbeatAt, etaSeconds), and `EnqueueRecalcAllResponseSchema` (`{ jobId: uuid }`).
  - **Client** — added two route constants (`recalcJobById(jobId)` and `cancelRecalcJob(jobId)`) and three methods on `api.admin`:
    - `enqueueRecalcAll()` — `POST /metrics/recalculate-all`, returns `{ jobId }` from the 202 body.
    - `getRecalcJobStatus(jobId)` — `GET /metrics/recalc-jobs/{id}`, returns the full `RecalcJobStatusDto` (uncached on the backend by design).
    - `cancelRecalcJob(jobId)` — `POST /metrics/recalc-jobs/{id}/cancel`, void on 204; conflict (409) propagates as a thrown HTTP error from `httpClient.post`.
  - **Removed**: the stale Sprint 1 `recalculateAllMetrics` method + `RecalculateAllMetricsResponseSchema` (`{ processed }`) — the backend endpoint is now async (Task 10), so the old `{ processed }` shape can never deserialize. Confirmed via grep that no consumer referenced either symbol.
  - Decision: kept `httpClient.post(url, {}, schema)` instead of a hypothetical `httpClient.accepted(...)` helper — the client's existing post helper already returns the parsed body, and 202 vs 200 status discrimination is irrelevant here (we only consume the body).
- [x] **Step 2: Unit test mocked** — repo convention is zero client-level tests; every client method is covered through its hook (see Task 18's `useImportBggTags.test.tsx`). Behavioral coverage for these three methods lands as part of Task 21 hook tests (`useEnqueueRecalcAll`, `useRecalcJobStatus` polling, `useCancelRecalcJob`). Schema-level guarantees come for free from the Zod parsers wired into `httpClient.get/post`.
- [x] **Step 3: Commit**

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

- [x] **Step 1: Write component tests for each behavior** — TDD trio for hooks (`useEnqueueRecalcAll.test.tsx` 4 tests, `useRecalcJobStatus.test.tsx` 4 tests, `useCancelRecalcJob.test.tsx` 5 tests = 13 hook tests) + components (`RecalcAllButton.test.tsx` 4 tests covering idle label, mutate-with-undefined, `onJobStarted` jobId forwarding, pending-state disable+spinner; `RecalcProgressDrawer.test.tsx` 10 tests covering loading, progress fraction, ETA, Cancel-fires-mutate, Cancelling-disabled-when-`cancellationRequested`, no-Cancel-when-terminal, exactly-once toast on each terminal transition Completed/Failed/Cancelled, null-jobId render). Hook tests document the polling stop-on-terminal contract via `refetchInterval` callback inspection (full timer-driven test deferred — exercised in drawer integration).
- [x] **Step 2: Implement** — Three hooks colocated under `apps/web/src/hooks/admin/`:
  - `useEnqueueRecalcAll` (mutation; no success toast — drawer owns terminal toasts; error toast `Failed to enqueue recalc: …`).
  - `useRecalcJobStatus` (query polling at 2s; `refetchInterval` callback returns `false` once `data.status` ∈ `{Completed, Failed, Cancelled}`; key factory `mechanicValidationKeys.recalcJob.byId(jobId)` extended in `mechanicValidationKeys.ts`).
  - `useCancelRecalcJob` (mutation; `toast.info('Cancellation requested — worker will stop on its next iteration')` since cancel is cooperative; invalidates the job-status query for snappy UI feedback).
  - Two components colocated under `apps/web/src/components/admin/mechanic-extractor/validation/`:
  - `RecalcAllButton.tsx` — disabled while `mutation.isPending`; idle label "Recalculate all", pending label "Enqueueing…"; calls `onJobStarted(response.jobId)` from the success callback.
  - `RecalcProgressDrawer.tsx` — inline panel (not a portal Sheet — Sprint 2 ships inline; future sprints can swap to `Sheet`); renders progress fraction `processed / total`, `<Progress>` bar, counters in `<dl>` (failed / skipped / consecutive failures / ETA in `<dt>/<dd>` pairs), `lastError` block, Cancel button while non-terminal, Close (X) button only once terminal. Cancel UX has three states: enabled "Cancel" (idle, running), disabled spinner "Cancel" (mutation in flight), disabled spinner "Cancelling…" (worker hasn't honored the flag yet — `cancellationRequested === true`). Terminal toasts fired exactly once per status transition via a `useRef<TerminalStatus | null>(null)` latch — re-poll cycles returning the same Completed snapshot do NOT re-toast (latch resets to null when the status flips back to non-terminal so a future Sprint 3 slot reuse works correctly).
- [x] **Step 3: Verify tests pass** — `pnpm vitest run` over both component files: 14/14 green. `pnpm typecheck` clean.
- [x] **Step 4: Commit**

---

### Task 22: Mount on dashboard page

- [x] **Step 1: Modify** `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/dashboard/page.tsx`:
  - Imported `useState`, `RecalcAllButton`, `RecalcProgressDrawer`.
  - Added `const [activeJobId, setActiveJobId] = useState<string | null>(null);`.
  - Wrapped the page header in `<div className="flex items-start justify-between gap-4">` so the button anchors to the top-right of the title block.
  - Mounted `<RecalcAllButton onJobStarted={id => setActiveJobId(id)} />` next to the heading; the drawer renders below the header when `activeJobId !== null`: `{activeJobId && <RecalcProgressDrawer jobId={activeJobId} onClose={() => setActiveJobId(null)} />}`.
  - Admin gate is **not** duplicated on the page — `apps/web/src/app/admin/(dashboard)/layout.tsx:36` already wraps the entire route group in `<RequireRole allowedRoles={['Admin']}>`. The feature flag is also already gated at the top of the page (`if (process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED !== FEATURE_FLAG) notFound()`), so the plan's `isMechanicValidationEnabled() && session.user.role === 'Admin'` guard is implicitly satisfied — if the page renders at all, both checks pass.
  - Typecheck clean.
- [x] **Step 2: Commit**

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

- [x] **Step 1: Add files** — created `apps/web/src/lib/feature-flags/mechanic-validation.ts` (`isMechanicValidationEnabled()` returns strict `===' 'true'` — no truthy coercion) and `apps/web/src/components/admin/mechanic-extractor/validation/FeatureFlagGate.tsx` (`'use client'` component returning `null` or an optional `fallback` when off).
- [x] **Step 2: Unit tests** — `mechanic-validation.test.ts` (5 tests: `'true'`/`'false'`/non-canonical truthy/unset/empty) + `FeatureFlagGate.test.tsx` (4 tests: renders children when on, null when unset/false, custom fallback). Both pinned to `@vitest-environment jsdom` to share the project's browser-polyfill setup.
- [x] **Step 3: Wrap each Sprint 2 admin section** — refactored the four call sites that used to inline `process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'`: dashboard/page.tsx, golden/page.tsx, golden/[gameId]/page.tsx now call `if (!isMechanicValidationEnabled()) notFound()`; review/page.tsx swaps the inline ternary for `<FeatureFlagGate><ValidationSection ... /></FeatureFlagGate>`. Verified zero `process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED` references remain in non-helper/non-test code.
- [x] **Step 4: Commit**

---

### Task 26: Compose flag wiring

**Files:**
- Modify: `apps/web/.env.development.example` (project uses Next.js per-app env files; `infra/.env.development` does not exist)
- Modify: `infra/compose.staging.yml` (project uses `compose.<env>.yml` naming, not `docker-compose.staging.yml`)
- Modify: `docs/operations/operations-manual.md`
- (Plan originally listed `infra/secrets/api-staging.secret.example` — N/A: `NEXT_PUBLIC_*` is a Next.js build-time public flag, not an API secret, and that file does not exist in `infra/secrets/`.)

- [x] **Step 1: Add to dev env example** — flipped `apps/web/.env.development.example` from `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=false` (Sprint 1 opt-in) to `=true`, with refreshed comment block referencing Sprint 1+2 surfaces and explaining strict literal-`'true'` semantics. Devs now get the feature on by default after `cp apps/web/.env.development.example apps/web/.env.local`.
- [x] **Step 2: Staging compose wiring** — added `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED: "${NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED:-false}"` to `infra/compose.staging.yml` web service under both `build.args` (Next.js inlines `NEXT_PUBLIC_*` at build time) and `environment` (SSR/runtime parity). Default-off until calibration spike (Tasks 23-24) clears; operators flip via host-shell export before `make staging`.
- [x] **Step 3: Operations manual** — appended a "Feature flags" subsection to Appendix B (after Integration Tests) documenting the dual-channel build-arg + runtime-env requirement, a table of all three `NEXT_PUBLIC_*` flags (`ALPHA_MODE`, `MOCK_MODE`, `MECHANIC_VALIDATION_ENABLED`) with defaults / purpose / owner, and the host-export workflow for enabling in staging without editing compose.
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
