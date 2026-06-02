# Issue #1675 — Per-Doc Quality Eval Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere un sistema di valutazione per-doc della qualità RAG (precision@K, MRR, latency) con goldset LLM-generated, cost cap, audit, e UI admin, isolato in un nuovo BC `KbQuality`.

**Architecture:** Nuovo Bounded Context `KbQuality` con aggregate `DocumentEvaluationRun`, 3 endpoint REST `/api/v1/admin/kb/docs/{docId}/evaluations`, MediatR pipeline behaviors per cost cap (mirror Mechanic Extractor M1.2 ADR-051) e rate limit. Cross-BC dependencies via ports & adapters verso KnowledgeBase, DocumentProcessing, SystemConfiguration, Administration. LLM-generated goldset con seed pinning per re-runnability deterministic. Quality bands (red/yellow/green) config-driven via `EvalQuality:QualityBands` con hot-reload.

**Tech Stack:**
- BE: .NET 9, ASP.NET Minimal APIs + MediatR (CQRS), EF Core 9 (PostgreSQL + pgvector), FluentValidation, xUnit + Testcontainers + WireMock.Net, FluentAssertions, Prometheus.NET
- FE: Next.js 16 (App Router) + React 19, TypeScript, TanStack Query v5, Zod, Vitest + React Testing Library, Tailwind 4, Playwright (E2E)

**Issue**: [#1675](https://github.com/meepleAi-app/meepleai-monorepo/issues/1675) (P3)
**Spec**: `docs/superpowers/specs/2026-06-01-sp5-admin-kb-fu4-spinouts-design.md` §3.3 (post brainstorm update 2026-06-02)
**Decision log**: D-F (ownership), D-G (success criteria), D-H (cost cap), D-I (BC location)

**Branch**: `feature/issue-1675-per-doc-quality-eval` (parent: `main-dev`)

**Effort**: ~29 hours (vedi spec §3.3 effort revisione table)

---

## Scope freeze (out-of-scope)

- Cross-doc bulk operations (re-index N docs, eval batch, compare embeddings) → separate epic
- Public/user-facing display of any of the data above → strictly admin
- Real-time streaming eval (#1675 is async only)
- Fase 2 (manual + feedback goldset strategies) — D-F trigger oggettivo, epic separato
- Quality bands calibration con dati storici reali → OQ-5 post-ship

## File Structure

### Backend — Created (BC `KbQuality`)

```
apps/api/src/Api/BoundedContexts/KbQuality/
├── Domain/
│   ├── Evaluation/
│   │   ├── DocumentEvaluationRun.cs
│   │   ├── EvaluationStatus.cs
│   │   ├── EvaluationMetrics.cs
│   │   ├── PrecisionMetrics.cs
│   │   ├── RankingMetrics.cs
│   │   ├── LatencyMetrics.cs
│   │   ├── QualityBand.cs
│   │   └── Exceptions/
│   │       ├── CostCapExceededException.cs
│   │       ├── EvalRateLimitedException.cs
│   │       └── InvalidGoldsetVersionException.cs
│   └── Goldset/
│       ├── GoldsetVersion.cs
│       ├── GoldsetStrategy.cs
│       └── GoldsetGenerationResult.cs
├── Application/
│   ├── Commands/StartEvaluation/
│   │   ├── StartEvaluationCommand.cs
│   │   ├── StartEvaluationCommandValidator.cs
│   │   ├── StartEvaluationCommandHandler.cs
│   │   └── EvaluationStartedResult.cs
│   ├── Queries/
│   │   ├── GetEvaluation/
│   │   │   ├── GetEvaluationQuery.cs
│   │   │   ├── GetEvaluationQueryHandler.cs
│   │   │   └── EvaluationDetailDto.cs
│   │   └── ListEvaluations/
│   │       ├── ListEvaluationsQuery.cs
│   │       ├── ListEvaluationsQueryHandler.cs
│   │       ├── EvaluationRunListItemDto.cs
│   │       └── PagedEvaluationsDto.cs
│   ├── Services/
│   │   ├── IGoldsetGenerator.cs
│   │   ├── IEvaluationExecutor.cs
│   │   ├── EvaluationExecutor.cs
│   │   ├── IEvaluationMetricsCalculator.cs
│   │   ├── EvaluationMetricsCalculator.cs
│   │   ├── IQualityBandResolver.cs
│   │   ├── QualityBandResolver.cs
│   │   ├── IEvaluationCostEstimator.cs
│   │   └── EvaluationCostEstimator.cs
│   ├── Behaviors/
│   │   ├── EvalCostCapBehavior.cs
│   │   └── EvalRateLimitBehavior.cs
│   ├── Ports/
│   │   ├── IKbSearchProvider.cs
│   │   ├── IPdfDocumentReadModel.cs
│   │   ├── IEvalCostBudgetChecker.cs
│   │   └── IAuditLogger.cs
│   └── Configuration/
│       ├── EvalQualityOptions.cs
│       └── QualityBandsConfig.cs
├── Infrastructure/
│   ├── EvaluationRepository.cs
│   ├── IEvaluationRepository.cs
│   ├── EntityConfigurations/
│   │   └── DocumentEvaluationRunEntityConfiguration.cs
│   ├── Migrations/<TS>_AddKbQualityTables.cs
│   ├── Adapters/
│   │   ├── KbSearchProviderAdapter.cs
│   │   ├── PdfDocumentReadModelAdapter.cs
│   │   ├── EvalCostBudgetCheckerAdapter.cs
│   │   └── AuditLoggerAdapter.cs
│   ├── Services/
│   │   └── LlmGoldsetGenerator.cs
│   └── BackgroundJobs/
│       ├── KbQualityRetentionJob.cs
│       └── KbQualityCostCapResetJob.cs
├── Routing/
│   └── AdminKbQualityEndpoints.cs
└── KbQualityModule.cs                          # DI registration extension method
```

### Backend — Modified

| Path | Change |
|------|--------|
| `apps/api/src/Api/Program.cs` | Add `builder.Services.AddKbQualityModule(builder.Configuration)` |
| `apps/api/src/Api/Routing/RouteRegistry.cs` (or equivalent) | Add `app.MapAdminKbQualityEndpoints()` |
| `apps/api/src/Api/Infrastructure/AppDbContext.cs` | Add `DbSet<DocumentEvaluationRun>` |
| `apps/api/src/Api/appsettings.json` | Add `EvalQuality` config section (defaults) |
| `apps/api/src/Api/appsettings.Development.json` | Override per dev (es. lower MonthlyCostCap=5.00) |

### Backend — Tests

```
tests/Api.Tests/BoundedContexts/KbQuality/
├── Unit/
│   ├── Domain/
│   │   ├── DocumentEvaluationRunTests.cs
│   │   ├── GoldsetVersionRegistryTests.cs
│   │   └── EvaluationMetricsTests.cs
│   └── Application/
│       ├── EvaluationMetricsCalculatorTests.cs
│       ├── QualityBandResolverTests.cs
│       └── EvaluationCostEstimatorTests.cs
└── Integration/
    ├── KbQualityIntegrationFixture.cs           # Testcontainers Postgres + WireMock LLM
    ├── StartEvaluationIntegrationTests.cs       # Scenari A, B, C, C2, D, E
    ├── AuditIntegrationTests.cs                 # Level=2 payload assertion
    └── RetentionJobIntegrationTests.cs          # 18m deletion
```

### Frontend — Created

```
apps/web/src/
├── lib/
│   ├── api/
│   │   ├── schemas/kb-quality.schemas.ts        # Zod schemas (R-12 mirror)
│   │   └── clients/kbQualityClient.ts           # API client (3 endpoints)
│   └── format/
│       └── quality-band.ts                       # Band → color/icon mapping
├── hooks/queries/
│   ├── useStartEvaluation.ts                    # Mutation hook
│   ├── useEvaluation.ts                          # Single eval detail
│   └── useEvaluationList.ts                      # Paginated list
└── components/admin/knowledge-base/explorer/
    ├── quality/
    │   ├── EvaluationTriggerButton.tsx          # Modal + cost preview + override toggle
    │   ├── EvaluationHistoryList.tsx            # Lista paginata
    │   ├── EvaluationRunDetailPanel.tsx         # Full metrics + Q&A pairs
    │   ├── EvaluationStatusChip.tsx             # Pending/Running/Completed/Failed
    │   └── QualityBandChip.tsx                  # Red/Yellow/Green visual
    └── __tests__/
        ├── EvaluationTriggerButton.test.tsx
        ├── EvaluationHistoryList.test.tsx
        ├── EvaluationRunDetailPanel.test.tsx
        └── QualityBandChip.test.tsx
```

### Frontend — Modified

| Path | Change |
|------|--------|
| `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx` | Add `quality` tab key |
| `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx` | Render `<QualityTabPanel docId={doc.id}/>` quando `activeTab === 'quality'` |

### Frontend — E2E

```
apps/web/e2e/admin/
└── kb-quality-eval-happy-path.spec.ts            # Playwright: trigger → wait → see metrics
```

---

## Pre-execution checklist

- [ ] Branch `feature/issue-1675-per-doc-quality-eval` checked out from `main-dev`
- [ ] `git config branch.feature/issue-1675-per-doc-quality-eval.parent main-dev`
- [ ] Spec letta: `docs/superpowers/specs/2026-06-01-sp5-admin-kb-fu4-spinouts-design.md` §3.3
- [ ] Verifica precedent codice: `apps/api/src/Api/BoundedContexts/MechanicExtractor/Application/Behaviors/CostCapBehavior.cs` (mirror pattern D-H)
- [ ] Verifica precedent codice: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersion.cs` (mirror pattern R-2)

---

## Phase A — Foundation (Tasks 1-3)

### Task 1: Branch setup + BC scaffolding

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/.gitkeep` (placeholder for empty dirs)
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs`

- [ ] **Step 1: Verifica branch corrente**

Run:
```bash
git branch --show-current
```
Expected output: `feature/issue-1675-per-doc-quality-eval`

- [ ] **Step 2: Crea KbQualityModule.cs skeleton**

Create `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.KbQuality;

/// <summary>
/// DI registration entry point for the KbQuality bounded context (#1675).
/// Wires aggregate repo, application services, MediatR behaviors, ports/adapters,
/// background jobs, and configuration options.
/// </summary>
public static class KbQualityModule
{
    public static IServiceCollection AddKbQualityModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Services + behaviors + adapters registered task-by-task; placeholder for now.
        return services;
    }
}
```

- [ ] **Step 3: Modifica Program.cs per registrare il modulo**

Modify `apps/api/src/Api/Program.cs` — find the section where other BC modules are registered (search for `AddKnowledgeBase` or similar) and add:

```csharp
builder.Services.AddKbQualityModule(builder.Configuration);
```

- [ ] **Step 4: Build per verificare zero regression**

Run:
```bash
cd apps/api/src/Api
dotnet build
```
Expected: `Build succeeded. 0 Warnings. 0 Errors.`

- [ ] **Step 5: Commit foundation**

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/ apps/api/src/Api/Program.cs
git commit -m "feat(kb-quality): #1675 bootstrap BC scaffolding + DI module"
```

---

### Task 2: EF migration `document_evaluation_runs`

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<TS>_AddKbQualityTables.cs` (auto-generated)
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/KbQuality/DocumentEvaluationRunEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/AppDbContext.cs`

- [ ] **Step 1: Write integration test for migration roundtrip**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Integration/DocumentEvaluationRunsMigrationTests.cs`:

```csharp
using Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Integration;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "KbQuality")]
public sealed class DocumentEvaluationRunsMigrationTests
    : IClassFixture<KbQualityIntegrationFixture>
{
    private readonly KbQualityIntegrationFixture _fixture;

    public DocumentEvaluationRunsMigrationTests(KbQualityIntegrationFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task Migration_CreatesTable_WithExpectedColumns()
    {
        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tableExists = await db.Database
            .SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'document_evaluation_runs')")
            .SingleAsync();

        tableExists.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Create the EntityConfiguration**

Create `apps/api/src/Api/Infrastructure/EntityConfigurations/KbQuality/DocumentEvaluationRunEntityConfiguration.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KbQuality;

internal sealed class DocumentEvaluationRunEntityConfiguration
    : IEntityTypeConfiguration<DocumentEvaluationRun>
{
    public void Configure(EntityTypeBuilder<DocumentEvaluationRun> builder)
    {
        builder.ToTable("document_evaluation_runs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PdfDocumentId).IsRequired();
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.CompletedAt);
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(e => e.GoldsetVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.GoldsetGenerationSeed).IsRequired();
        builder.Property(e => e.CostUsd).HasPrecision(10, 4);
        builder.Property(e => e.TriggeredByAdminId).IsRequired();
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        // Metrics stored as JSONB (composed value object — flatten on read via projection)
        builder.OwnsOne(e => e.Metrics, mb =>
        {
            mb.ToJson();
            mb.OwnsOne(m => m.Precision);
            mb.OwnsOne(m => m.Ranking);
            mb.OwnsOne(m => m.Latency);
        });

        builder.HasIndex(e => new { e.PdfDocumentId, e.StartedAt }).IsDescending(false, true);
        builder.HasIndex(e => e.TriggeredByAdminId);
        builder.HasIndex(e => e.CompletedAt);  // for retention job WHERE filter
    }
}
```

- [ ] **Step 3: Add DbSet + apply configuration in AppDbContext**

Modify `apps/api/src/Api/Infrastructure/AppDbContext.cs`:

Find the `DbSet` declarations section and add:
```csharp
public DbSet<DocumentEvaluationRun> DocumentEvaluationRuns => Set<DocumentEvaluationRun>();
```

Find `OnModelCreating` and add inside (after existing `ApplyConfiguration` calls):
```csharp
modelBuilder.ApplyConfiguration(new DocumentEvaluationRunEntityConfiguration());
```

- [ ] **Step 4: Generate migration**

Run:
```bash
cd apps/api/src/Api
dotnet ef migrations add AddKbQualityTables
```
Expected: `Build succeeded.` + `Done.` + new files under `Infrastructure/Migrations/`

- [ ] **Step 5: Review migration SQL**

Read the generated migration file. Verify:
- `CREATE TABLE document_evaluation_runs` con tutte le colonne sopra
- `metrics` column tipo `jsonb` (PostgreSQL)
- 3 indici creati

If anything looks wrong, regenerate with `dotnet ef migrations remove` and fix the config.

- [ ] **Step 6: Apply migration locally + run test**

Run:
```bash
cd apps/api/src/Api
dotnet ef database update
cd ../../../../tests/Api.Tests
dotnet test --filter "FullyQualifiedName~DocumentEvaluationRunsMigrationTests" -v normal
```
Expected: 1 test passing.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/ tests/Api.Tests/BoundedContexts/KbQuality/Integration/DocumentEvaluationRunsMigrationTests.cs
git commit -m "feat(kb-quality): #1675 EF migration document_evaluation_runs table"
```

---

### Task 3: Aggregate `DocumentEvaluationRun` + `EvaluationStatus` enum

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/DocumentEvaluationRun.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/EvaluationStatus.cs`
- Test: `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/DocumentEvaluationRunTests.cs`

- [ ] **Step 1: Write failing unit tests for factory `Create`**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/DocumentEvaluationRunTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class DocumentEvaluationRunTests
{
    private static readonly Guid DocId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AdminId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Create_WithoutReuseSeed_GeneratesRandomSeed()
    {
        var run1 = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, reuseSeed: null);
        var run2 = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, reuseSeed: null);

        run1.GoldsetGenerationSeed.Should().NotBe(0);
        run2.GoldsetGenerationSeed.Should().NotBe(0);
        // Two distinct invocations should not collide (probabilistic ~1/2^63 false positive)
        run1.GoldsetGenerationSeed.Should().NotBe(run2.GoldsetGenerationSeed);
    }

    [Fact]
    public void Create_WithReuseSeed_PinsToProvidedValue()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, reuseSeed: 42L);

        run.GoldsetGenerationSeed.Should().Be(42L);
    }

    [Fact]
    public void Create_SetsInitialState()
    {
        var before = DateTime.UtcNow;
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, reuseSeed: null);
        var after = DateTime.UtcNow;

        run.Id.Should().NotBe(Guid.Empty);
        run.PdfDocumentId.Should().Be(DocId);
        run.GoldsetVersion.Should().Be("auto-v1");
        run.TriggeredByAdminId.Should().Be(AdminId);
        run.Status.Should().Be(EvaluationStatus.Pending);
        run.StartedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        run.CompletedAt.Should().BeNull();
        run.Metrics.Should().BeNull();
        run.CostUsd.Should().BeNull();
        run.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void TransitionTo_FromPending_AllowsGoldsetGenerating()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, null);

        run.TransitionTo(EvaluationStatus.GoldsetGenerating);

        run.Status.Should().Be(EvaluationStatus.GoldsetGenerating);
    }

    [Fact]
    public void MarkCompleted_SetsTerminalState()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, null);
        run.TransitionTo(EvaluationStatus.GoldsetGenerating);
        run.TransitionTo(EvaluationStatus.Running);

        var metrics = TestData.SampleMetrics();
        run.MarkCompleted(metrics, costUsd: 0.05m);

        run.Status.Should().Be(EvaluationStatus.Completed);
        run.CompletedAt.Should().NotBeNull();
        run.Metrics.Should().Be(metrics);
        run.CostUsd.Should().Be(0.05m);
    }

    [Fact]
    public void MarkFailed_SetsErrorMessage()
    {
        var run = DocumentEvaluationRun.Create(DocId, "auto-v1", AdminId, null);

        run.MarkFailed("LLM timeout after 30s");

        run.Status.Should().Be(EvaluationStatus.Failed);
        run.ErrorMessage.Should().Be("LLM timeout after 30s");
        run.CompletedAt.Should().NotBeNull();
    }
}

internal static class TestData
{
    public static EvaluationMetrics SampleMetrics() => new(
        Precision: new PrecisionMetrics(At1: 0.8, At3: 0.7, At5: 0.65),
        Ranking: new RankingMetrics(Mrr: 0.55),
        Latency: new LatencyMetrics(P50: TimeSpan.FromMilliseconds(120), P95: TimeSpan.FromMilliseconds(450)),
        QueryCount: 15,
        CostUsd: 0.05m,
        QualityBand: QualityBand.Yellow);
}
```

- [ ] **Step 2: Run tests to verify they fail (no aggregate yet)**

Run:
```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~DocumentEvaluationRunTests" -v normal
```
Expected: compile errors `DocumentEvaluationRun`/`EvaluationStatus`/`EvaluationMetrics`/`PrecisionMetrics`/`RankingMetrics`/`LatencyMetrics`/`QualityBand` not found.

- [ ] **Step 3: Create EvaluationStatus enum**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/EvaluationStatus.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Evaluation;

/// <summary>
/// Lifecycle status of a per-doc evaluation run (#1675 §3.3).
/// </summary>
public enum EvaluationStatus
{
    Pending,
    GoldsetGenerating,
    Running,
    Completed,
    Failed,
    RateLimited,
    CostCapped
}
```

- [ ] **Step 4: Create DocumentEvaluationRun aggregate**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/DocumentEvaluationRun.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Evaluation;

/// <summary>
/// Aggregate root tracking a single per-doc evaluation run lifecycle.
/// Issue #1675 — design doc §3.3.
/// </summary>
public sealed class DocumentEvaluationRun
{
    public Guid Id { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public EvaluationStatus Status { get; private set; }
    public string GoldsetVersion { get; private set; } = default!;
    public long GoldsetGenerationSeed { get; private set; }
    public EvaluationMetrics? Metrics { get; private set; }
    public decimal? CostUsd { get; private set; }
    public Guid TriggeredByAdminId { get; private set; }
    public string? ErrorMessage { get; private set; }

    // EF Core ctor
    private DocumentEvaluationRun() { }

    public static DocumentEvaluationRun Create(
        Guid pdfDocumentId,
        string goldsetVersion,
        Guid triggeredByAdminId,
        long? reuseSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goldsetVersion);
        if (pdfDocumentId == Guid.Empty) throw new ArgumentException("pdfDocumentId required", nameof(pdfDocumentId));
        if (triggeredByAdminId == Guid.Empty) throw new ArgumentException("triggeredByAdminId required", nameof(triggeredByAdminId));

        return new DocumentEvaluationRun
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfDocumentId,
            GoldsetVersion = goldsetVersion,
            GoldsetGenerationSeed = reuseSeed ?? unchecked((long)Random.Shared.NextInt64()),
            TriggeredByAdminId = triggeredByAdminId,
            StartedAt = DateTime.UtcNow,
            Status = EvaluationStatus.Pending,
        };
    }

    public void TransitionTo(EvaluationStatus next)
    {
        // Allow forward-only transitions per state machine (#1675 §3.3).
        // Terminal states (Completed, Failed, RateLimited, CostCapped) cannot transition.
        if (Status is EvaluationStatus.Completed or EvaluationStatus.Failed
                   or EvaluationStatus.RateLimited or EvaluationStatus.CostCapped)
        {
            throw new InvalidOperationException($"Cannot transition from terminal state {Status} to {next}");
        }

        Status = next;
    }

    public void MarkCompleted(EvaluationMetrics metrics, decimal costUsd)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        Status = EvaluationStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Metrics = metrics;
        CostUsd = costUsd;
    }

    public void MarkFailed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        Status = EvaluationStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }
}
```

- [ ] **Step 5: Create stub records for compile (full impl in Task 5)**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/EvaluationMetrics.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Evaluation;

// Full implementation in Task 5 — stub for compile dependency from Task 3.
public sealed record EvaluationMetrics(
    PrecisionMetrics Precision,
    RankingMetrics Ranking,
    LatencyMetrics Latency,
    int QueryCount,
    decimal CostUsd,
    QualityBand QualityBand);

public sealed record PrecisionMetrics(double At1, double At3, double At5);
public sealed record RankingMetrics(double Mrr);
public sealed record LatencyMetrics(TimeSpan P50, TimeSpan P95);

public enum QualityBand { Red, Yellow, Green }
```

- [ ] **Step 6: Run tests to verify they pass**

Run:
```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~DocumentEvaluationRunTests" -v normal
```
Expected: 6 tests passing.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/Domain/ tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/DocumentEvaluationRunTests.cs
git commit -m "feat(kb-quality): #1675 DocumentEvaluationRun aggregate + status enum"
```

---

## Phase B — Domain Value Objects (Tasks 4-6)

### Task 4: `GoldsetVersion` value object + registry

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Goldset/GoldsetVersion.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Goldset/GoldsetStrategy.cs`
- Test: `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/GoldsetVersionRegistryTests.cs`

- [ ] **Step 1: Write failing registry tests**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/GoldsetVersionRegistryTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Domain.Goldset;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class GoldsetVersionRegistryTests
{
    [Fact]
    public void Registry_ContainsAutoCurrent()
    {
        GoldsetVersion.Registry.Should().ContainSingle(v => v.Version == "auto-v1");
        GoldsetVersion.AutoCurrent.Version.Should().Be("auto-v1");
        GoldsetVersion.AutoCurrent.Strategy.Should().Be(GoldsetStrategy.LlmAutoGen);
    }

    [Fact]
    public void TryGet_KnownVersion_ReturnsTrue()
    {
        var found = GoldsetVersion.TryGet("auto-v1", out var version);

        found.Should().BeTrue();
        version!.DisplayName.Should().Be("Auto LLM v1");
    }

    [Fact]
    public void TryGet_UnknownVersion_ReturnsFalse()
    {
        var found = GoldsetVersion.TryGet("manual-v1", out var version);

        found.Should().BeFalse();
        version.Should().BeNull();
    }

    [Fact]
    public void TryGet_NullOrWhitespace_ReturnsFalse()
    {
        GoldsetVersion.TryGet(null, out _).Should().BeFalse();
        GoldsetVersion.TryGet("", out _).Should().BeFalse();
        GoldsetVersion.TryGet("   ", out _).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run test, verify failure**

Run:
```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~GoldsetVersionRegistryTests" -v normal
```
Expected: compile errors (`GoldsetVersion`, `GoldsetStrategy` not found).

- [ ] **Step 3: Create GoldsetStrategy enum**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Goldset/GoldsetStrategy.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Goldset;

/// <summary>
/// Goldset generation strategy (#1675 §3.3, D-C/D-F).
/// Fase 1 ships only LlmAutoGen; Manual + Feedback added when D-F trigger fires.
/// </summary>
public enum GoldsetStrategy
{
    LlmAutoGen,
    Manual,
    Feedback,
}
```

- [ ] **Step 4: Create GoldsetVersion value object + registry**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Goldset/GoldsetVersion.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace Api.BoundedContexts.KbQuality.Domain.Goldset;

/// <summary>
/// Value object identifying a goldset generation strategy version.
/// Code-resident registry parity con IndexerVersion #1673 (R-2).
/// Retention SLA: 18 months post-supersession.
/// </summary>
public sealed record GoldsetVersion(string Version, string DisplayName, GoldsetStrategy Strategy)
{
    public static GoldsetVersion AutoCurrent { get; } =
        new("auto-v1", "Auto LLM v1", GoldsetStrategy.LlmAutoGen);

    public static IReadOnlyList<GoldsetVersion> Registry { get; } = [AutoCurrent];

    public static bool TryGet(string? version, [NotNullWhen(true)] out GoldsetVersion? result)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            result = null;
            return false;
        }

        result = Registry.FirstOrDefault(v =>
            string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase));

        return result is not null;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:
```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~GoldsetVersionRegistryTests" -v normal
```
Expected: 4 tests passing.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/Domain/Goldset/ tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/GoldsetVersionRegistryTests.cs
git commit -m "feat(kb-quality): #1675 GoldsetVersion value object + registry"
```

---

### Task 5: Domain exceptions

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/Exceptions/CostCapExceededException.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/Exceptions/EvalRateLimitedException.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/Exceptions/InvalidGoldsetVersionException.cs`
- Test: `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/KbQualityExceptionsTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/KbQualityExceptionsTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class KbQualityExceptionsTests
{
    [Fact]
    public void CostCapExceeded_CarriesEstimatedAndRemaining()
    {
        var ex = new CostCapExceededException(estimated: 0.60m, remaining: 0.50m);

        ex.EstimatedCostUsd.Should().Be(0.60m);
        ex.RemainingBudgetUsd.Should().Be(0.50m);
        ex.Message.Should().Contain("0.60").And.Contain("0.50");
    }

    [Fact]
    public void EvalRateLimited_CarriesRetryAfter()
    {
        var retryAfter = TimeSpan.FromMinutes(7);
        var ex = new EvalRateLimitedException(retryAfter);

        ex.RetryAfter.Should().Be(retryAfter);
    }

    [Fact]
    public void InvalidGoldsetVersion_CarriesRequestedAndAvailable()
    {
        var ex = new InvalidGoldsetVersionException("manual-v1", ["auto-v1"]);

        ex.RequestedVersion.Should().Be("manual-v1");
        ex.AvailableVersions.Should().BeEquivalentTo(["auto-v1"]);
    }
}
```

- [ ] **Step 2: Run test, verify failure**

Run:
```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~KbQualityExceptionsTests" -v normal
```
Expected: compile errors.

- [ ] **Step 3: Create CostCapExceededException**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/Exceptions/CostCapExceededException.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;

public sealed class CostCapExceededException : Exception
{
    public decimal EstimatedCostUsd { get; }
    public decimal RemainingBudgetUsd { get; }

    public CostCapExceededException(decimal estimated, decimal remaining)
        : base($"Eval cost {estimated:F2} USD exceeds remaining budget {remaining:F2} USD")
    {
        EstimatedCostUsd = estimated;
        RemainingBudgetUsd = remaining;
    }
}
```

- [ ] **Step 4: Create EvalRateLimitedException**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/Exceptions/EvalRateLimitedException.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;

public sealed class EvalRateLimitedException : Exception
{
    public TimeSpan RetryAfter { get; }

    public EvalRateLimitedException(TimeSpan retryAfter)
        : base($"Eval rate limited; retry after {retryAfter.TotalSeconds:F0}s")
    {
        RetryAfter = retryAfter;
    }
}
```

- [ ] **Step 5: Create InvalidGoldsetVersionException**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/Exceptions/InvalidGoldsetVersionException.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;

public sealed class InvalidGoldsetVersionException : Exception
{
    public string RequestedVersion { get; }
    public IReadOnlyList<string> AvailableVersions { get; }

    public InvalidGoldsetVersionException(string requestedVersion, IReadOnlyList<string> availableVersions)
        : base($"Goldset '{requestedVersion}' not registered. Available: [{string.Join(", ", availableVersions)}]")
    {
        RequestedVersion = requestedVersion;
        AvailableVersions = availableVersions;
    }
}
```

- [ ] **Step 6: Run tests + commit**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~KbQualityExceptionsTests" -v normal
```
Expected: 3 tests passing.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/Domain/Evaluation/Exceptions/ tests/Api.Tests/BoundedContexts/KbQuality/Unit/Domain/KbQualityExceptionsTests.cs
git commit -m "feat(kb-quality): #1675 domain exceptions (cost cap, rate limit, invalid goldset)"
```

---

### Task 6: `EvaluationMetricsCalculator` (precision@K + MRR math)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IEvaluationMetricsCalculator.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/EvaluationMetricsCalculator.cs`
- Test: `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/EvaluationMetricsCalculatorTests.cs`

- [ ] **Step 1: Write failing unit tests**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/EvaluationMetricsCalculatorTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Application;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class EvaluationMetricsCalculatorTests
{
    private readonly EvaluationMetricsCalculator _sut = new();

    [Fact]
    public void PrecisionAtK_AllRelevantInTopK_ReturnsOne()
    {
        // 3 of 3 top-3 results are relevant
        var queryResults = new[]
        {
            new QueryResult(QueryId: "q1", RelevantHits: [true, true, true, false, false])
        };

        var metrics = _sut.Compute(queryResults);

        metrics.At1.Should().Be(1.0);
        metrics.At3.Should().Be(1.0);
        metrics.At5.Should().BeApproximately(0.6, 1e-9);  // 3/5
    }

    [Fact]
    public void PrecisionAtK_AveragesAcrossQueries()
    {
        // Query 1: 1 relevant in top-1, 1 in top-3, 1 in top-5 → P@1=1, P@3=0.33, P@5=0.2
        // Query 2: 0 relevant in top-1, 0 in top-3, 0 in top-5 → P@1=0, P@3=0, P@5=0
        var queryResults = new[]
        {
            new QueryResult("q1", [true, false, false, false, false]),
            new QueryResult("q2", [false, false, false, false, false])
        };

        var metrics = _sut.Compute(queryResults);

        metrics.At1.Should().BeApproximately(0.5, 1e-9);                 // avg(1, 0)
        metrics.At3.Should().BeApproximately((1.0/3 + 0) / 2, 1e-9);
        metrics.At5.Should().BeApproximately((0.2 + 0) / 2, 1e-9);
    }

    [Fact]
    public void Mrr_FirstRelevantAtRank1_ReturnsOne()
    {
        var queryResults = new[] { new QueryResult("q1", [true, false, false]) };

        var metrics = _sut.Compute(queryResults);

        metrics.Mrr.Should().Be(1.0);
    }

    [Fact]
    public void Mrr_AveragesReciprocalRanksAcrossQueries()
    {
        // Q1 first relevant at rank 2 → 1/2 = 0.5
        // Q2 first relevant at rank 4 → 1/4 = 0.25
        // Q3 no relevant → 0
        var queryResults = new[]
        {
            new QueryResult("q1", [false, true, false]),
            new QueryResult("q2", [false, false, false, true]),
            new QueryResult("q3", [false, false, false]),
        };

        var metrics = _sut.Compute(queryResults);

        metrics.Mrr.Should().BeApproximately((0.5 + 0.25 + 0) / 3.0, 1e-9);
    }
}
```

- [ ] **Step 2: Run test, verify failure**

Run:
```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~EvaluationMetricsCalculatorTests" -v normal
```
Expected: compile errors.

- [ ] **Step 3: Create QueryResult input record**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/QueryResult.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Services;

/// <summary>
/// Single goldset query execution result feeding the metrics calculator.
/// </summary>
/// <param name="QueryId">Identifier of the goldset Q&A pair</param>
/// <param name="RelevantHits">Boolean array marking top-N retrieved chunks; index 0 = rank 1.</param>
public sealed record QueryResult(string QueryId, IReadOnlyList<bool> RelevantHits);
```

- [ ] **Step 4: Create IEvaluationMetricsCalculator + impl**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IEvaluationMetricsCalculator.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Domain.Evaluation;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IEvaluationMetricsCalculator
{
    PrecisionAndRanking Compute(IReadOnlyList<QueryResult> queryResults);
}

public sealed record PrecisionAndRanking(double At1, double At3, double At5, double Mrr);
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/EvaluationMetricsCalculator.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Services;

public sealed class EvaluationMetricsCalculator : IEvaluationMetricsCalculator
{
    public PrecisionAndRanking Compute(IReadOnlyList<QueryResult> queryResults)
    {
        ArgumentNullException.ThrowIfNull(queryResults);
        if (queryResults.Count == 0)
        {
            return new PrecisionAndRanking(0, 0, 0, 0);
        }

        double sumP1 = 0, sumP3 = 0, sumP5 = 0, sumRr = 0;

        foreach (var q in queryResults)
        {
            sumP1 += PrecisionAt(q.RelevantHits, 1);
            sumP3 += PrecisionAt(q.RelevantHits, 3);
            sumP5 += PrecisionAt(q.RelevantHits, 5);
            sumRr += ReciprocalRank(q.RelevantHits);
        }

        var n = queryResults.Count;
        return new PrecisionAndRanking(
            At1: sumP1 / n,
            At3: sumP3 / n,
            At5: sumP5 / n,
            Mrr: sumRr / n);
    }

    private static double PrecisionAt(IReadOnlyList<bool> hits, int k)
    {
        if (hits.Count == 0) return 0;
        var bound = Math.Min(k, hits.Count);
        var relevant = 0;
        for (var i = 0; i < bound; i++)
        {
            if (hits[i]) relevant++;
        }
        return (double)relevant / k;
    }

    private static double ReciprocalRank(IReadOnlyList<bool> hits)
    {
        for (var i = 0; i < hits.Count; i++)
        {
            if (hits[i]) return 1.0 / (i + 1);
        }
        return 0;
    }
}
```

- [ ] **Step 5: Run tests + commit**

Run:
```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~EvaluationMetricsCalculatorTests" -v normal
```
Expected: 4 tests passing.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/ tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/EvaluationMetricsCalculatorTests.cs
git commit -m "feat(kb-quality): #1675 EvaluationMetricsCalculator (precision@K + MRR)"
```

---

## Phase C — Application Services + Configuration (Tasks 7-10)

### Task 7: `EvalQualityOptions` config + `QualityBandResolver`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Configuration/EvalQualityOptions.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Configuration/QualityBandsConfig.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IQualityBandResolver.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/QualityBandResolver.cs`
- Modify: `apps/api/src/Api/appsettings.json` (add `EvalQuality` section)
- Test: `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/QualityBandResolverTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/QualityBandResolverTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Application;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class QualityBandResolverTests
{
    private static QualityBandResolver Build(QualityBandsConfig bands)
    {
        var options = Options.Create(new EvalQualityOptions { QualityBands = bands });
        var monitor = new TestOptionsMonitor<EvalQualityOptions>(options.Value);
        return new QualityBandResolver(monitor);
    }

    private static QualityBandsConfig DefaultBands() => new()
    {
        PrecisionAt5 = new BandThreshold { RedMax = 0.40, YellowMax = 0.70 },
        Mrr = new BandThreshold { RedMax = 0.30, YellowMax = 0.60 },
        LatencyP95Ms = new BandThreshold { GreenMax = 30_000, YellowMax = 60_000, InvertedSeverity = true },
    };

    [Theory]
    [InlineData(0.39, QualityBand.Red)]
    [InlineData(0.40, QualityBand.Yellow)]   // right-exclusive boundary
    [InlineData(0.69, QualityBand.Yellow)]
    [InlineData(0.70, QualityBand.Green)]
    [InlineData(0.95, QualityBand.Green)]
    public void Resolve_PrecisionAt5_AppliesRightExclusiveIntervals(double value, QualityBand expected)
    {
        var sut = Build(DefaultBands());
        var metrics = TestMetrics(precisionAt5: value, mrr: 1.0, latencyP95Ms: 0);

        sut.Resolve(metrics).Should().Be(expected);
    }

    [Fact]
    public void Resolve_LatencyAboveYellow_ReturnsRed()
    {
        var sut = Build(DefaultBands());
        var metrics = TestMetrics(precisionAt5: 1.0, mrr: 1.0, latencyP95Ms: 60_001);

        sut.Resolve(metrics).Should().Be(QualityBand.Red);
    }

    [Fact]
    public void Resolve_OverallBand_TakesMaxSeverity()
    {
        var sut = Build(DefaultBands());
        // precision green, mrr red, latency green → overall red
        var metrics = TestMetrics(precisionAt5: 0.9, mrr: 0.1, latencyP95Ms: 10);

        sut.Resolve(metrics).Should().Be(QualityBand.Red);
    }

    private static EvaluationMetrics TestMetrics(double precisionAt5, double mrr, double latencyP95Ms) => new(
        Precision: new PrecisionMetrics(0, 0, precisionAt5),
        Ranking: new RankingMetrics(mrr),
        Latency: new LatencyMetrics(TimeSpan.Zero, TimeSpan.FromMilliseconds(latencyP95Ms)),
        QueryCount: 10,
        CostUsd: 0.01m,
        QualityBand: QualityBand.Green);  // ignored — resolver computes it
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
```

- [ ] **Step 2: Run test, verify failure**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~QualityBandResolverTests" -v normal
```
Expected: compile errors.

- [ ] **Step 3: Create config records**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Configuration/QualityBandsConfig.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Configuration;

public sealed class QualityBandsConfig
{
    public BandThreshold PrecisionAt5 { get; set; } = new();
    public BandThreshold Mrr { get; set; } = new();
    public BandThreshold LatencyP95Ms { get; set; } = new();
}

public sealed class BandThreshold
{
    /// <summary>
    /// For severity-DIRECT metrics (precision/MRR): values strictly less than RedMax → Red.
    /// Right-exclusive: e.g. 0.40 falls in Yellow band, not Red.
    /// </summary>
    public double RedMax { get; set; }

    /// <summary>
    /// For severity-DIRECT metrics: values in [RedMax, YellowMax) → Yellow. Above → Green.
    /// </summary>
    public double YellowMax { get; set; }

    /// <summary>
    /// For inverted-severity metrics (latency): values strictly less than GreenMax → Green.
    /// </summary>
    public double GreenMax { get; set; }

    /// <summary>
    /// When true: severity is inverted (lower = better). Defaults false.
    /// </summary>
    public bool InvertedSeverity { get; set; }
}
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Configuration/EvalQualityOptions.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Configuration;

public sealed class EvalQualityOptions
{
    public const string SectionName = "EvalQuality";

    public decimal MonthlyCostCap { get; set; } = 50.00m;
    public int RateLimitPerDocMinutes { get; set; } = 10;
    public int RetentionMonths { get; set; } = 18;
    public QualityBandsConfig QualityBands { get; set; } = new();
}
```

- [ ] **Step 4: Create IQualityBandResolver + impl**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IQualityBandResolver.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Domain.Evaluation;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IQualityBandResolver
{
    QualityBand Resolve(EvaluationMetrics metrics);
}
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/QualityBandResolver.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public sealed class QualityBandResolver(IOptionsMonitor<EvalQualityOptions> options) : IQualityBandResolver
{
    public QualityBand Resolve(EvaluationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        var bands = options.CurrentValue.QualityBands;

        var p5Band = Direct(metrics.Precision.At5, bands.PrecisionAt5);
        var mrrBand = Direct(metrics.Ranking.Mrr, bands.Mrr);
        var latencyBand = Inverted(metrics.Latency.P95.TotalMilliseconds, bands.LatencyP95Ms);

        // Overall = worst severity across all metrics
        var bandsList = new[] { p5Band, mrrBand, latencyBand };
        if (bandsList.Contains(QualityBand.Red)) return QualityBand.Red;
        if (bandsList.Contains(QualityBand.Yellow)) return QualityBand.Yellow;
        return QualityBand.Green;
    }

    private static QualityBand Direct(double value, BandThreshold t)
    {
        // Right-exclusive: red = [0, RedMax), yellow = [RedMax, YellowMax), green = [YellowMax, ∞)
        if (value < t.RedMax) return QualityBand.Red;
        if (value < t.YellowMax) return QualityBand.Yellow;
        return QualityBand.Green;
    }

    private static QualityBand Inverted(double value, BandThreshold t)
    {
        // Right-exclusive inverted: green = [0, GreenMax), yellow = [GreenMax, YellowMax), red = [YellowMax, ∞)
        if (value < t.GreenMax) return QualityBand.Green;
        if (value < t.YellowMax) return QualityBand.Yellow;
        return QualityBand.Red;
    }
}
```

- [ ] **Step 5: Add `EvalQuality` section to appsettings.json**

Modify `apps/api/src/Api/appsettings.json` — add at root level (sibling of existing sections):

```json
"EvalQuality": {
  "MonthlyCostCap": 50.00,
  "RateLimitPerDocMinutes": 10,
  "RetentionMonths": 18,
  "QualityBands": {
    "PrecisionAt5": { "RedMax": 0.40, "YellowMax": 0.70 },
    "Mrr":         { "RedMax": 0.30, "YellowMax": 0.60 },
    "LatencyP95Ms": { "GreenMax": 30000, "YellowMax": 60000, "InvertedSeverity": true }
  }
}
```

Modify `apps/api/src/Api/appsettings.Development.json` — override:

```json
"EvalQuality": {
  "MonthlyCostCap": 5.00
}
```

- [ ] **Step 6: Wire options in KbQualityModule.cs**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs` — replace the placeholder with:

```csharp
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.KbQuality;

public static class KbQualityModule
{
    public static IServiceCollection AddKbQualityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EvalQualityOptions>(configuration.GetSection(EvalQualityOptions.SectionName));

        services.AddSingleton<IEvaluationMetricsCalculator, EvaluationMetricsCalculator>();
        services.AddSingleton<IQualityBandResolver, QualityBandResolver>();

        return services;
    }
}
```

- [ ] **Step 7: Run tests + commit**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~QualityBandResolverTests" -v normal
```
Expected: 7 tests passing (5 theory + 2 facts).

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/ apps/api/src/Api/appsettings.json apps/api/src/Api/appsettings.Development.json tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/QualityBandResolverTests.cs
git commit -m "feat(kb-quality): #1675 EvalQualityOptions + QualityBandResolver (D-G)"
```

---

### Task 8: Cross-BC ports (interfaces only)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IKbSearchProvider.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IPdfDocumentReadModel.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IEvalCostBudgetChecker.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IAuditLogger.cs`

- [ ] **Step 1: Create IKbSearchProvider**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IKbSearchProvider.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to KnowledgeBase BC: executes a single retrieval query and returns chunk IDs.
/// Implemented by KbSearchProviderAdapter (Task 18) calling SearchQueryHandler.
/// </summary>
public interface IKbSearchProvider
{
    /// <param name="docId">Restrict search to chunks belonging to this PDF.</param>
    /// <param name="question">Natural-language query from the goldset.</param>
    /// <param name="topK">Max chunks to retrieve (typically 10 for P@1/3/5).</param>
    Task<SearchResult> SearchAsync(Guid docId, string question, int topK, CancellationToken ct);
}

public sealed record SearchResult(IReadOnlyList<Guid> RetrievedChunkIds, TimeSpan Elapsed);
```

- [ ] **Step 2: Create IPdfDocumentReadModel**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IPdfDocumentReadModel.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to DocumentProcessing BC: read-only view of PDF doc + its chunks needed for eval.
/// </summary>
public interface IPdfDocumentReadModel
{
    Task<PdfDocSnapshot?> GetSnapshotAsync(Guid docId, CancellationToken ct);
}

public sealed record PdfDocSnapshot(
    Guid Id,
    string FileName,
    int ChunkCount,
    string ProcessingState,
    IReadOnlyList<ChunkSnapshot> TopChunks);

public sealed record ChunkSnapshot(Guid ChunkId, int Position, string Snippet);
```

- [ ] **Step 3: Create IEvalCostBudgetChecker**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IEvalCostBudgetChecker.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to SystemConfiguration BC: per-tenant monthly cost budget tracking (D-H, ADR-051).
/// </summary>
public interface IEvalCostBudgetChecker
{
    Task<decimal> GetRemainingAsync(Guid tenantId, CancellationToken ct);

    Task IncrementSpentAsync(Guid tenantId, decimal amountUsd, CancellationToken ct);
}
```

- [ ] **Step 4: Create IAuditLogger**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IAuditLogger.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to Administration BC: emit auditable events (Level=2 for triggered, Level=1 for completed).
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(string actionName, string entityType, int level, Guid? entityId, object payload, CancellationToken ct);
}
```

- [ ] **Step 5: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: `Build succeeded. 0 Errors.`

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/
git commit -m "feat(kb-quality): #1675 cross-BC ports (search, pdfdoc, costbudget, audit)"
```

---

### Task 9: `IEvaluationCostEstimator` + impl

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IEvaluationCostEstimator.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/EvaluationCostEstimator.cs`
- Test: `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/EvaluationCostEstimatorTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/EvaluationCostEstimatorTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Application;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class EvaluationCostEstimatorTests
{
    private static readonly Guid DocId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task EstimateAsync_BasedOnChunkCount()
    {
        // 30 chunks → top-5 chunks generate goldset, 3 Q&A each = 15 queries
        // Cost model: $0.002 LLM goldset gen per chunk + $0.001 per query exec
        // = 5*0.002 + 15*0.001 = 0.025
        var pdf = new Mock<IPdfDocumentReadModel>();
        pdf.Setup(p => p.GetSnapshotAsync(DocId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new PdfDocSnapshot(DocId, "test.pdf", ChunkCount: 30, "ready", []));

        var sut = new EvaluationCostEstimator(pdf.Object);

        var cost = await sut.EstimateAsync(DocId, CancellationToken.None);

        cost.Should().BeApproximately(0.025m, 0.001m);
    }

    [Fact]
    public async Task EstimateAsync_MissingDoc_ReturnsZero()
    {
        var pdf = new Mock<IPdfDocumentReadModel>();
        pdf.Setup(p => p.GetSnapshotAsync(DocId, It.IsAny<CancellationToken>()))
           .ReturnsAsync((PdfDocSnapshot?)null);

        var sut = new EvaluationCostEstimator(pdf.Object);

        var cost = await sut.EstimateAsync(DocId, CancellationToken.None);

        cost.Should().Be(0m);
    }
}
```

- [ ] **Step 2: Create IEvaluationCostEstimator + impl**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IEvaluationCostEstimator.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IEvaluationCostEstimator
{
    Task<decimal> EstimateAsync(Guid docId, CancellationToken ct);
}
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/EvaluationCostEstimator.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Ports;

namespace Api.BoundedContexts.KbQuality.Application.Services;

/// <summary>
/// Pre-flight cost estimator for a per-doc eval run (D-H).
/// Cost model: $0.002 per chunk goldset generation (top-5 chunks) + $0.001 per query execution
/// (3 queries per top chunk = 15 queries). Final ~$0.025/eval for a 30-chunk doc.
/// </summary>
public sealed class EvaluationCostEstimator(IPdfDocumentReadModel pdf) : IEvaluationCostEstimator
{
    private const int GoldsetTopChunks = 5;
    private const int QueriesPerChunk = 3;
    private const decimal CostPerChunkUsd = 0.002m;
    private const decimal CostPerQueryUsd = 0.001m;

    public async Task<decimal> EstimateAsync(Guid docId, CancellationToken ct)
    {
        var snapshot = await pdf.GetSnapshotAsync(docId, ct);
        if (snapshot is null) return 0m;

        var effectiveTopChunks = Math.Min(GoldsetTopChunks, snapshot.ChunkCount);
        var goldsetCost = effectiveTopChunks * CostPerChunkUsd;
        var queryCost = effectiveTopChunks * QueriesPerChunk * CostPerQueryUsd;
        return goldsetCost + queryCost;
    }
}
```

- [ ] **Step 3: Register in KbQualityModule + run tests + commit**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs` — add inside `AddKbQualityModule`:

```csharp
services.AddSingleton<IEvaluationCostEstimator, EvaluationCostEstimator>();
```

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~EvaluationCostEstimatorTests" -v normal
```
Expected: 2 tests passing.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/ tests/Api.Tests/BoundedContexts/KbQuality/Unit/Application/EvaluationCostEstimatorTests.cs
git commit -m "feat(kb-quality): #1675 EvaluationCostEstimator"
```

---

### Task 10: `IGoldsetGenerator` + `LlmGoldsetGenerator` impl

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IGoldsetGenerator.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Goldset/GoldsetGenerationResult.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Services/LlmGoldsetGenerator.cs`

> **NOTE**: integration test for actual LLM invocation is deferred to Phase H (Task 22 cold start). Unit testing pure logic only (Q&A pair parsing, seed prompt injection).

- [ ] **Step 1: Create GoldsetGenerationResult**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Domain/Goldset/GoldsetGenerationResult.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Goldset;

public sealed record GoldsetGenerationResult(
    IReadOnlyList<GoldsetQaPair> Pairs,
    decimal CostUsd,
    TimeSpan Elapsed);

public sealed record GoldsetQaPair(
    string Id,
    string Question,
    string ExpectedAnswer,
    Guid SourceChunkId);
```

- [ ] **Step 2: Create IGoldsetGenerator**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IGoldsetGenerator.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Goldset;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IGoldsetGenerator
{
    Task<GoldsetGenerationResult> GenerateAsync(
        PdfDocSnapshot doc,
        long seed,
        CancellationToken ct);
}
```

- [ ] **Step 3: Create LlmGoldsetGenerator (skeleton — full LLM wiring deferred to integration test)**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Services/LlmGoldsetGenerator.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Goldset;
using Api.Services; // ILlmGateway exists in Api.Services namespace
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KbQuality.Infrastructure.Services;

/// <summary>
/// Generates Q&A goldset pairs by prompting the LLM gateway with seed-pinned requests
/// against the top-5 chunks of the document. Format: 3 Q&A per chunk = 15 pairs total.
/// </summary>
public sealed class LlmGoldsetGenerator(
    ILlmGateway llm,
    ILogger<LlmGoldsetGenerator> logger) : IGoldsetGenerator
{
    private const int PairsPerChunk = 3;
    private const int TopChunks = 5;
    private const decimal CostPerChunkUsd = 0.002m;

    public async Task<GoldsetGenerationResult> GenerateAsync(
        PdfDocSnapshot doc,
        long seed,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var sw = Stopwatch.StartNew();
        var pairs = new List<GoldsetQaPair>();
        var totalCost = 0m;

        var chunks = doc.TopChunks.Take(TopChunks).ToList();
        foreach (var chunk in chunks)
        {
            var prompt = BuildPrompt(chunk, PairsPerChunk, seed);
            var response = await llm.CompleteAsync(prompt, seed: seed, ct);

            var chunkPairs = ParsePairs(response, chunk.ChunkId);
            pairs.AddRange(chunkPairs);
            totalCost += CostPerChunkUsd;
        }

        logger.LogInformation("Goldset generated: {PairCount} pairs from {ChunkCount} chunks for doc {DocId}",
            pairs.Count, chunks.Count, doc.Id);

        return new GoldsetGenerationResult(pairs, totalCost, sw.Elapsed);
    }

    private static string BuildPrompt(ChunkSnapshot chunk, int pairsPerChunk, long seed) =>
        $$"""
        You are generating gold-standard Q&A pairs to evaluate retrieval quality.
        Produce exactly {{pairsPerChunk}} self-contained Q&A pairs grounded ONLY in the chunk below.
        Return strict JSON: { "pairs": [{ "question": "...", "answer": "..." }, ...] }.
        Seed for reproducibility: {{seed}}

        Chunk snippet:
        ---
        {{chunk.Snippet}}
        ---
        """;

    private static IEnumerable<GoldsetQaPair> ParsePairs(string llmResponse, Guid chunkId)
    {
        var doc = JsonDocument.Parse(llmResponse);
        var pairs = doc.RootElement.GetProperty("pairs");
        foreach (var pair in pairs.EnumerateArray())
        {
            yield return new GoldsetQaPair(
                Id: Guid.NewGuid().ToString(),
                Question: pair.GetProperty("question").GetString() ?? "",
                ExpectedAnswer: pair.GetProperty("answer").GetString() ?? "",
                SourceChunkId: chunkId);
        }
    }
}
```

- [ ] **Step 4: Register in KbQualityModule**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs` — add:

```csharp
services.AddScoped<IGoldsetGenerator, Infrastructure.Services.LlmGoldsetGenerator>();
```

- [ ] **Step 5: Build verification + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors. If `ILlmGateway` symbol unknown, locate it via:
```bash
grep -r "interface ILlmGateway" apps/api/src/Api/
```
and update the `using` directive in `LlmGoldsetGenerator.cs` accordingly.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 IGoldsetGenerator + LlmGoldsetGenerator skeleton"
```

---

## Phase D — MediatR Pipeline Behaviors (Tasks 11-13)

### Task 11: `EvalCostCapBehavior` (D-H)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Behaviors/EvalCostCapBehavior.cs`
- Create stub: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Commands/StartEvaluation/StartEvaluationCommand.cs` (skeleton; full handler in Task 14)
- Test: covered in integration Phase H (Task 23)

- [ ] **Step 1: Create StartEvaluationCommand skeleton**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Commands/StartEvaluation/StartEvaluationCommand.cs`:

```csharp
using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;

public sealed record StartEvaluationCommand(
    Guid DocId,
    string? GoldsetVersion,
    bool OverrideCostCap) : IRequest<EvaluationStartedResult>;

public sealed record EvaluationStartedResult(
    Guid EvaluationId,
    DateTime LocationCreatedAt,
    int RateLimitRemaining,
    DateTime RateLimitReset,
    decimal CostCapRemaining,
    decimal CostCapEstimate);
```

- [ ] **Step 2: Create EvalCostCapBehavior**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Behaviors/EvalCostCapBehavior.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using Api.Services;  // ICurrentUserService
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KbQuality.Application.Behaviors;

/// <summary>
/// Pre-flight cost cap check (D-H, mirror Mechanic Extractor M1.2 ADR-051).
/// Order: register BEFORE AuditBehavior so cost-capped requests still get audited.
/// </summary>
public sealed class EvalCostCapBehavior<TRequest, TResponse>(
    IEvalCostBudgetChecker budget,
    IEvaluationCostEstimator estimator,
    ICurrentUserService user,
    ILogger<EvalCostCapBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : StartEvaluationCommand
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var estimated = await estimator.EstimateAsync(request.DocId, ct);
        var remaining = await budget.GetRemainingAsync(user.TenantId, ct);

        if (estimated > remaining && !request.OverrideCostCap)
        {
            logger.LogWarning("Cost cap reject: estimated={Estimated} remaining={Remaining} doc={DocId}",
                estimated, remaining, request.DocId);
            throw new CostCapExceededException(estimated, remaining);
        }

        if (request.OverrideCostCap && !user.HasPermission("OverrideEvalCostCap"))
        {
            throw new ForbiddenAccessException("OverrideEvalCostCap permission required");
        }

        if (request.OverrideCostCap)
        {
            logger.LogWarning("Cost cap OVERRIDDEN by admin={AdminId} doc={DocId} estimated={Estimated} remaining={Remaining}",
                user.UserId, request.DocId, estimated, remaining);
        }

        var result = await next();

        // Post-handle: increment actual spent (the handler is expected to have computed it).
        // We re-read the budget via tenant repo on the next request — increment here uses estimate
        // as an upper bound; the handler may correct via direct repo call upon completion.
        await budget.IncrementSpentAsync(user.TenantId, estimated, ct);

        return result;
    }
}

// Local minimal exception — replace with project-wide ForbiddenException if it exists in SharedKernel.
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
}
```

> **NOTE on `ICurrentUserService`**: confirm symbol via `grep -r "interface ICurrentUserService" apps/api/src/Api/`. The interface must expose at least `UserId`, `TenantId`, `HasPermission(string)`. If signature differs, adjust the behavior accordingly.

> **NOTE on `ForbiddenAccessException`**: check if `apps/api/src/Api/SharedKernel/Exceptions/ForbiddenException.cs` already exists. If yes, use it and delete the local stub.

- [ ] **Step 3: Register behavior in KbQualityModule**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs` — add:

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.EvalCostCapBehavior<,>));
```

(adjust the `using MediatR;` directive at top of file.)

- [ ] **Step 4: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 EvalCostCapBehavior (mirror ME M1.2 ADR-051)"
```

---

### Task 12: `EvalRateLimitBehavior`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Behaviors/EvalRateLimitBehavior.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IEvaluationRateLimitStore.cs` (port for query lookup; implementation in Task 17 EvaluationRepository)

- [ ] **Step 1: Create rate-limit query port**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Ports/IEvaluationRateLimitStore.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Sliding-window rate-limit lookup: most recent run started by this admin on this doc.
/// Returns null if no runs within the window.
/// </summary>
public interface IEvaluationRateLimitStore
{
    Task<DateTime?> GetLastStartedAtAsync(Guid docId, Guid adminId, TimeSpan window, CancellationToken ct);
}
```

- [ ] **Step 2: Create EvalRateLimitBehavior**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Behaviors/EvalRateLimitBehavior.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Application.Behaviors;

/// <summary>
/// Sliding-window rate limit: at most 1 eval per (docId, adminId) per
/// `EvalQuality:RateLimitPerDocMinutes` (default 10min). Throws EvalRateLimitedException.
/// </summary>
public sealed class EvalRateLimitBehavior<TRequest, TResponse>(
    IEvaluationRateLimitStore store,
    ICurrentUserService user,
    IOptionsMonitor<EvalQualityOptions> options
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : StartEvaluationCommand
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var window = TimeSpan.FromMinutes(options.CurrentValue.RateLimitPerDocMinutes);
        var lastStart = await store.GetLastStartedAtAsync(request.DocId, user.UserId, window, ct);

        if (lastStart is { } last)
        {
            var elapsed = DateTime.UtcNow - last;
            if (elapsed < window)
            {
                throw new EvalRateLimitedException(window - elapsed);
            }
        }

        return await next();
    }
}
```

- [ ] **Step 3: Register behavior**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs` — add (BEFORE the cost cap behavior so rate limit fires first):

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.EvalRateLimitBehavior<,>));
```

Re-order so rate-limit registration appears BEFORE cost-cap registration.

- [ ] **Step 4: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 EvalRateLimitBehavior (sliding window 10min)"
```

---

### Task 13: `AuditableAction` wiring + payload contract

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Commands/StartEvaluation/StartEvaluationCommand.cs`

- [ ] **Step 1: Add `[AuditableAction]` attribute**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/Application/Commands/StartEvaluation/StartEvaluationCommand.cs` — add the attribute on the command type:

```csharp
using Api.Infrastructure.Auditing;  // verify namespace via grep "AuditableActionAttribute"
using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;

[AuditableAction("DocumentEvaluationTriggered", "Document", Level = 2)]
public sealed record StartEvaluationCommand(
    Guid DocId,
    string? GoldsetVersion,
    bool OverrideCostCap) : IRequest<EvaluationStartedResult>;

public sealed record EvaluationStartedResult(
    Guid EvaluationId,
    DateTime LocationCreatedAt,
    int RateLimitRemaining,
    DateTime RateLimitReset,
    decimal CostCapRemaining,
    decimal CostCapEstimate);
```

> **NOTE**: verify `AuditableActionAttribute` namespace via:
> ```bash
> grep -rn "class AuditableActionAttribute" apps/api/src/Api/
> ```
> Adjust the `using` directive accordingly. Payload extraction is handled by the existing `AuditBehavior` pipeline (do not re-implement).

- [ ] **Step 2: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 audit wiring Level=2 on StartEvaluationCommand"
```

---

## Phase E — Commands & Queries (Tasks 14-16)

### Task 14: `StartEvaluationCommand` validator + handler + `IEvaluationExecutor`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Commands/StartEvaluation/StartEvaluationCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Commands/StartEvaluation/StartEvaluationCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IEvaluationExecutor.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/EvaluationExecutor.cs`

- [ ] **Step 1: Create FluentValidation validator**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Commands/StartEvaluation/StartEvaluationCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;

public sealed class StartEvaluationCommandValidator : AbstractValidator<StartEvaluationCommand>
{
    public StartEvaluationCommandValidator()
    {
        RuleFor(x => x.DocId).NotEmpty();
        RuleFor(x => x.GoldsetVersion)
            .MaximumLength(64)
            .When(x => x.GoldsetVersion is not null);
    }
}
```

- [ ] **Step 2: Create IEvaluationExecutor**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/IEvaluationExecutor.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Domain.Goldset;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IEvaluationExecutor
{
    Task<EvaluationOutcome> ExecuteAsync(
        Guid docId,
        PdfDocSnapshot pdf,
        IReadOnlyList<GoldsetQaPair> goldset,
        long seed,
        CancellationToken ct);
}

public sealed record EvaluationOutcome(EvaluationMetrics Metrics, decimal AdditionalCostUsd);
```

- [ ] **Step 3: Create EvaluationExecutor (orchestrates IKbSearchProvider)**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Services/EvaluationExecutor.cs`:

```csharp
using System.Diagnostics;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Domain.Goldset;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public sealed class EvaluationExecutor(
    IKbSearchProvider search,
    IEvaluationMetricsCalculator calculator,
    IQualityBandResolver bands) : IEvaluationExecutor
{
    private const int TopK = 5;
    private const decimal CostPerQueryUsd = 0.001m;

    public async Task<EvaluationOutcome> ExecuteAsync(
        Guid docId,
        PdfDocSnapshot pdf,
        IReadOnlyList<GoldsetQaPair> goldset,
        long seed,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(goldset);

        var latencies = new List<TimeSpan>();
        var queryResults = new List<QueryResult>();

        foreach (var pair in goldset)
        {
            var sw = Stopwatch.StartNew();
            var result = await search.SearchAsync(docId, pair.Question, TopK, ct);
            sw.Stop();

            latencies.Add(sw.Elapsed);
            var hits = result.RetrievedChunkIds.Select(id => id == pair.SourceChunkId).ToArray();
            queryResults.Add(new QueryResult(pair.Id, hits));
        }

        var ranking = calculator.Compute(queryResults);
        latencies.Sort();
        var (p50, p95) = ComputePercentiles(latencies);

        var metricsForBand = new EvaluationMetrics(
            Precision: new PrecisionMetrics(ranking.At1, ranking.At3, ranking.At5),
            Ranking: new RankingMetrics(ranking.Mrr),
            Latency: new LatencyMetrics(p50, p95),
            QueryCount: queryResults.Count,
            CostUsd: queryResults.Count * CostPerQueryUsd,
            QualityBand: QualityBand.Green);  // placeholder, resolved below

        var band = bands.Resolve(metricsForBand);
        var metrics = metricsForBand with { QualityBand = band };

        return new EvaluationOutcome(metrics, AdditionalCostUsd: queryResults.Count * CostPerQueryUsd);
    }

    private static (TimeSpan P50, TimeSpan P95) ComputePercentiles(IReadOnlyList<TimeSpan> sortedLatencies)
    {
        if (sortedLatencies.Count == 0) return (TimeSpan.Zero, TimeSpan.Zero);
        var p50 = sortedLatencies[(int)(sortedLatencies.Count * 0.5)];
        var p95Idx = Math.Min((int)(sortedLatencies.Count * 0.95), sortedLatencies.Count - 1);
        return (p50, sortedLatencies[p95Idx]);
    }
}
```

- [ ] **Step 4: Create StartEvaluationCommandHandler**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Commands/StartEvaluation/StartEvaluationCommandHandler.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using Api.BoundedContexts.KbQuality.Domain.Goldset;
using Api.BoundedContexts.KbQuality.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.BoundedContexts.KbQuality.Application.Configuration;

namespace Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;

public sealed class StartEvaluationCommandHandler(
    IPdfDocumentReadModel pdfRepo,
    IGoldsetGenerator goldsetGen,
    IEvaluationExecutor executor,
    IEvaluationRepository runRepo,
    ICurrentUserService user,
    IOptionsMonitor<EvalQualityOptions> options,
    ILogger<StartEvaluationCommandHandler> logger
) : IRequestHandler<StartEvaluationCommand, EvaluationStartedResult>
{
    public async Task<EvaluationStartedResult> Handle(
        StartEvaluationCommand request,
        CancellationToken ct)
    {
        // 1. Resolve goldset version
        var requestedVersion = request.GoldsetVersion ?? GoldsetVersion.AutoCurrent.Version;
        if (!GoldsetVersion.TryGet(requestedVersion, out var goldsetVer))
        {
            throw new InvalidGoldsetVersionException(
                requestedVersion,
                GoldsetVersion.Registry.Select(v => v.Version).ToArray());
        }

        // 2. Load doc snapshot
        var pdf = await pdfRepo.GetSnapshotAsync(request.DocId, ct)
            ?? throw new InvalidOperationException($"Doc {request.DocId} not found");

        // 3. Resolve seed: re-use latest within 24h on same (docId, goldsetVersion); else random.
        var existingSeed = await runRepo.GetLatestSeedAsync(
            request.DocId, goldsetVer.Version, TimeSpan.FromHours(24), ct);
        var run = DocumentEvaluationRun.Create(
            request.DocId, goldsetVer.Version, user.UserId, reuseSeed: existingSeed);

        await runRepo.AddAsync(run, ct);
        await runRepo.SaveChangesAsync(ct);

        // 4. Generate goldset (transition GoldsetGenerating)
        run.TransitionTo(EvaluationStatus.GoldsetGenerating);
        await runRepo.SaveChangesAsync(ct);

        var goldset = await goldsetGen.GenerateAsync(pdf, run.GoldsetGenerationSeed, ct);

        // 5. Execute eval (transition Running)
        run.TransitionTo(EvaluationStatus.Running);
        await runRepo.SaveChangesAsync(ct);

        try
        {
            var outcome = await executor.ExecuteAsync(
                request.DocId, pdf, goldset.Pairs, run.GoldsetGenerationSeed, ct);

            var totalCost = goldset.CostUsd + outcome.AdditionalCostUsd;
            var finalMetrics = outcome.Metrics with { CostUsd = totalCost };
            run.MarkCompleted(finalMetrics, totalCost);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Eval failed for doc {DocId}", request.DocId);
            run.MarkFailed(ex.Message);
        }

        await runRepo.SaveChangesAsync(ct);

        return new EvaluationStartedResult(
            EvaluationId: run.Id,
            LocationCreatedAt: run.StartedAt,
            RateLimitRemaining: 0,
            RateLimitReset: DateTime.UtcNow.AddMinutes(options.CurrentValue.RateLimitPerDocMinutes),
            CostCapRemaining: 0m,    // populated by CostCapBehavior decoration (out of band)
            CostCapEstimate: run.CostUsd ?? 0m);
    }
}
```

- [ ] **Step 5: Register handler + executor**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs` — add:

```csharp
services.AddScoped<IEvaluationExecutor, EvaluationExecutor>();
// MediatR auto-discovers handlers via assembly scan; ensure assembly registered (likely already done in Program.cs).
```

- [ ] **Step 6: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors (IEvaluationRepository signature comes from Task 17 — if blocked, stub it as `public interface IEvaluationRepository { Task AddAsync(DocumentEvaluationRun, CancellationToken); Task SaveChangesAsync(CancellationToken); Task<long?> GetLatestSeedAsync(Guid, string, TimeSpan, CancellationToken); }` in `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/IEvaluationRepository.cs`).

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 StartEvaluation command + handler + executor"
```

---

### Task 15: `GetEvaluationQuery` + handler + detail DTO

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Queries/GetEvaluation/{GetEvaluationQuery,GetEvaluationQueryHandler,EvaluationDetailDto}.cs`

- [ ] **Step 1: Create query record + DTO**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Queries/GetEvaluation/GetEvaluationQuery.cs`:

```csharp
using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Queries.GetEvaluation;

public sealed record GetEvaluationQuery(Guid DocId, Guid EvaluationId) : IRequest<EvaluationDetailDto?>;
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Queries/GetEvaluation/EvaluationDetailDto.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Queries.GetEvaluation;

public sealed record EvaluationDetailDto(
    Guid EvaluationId,
    Guid PdfDocumentId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    string GoldsetVersion,
    long GoldsetGenerationSeed,
    EvaluationMetricsDto? Metrics,
    decimal? CostUsd,
    Guid TriggeredByAdminId,
    string? ErrorMessage);

public sealed record EvaluationMetricsDto(
    PrecisionDto Precision,
    RankingDto Ranking,
    LatencyDto Latency,
    int QueryCount,
    decimal CostUsd,
    string QualityBand);

public sealed record PrecisionDto(double At1, double At3, double At5);
public sealed record RankingDto(double Mrr);
public sealed record LatencyDto(int P50Ms, int P95Ms);
```

- [ ] **Step 2: Create handler**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Queries/GetEvaluation/GetEvaluationQueryHandler.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Infrastructure;
using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Queries.GetEvaluation;

public sealed class GetEvaluationQueryHandler(IEvaluationRepository repo)
    : IRequestHandler<GetEvaluationQuery, EvaluationDetailDto?>
{
    public async Task<EvaluationDetailDto?> Handle(GetEvaluationQuery request, CancellationToken ct)
    {
        var run = await repo.GetByIdAsync(request.EvaluationId, ct);
        if (run is null || run.PdfDocumentId != request.DocId) return null;

        return new EvaluationDetailDto(
            EvaluationId: run.Id,
            PdfDocumentId: run.PdfDocumentId,
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            Status: run.Status.ToString(),
            GoldsetVersion: run.GoldsetVersion,
            GoldsetGenerationSeed: run.GoldsetGenerationSeed,
            Metrics: run.Metrics is null
                ? null
                : new EvaluationMetricsDto(
                    Precision: new PrecisionDto(run.Metrics.Precision.At1, run.Metrics.Precision.At3, run.Metrics.Precision.At5),
                    Ranking: new RankingDto(run.Metrics.Ranking.Mrr),
                    Latency: new LatencyDto((int)run.Metrics.Latency.P50.TotalMilliseconds, (int)run.Metrics.Latency.P95.TotalMilliseconds),
                    QueryCount: run.Metrics.QueryCount,
                    CostUsd: run.Metrics.CostUsd,
                    QualityBand: run.Metrics.QualityBand.ToString()),
            CostUsd: run.CostUsd,
            TriggeredByAdminId: run.TriggeredByAdminId,
            ErrorMessage: run.ErrorMessage);
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors (assumes `IEvaluationRepository.GetByIdAsync(Guid, CancellationToken)` exists — add to repo interface in Task 17).

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 GetEvaluationQuery + handler + detail DTO"
```

---

### Task 16: `ListEvaluationsQuery` + paginated handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Application/Queries/ListEvaluations/{ListEvaluationsQuery,ListEvaluationsQueryHandler,EvaluationRunListItemDto,PagedEvaluationsDto}.cs`

- [ ] **Step 1: Create query + DTOs**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Queries/ListEvaluations/ListEvaluationsQuery.cs`:

```csharp
using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Queries.ListEvaluations;

public sealed record ListEvaluationsQuery(
    Guid DocId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedEvaluationsDto>;
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Queries/ListEvaluations/PagedEvaluationsDto.cs`:

```csharp
namespace Api.BoundedContexts.KbQuality.Application.Queries.ListEvaluations;

public sealed record PagedEvaluationsDto(
    IReadOnlyList<EvaluationRunListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record EvaluationRunListItemDto(
    Guid EvaluationId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    string GoldsetVersion,
    double? PrecisionAt5,
    double? Mrr,
    int? LatencyP95Ms,
    decimal? CostUsd,
    string? QualityBand);
```

- [ ] **Step 2: Create handler**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Application/Queries/ListEvaluations/ListEvaluationsQueryHandler.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Infrastructure;
using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Queries.ListEvaluations;

public sealed class ListEvaluationsQueryHandler(IEvaluationRepository repo)
    : IRequestHandler<ListEvaluationsQuery, PagedEvaluationsDto>
{
    public async Task<PagedEvaluationsDto> Handle(ListEvaluationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (runs, total) = await repo.ListByDocAsync(request.DocId, page, pageSize, ct);

        var items = runs.Select(r => new EvaluationRunListItemDto(
            EvaluationId: r.Id,
            StartedAt: r.StartedAt,
            CompletedAt: r.CompletedAt,
            Status: r.Status.ToString(),
            GoldsetVersion: r.GoldsetVersion,
            PrecisionAt5: r.Metrics?.Precision.At5,
            Mrr: r.Metrics?.Ranking.Mrr,
            LatencyP95Ms: r.Metrics is null ? null : (int)r.Metrics.Latency.P95.TotalMilliseconds,
            CostUsd: r.CostUsd,
            QualityBand: r.Metrics?.QualityBand.ToString()
        )).ToList();

        return new PagedEvaluationsDto(items, total, page, pageSize);
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors (assumes `IEvaluationRepository.ListByDocAsync` exists — added in Task 17).

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 ListEvaluationsQuery + paginated handler"
```

---

## Phase F — Infrastructure (Tasks 17-19)

### Task 17: `IEvaluationRepository` + EF Core impl

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/IEvaluationRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/EvaluationRepository.cs`

- [ ] **Step 1: Create IEvaluationRepository**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/IEvaluationRepository.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Domain.Evaluation;

namespace Api.BoundedContexts.KbQuality.Infrastructure;

public interface IEvaluationRepository
{
    Task AddAsync(DocumentEvaluationRun run, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<DocumentEvaluationRun?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<DocumentEvaluationRun> items, int total)> ListByDocAsync(
        Guid docId, int page, int pageSize, CancellationToken ct);
    Task<long?> GetLatestSeedAsync(Guid docId, string goldsetVersion, TimeSpan within, CancellationToken ct);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct);
}
```

- [ ] **Step 2: Create EvaluationRepository**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/EvaluationRepository.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KbQuality.Infrastructure;

public sealed class EvaluationRepository(AppDbContext db) : IEvaluationRepository, IEvaluationRateLimitStore
{
    public async Task AddAsync(DocumentEvaluationRun run, CancellationToken ct)
        => await db.DocumentEvaluationRuns.AddAsync(run, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public Task<DocumentEvaluationRun?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.DocumentEvaluationRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<(IReadOnlyList<DocumentEvaluationRun> items, int total)> ListByDocAsync(
        Guid docId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.DocumentEvaluationRuns
            .AsNoTracking()
            .Where(r => r.PdfDocumentId == docId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<long?> GetLatestSeedAsync(
        Guid docId, string goldsetVersion, TimeSpan within, CancellationToken ct)
    {
        var since = DateTime.UtcNow - within;
        var latest = await db.DocumentEvaluationRuns
            .AsNoTracking()
            .Where(r => r.PdfDocumentId == docId
                     && r.GoldsetVersion == goldsetVersion
                     && r.StartedAt > since)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => (long?)r.GoldsetGenerationSeed)
            .FirstOrDefaultAsync(ct);
        return latest;
    }

    public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct)
        => db.DocumentEvaluationRuns
              .Where(r => r.CompletedAt != null && r.CompletedAt < cutoff)
              .ExecuteDeleteAsync(ct);

    // IEvaluationRateLimitStore
    public Task<DateTime?> GetLastStartedAtAsync(
        Guid docId, Guid adminId, TimeSpan window, CancellationToken ct)
    {
        var since = DateTime.UtcNow - window;
        return db.DocumentEvaluationRuns
            .AsNoTracking()
            .Where(r => r.PdfDocumentId == docId
                     && r.TriggeredByAdminId == adminId
                     && r.StartedAt > since)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => (DateTime?)r.StartedAt)
            .FirstOrDefaultAsync(ct);
    }
}
```

- [ ] **Step 3: Register in KbQualityModule**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs`:

```csharp
services.AddScoped<EvaluationRepository>();
services.AddScoped<IEvaluationRepository>(sp => sp.GetRequiredService<EvaluationRepository>());
services.AddScoped<IEvaluationRateLimitStore>(sp => sp.GetRequiredService<EvaluationRepository>());
```

- [ ] **Step 4: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/
git commit -m "feat(kb-quality): #1675 EvaluationRepository (EF Core + rate-limit store)"
```

---

### Task 18: Cross-BC adapters

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Adapters/KbSearchProviderAdapter.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Adapters/PdfDocumentReadModelAdapter.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Adapters/EvalCostBudgetCheckerAdapter.cs`
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Adapters/AuditLoggerAdapter.cs`

- [ ] **Step 1: Create KbSearchProviderAdapter**

Discover concrete search query: search the codebase for the existing search entry point:
```bash
grep -rn "SearchQuery\|class SearchQueryHandler" apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/
```
Note the exact type names and namespaces.

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Adapters/KbSearchProviderAdapter.cs`:

```csharp
using System.Diagnostics;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.Search;  // verify via grep
using MediatR;

namespace Api.BoundedContexts.KbQuality.Infrastructure.Adapters;

public sealed class KbSearchProviderAdapter(IMediator mediator) : IKbSearchProvider
{
    public async Task<SearchResult> SearchAsync(Guid docId, string question, int topK, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        // Adjust SearchQuery construction to match actual signature in the KB BC.
        var query = new SearchQuery(
            Question: question,
            DocId: docId,
            TopK: topK);
        var result = await mediator.Send(query, ct);

        sw.Stop();
        var chunkIds = result.Hits.Select(h => h.ChunkId).Take(topK).ToList();
        return new SearchResult(chunkIds, sw.Elapsed);
    }
}
```

> **NOTE**: `SearchQuery`/`Hits`/`ChunkId` are placeholders for the actual API surface. Adjust based on grep findings. If KnowledgeBase BC does not yet expose a doc-scoped search, add an overload there OR call the unscoped search and filter post-hoc.

- [ ] **Step 2: Create PdfDocumentReadModelAdapter**

Discover existing PDF read query:
```bash
grep -rn "GetKbDocumentByIdQuery\|GetPdfDocumentQuery" apps/api/src/Api/BoundedContexts/DocumentProcessing/
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Adapters/PdfDocumentReadModelAdapter.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KbQuality.Infrastructure.Adapters;

public sealed class PdfDocumentReadModelAdapter(AppDbContext db) : IPdfDocumentReadModel
{
    private const int TopChunksForGoldset = 5;

    public async Task<PdfDocSnapshot?> GetSnapshotAsync(Guid docId, CancellationToken ct)
    {
        var pdf = await db.Set<PdfDocumentEntity>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == docId, ct);
        if (pdf is null) return null;

        // Discover TextChunkEntity name + DbSet via grep "TextChunkEntity"
        var chunks = await db.Set<TextChunkEntity>().AsNoTracking()
            .Where(c => c.PdfDocumentId == docId)
            .OrderBy(c => c.Position)
            .Take(TopChunksForGoldset)
            .Select(c => new ChunkSnapshot(c.Id, c.Position, c.Snippet))
            .ToListAsync(ct);

        var totalChunks = await db.Set<TextChunkEntity>().CountAsync(c => c.PdfDocumentId == docId, ct);

        return new PdfDocSnapshot(
            Id: pdf.Id,
            FileName: pdf.FileName,
            ChunkCount: totalChunks,
            ProcessingState: pdf.ProcessingState,
            TopChunks: chunks);
    }
}
```

> **NOTE**: `TextChunkEntity` column names (`Position`, `Snippet`) must match actuals. Run `grep -rn "class TextChunkEntity" apps/api/src/Api/Infrastructure/Entities/` to confirm.

- [ ] **Step 3: Create EvalCostBudgetCheckerAdapter**

Discover SystemConfiguration BC key-value store:
```bash
grep -rn "ISystemConfigStore\|class SystemConfigService" apps/api/src/Api/BoundedContexts/SystemConfiguration/
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Adapters/EvalCostBudgetCheckerAdapter.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.SystemConfiguration.Application.Services;  // verify via grep
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Infrastructure.Adapters;

public sealed class EvalCostBudgetCheckerAdapter(
    ISystemConfigStore configStore,
    IOptionsMonitor<EvalQualityOptions> options) : IEvalCostBudgetChecker
{
    private static string SpentKey(Guid tenantId) =>
        $"EvalQuality:Spent.{tenantId}.{DateTime.UtcNow:yyyy-MM}";

    public async Task<decimal> GetRemainingAsync(Guid tenantId, CancellationToken ct)
    {
        var spent = await configStore.GetDecimalAsync(SpentKey(tenantId), defaultValue: 0m, ct);
        var cap = options.CurrentValue.MonthlyCostCap;
        return Math.Max(0m, cap - spent);
    }

    public async Task IncrementSpentAsync(Guid tenantId, decimal amountUsd, CancellationToken ct)
    {
        var key = SpentKey(tenantId);
        var current = await configStore.GetDecimalAsync(key, 0m, ct);
        await configStore.SetDecimalAsync(key, current + amountUsd, ct);
    }
}
```

> **NOTE**: `ISystemConfigStore.GetDecimalAsync`/`SetDecimalAsync` are placeholders. Adjust to actual API; if no decimal getter exists, use the closest matching method (e.g. `GetStringAsync` + `decimal.Parse`).

- [ ] **Step 4: Create AuditLoggerAdapter**

Discover audit service:
```bash
grep -rn "IAuditService\|class AuditWriter" apps/api/src/Api/BoundedContexts/Administration/
```

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/Adapters/AuditLoggerAdapter.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.Services;  // verify via grep
using Api.BoundedContexts.KbQuality.Application.Ports;

namespace Api.BoundedContexts.KbQuality.Infrastructure.Adapters;

public sealed class AuditLoggerAdapter(IAuditService audit) : IAuditLogger
{
    public Task LogAsync(string actionName, string entityType, int level, Guid? entityId, object payload, CancellationToken ct)
        => audit.LogAsync(actionName, entityType, level, entityId, payload, ct);
}
```

> **NOTE**: if the existing `IAuditService.LogAsync` signature differs, prefer matching it 1:1 — do NOT introduce signature drift in the adapter.

- [ ] **Step 5: Register adapters**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs`:

```csharp
services.AddScoped<IKbSearchProvider, Infrastructure.Adapters.KbSearchProviderAdapter>();
services.AddScoped<IPdfDocumentReadModel, Infrastructure.Adapters.PdfDocumentReadModelAdapter>();
services.AddScoped<IEvalCostBudgetChecker, Infrastructure.Adapters.EvalCostBudgetCheckerAdapter>();
services.AddScoped<IAuditLogger, Infrastructure.Adapters.AuditLoggerAdapter>();
```

- [ ] **Step 6: Build + commit**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors (if cross-BC types fail to resolve, run the grep commands above + adjust namespaces/method signatures inline).

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 cross-BC adapters (search, pdfdoc, costbudget, audit)"
```

---

### Task 19: Routing `AdminKbQualityEndpoints`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Routing/AdminKbQualityEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs` (call `MapAdminKbQualityEndpoints`)

- [ ] **Step 1: Create the routing class**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Routing/AdminKbQualityEndpoints.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.BoundedContexts.KbQuality.Application.Queries.GetEvaluation;
using Api.BoundedContexts.KbQuality.Application.Queries.ListEvaluations;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Api.BoundedContexts.KbQuality.Routing;

public static class AdminKbQualityEndpoints
{
    public static IEndpointRouteBuilder MapAdminKbQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/kb/docs/{docId:guid}/evaluations")
            .RequireAuthorization("AdminOrSuperadmin")  // adjust to actual policy name
            .WithTags("Admin KB Quality");

        group.MapPost("", StartEvaluation);
        group.MapGet("", ListEvaluations);
        group.MapGet("{evaluationId:guid}", GetEvaluation);

        return app;
    }

    public sealed record StartEvaluationRequestBody(string? GoldsetVersion, bool OverrideCostCap = false);

    private static async Task<IResult> StartEvaluation(
        Guid docId,
        [FromBody] StartEvaluationRequestBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new StartEvaluationCommand(docId, body.GoldsetVersion, body.OverrideCostCap),
                ct);
            return Results.Accepted(
                uri: $"/api/v1/admin/kb/docs/{docId}/evaluations/{result.EvaluationId}",
                value: result);
        }
        catch (InvalidGoldsetVersionException ex)
        {
            return Results.BadRequest(new
            {
                error = "InvalidGoldsetVersion",
                message = ex.Message,
                requested = ex.RequestedVersion,
                available = ex.AvailableVersions
            });
        }
        catch (CostCapExceededException ex)
        {
            return Results.Json(new
            {
                estimated = ex.EstimatedCostUsd,
                remaining = ex.RemainingBudgetUsd,
                hint = "Set overrideCostCap=true with OverrideEvalCostCap permission"
            }, statusCode: StatusCodes.Status402PaymentRequired);
        }
        catch (EvalRateLimitedException ex)
        {
            var seconds = (int)ex.RetryAfter.TotalSeconds;
            return Results.Json(new
            {
                error = "RateLimited",
                retryAfter = seconds
            }, statusCode: StatusCodes.Status429TooManyRequests);
        }
    }

    private static async Task<IResult> GetEvaluation(
        Guid docId,
        Guid evaluationId,
        IMediator mediator,
        CancellationToken ct)
    {
        var dto = await mediator.Send(new GetEvaluationQuery(docId, evaluationId), ct);
        if (dto is null) return Results.NotFound();

        if (dto.Status is "Pending" or "Running" or "GoldsetGenerating")
            return Results.StatusCode(StatusCodes.Status423Locked);

        return Results.Ok(dto);
    }

    private static async Task<IResult> ListEvaluations(
        Guid docId,
        IMediator mediator,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var dto = await mediator.Send(new ListEvaluationsQuery(docId, page, pageSize), ct);
        return Results.Ok(dto);
    }
}
```

- [ ] **Step 2: Wire endpoints in Program.cs**

Modify `apps/api/src/Api/Program.cs` — find existing `app.Map*Endpoints()` call site and add:

```csharp
app.MapAdminKbQualityEndpoints();
```

- [ ] **Step 3: Build + smoke run**

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors.

(Optional smoke):
```bash
dotnet run --launch-profile http  # start API
# in another terminal:
curl -X GET http://localhost:8080/scalar/v1
# verify "Admin KB Quality" tag visible in Scalar UI under /api/v1/admin/kb/docs/{docId}/evaluations
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/Routing/ apps/api/src/Api/Program.cs
git commit -m "feat(kb-quality): #1675 admin endpoints POST/GET evaluations"
```

---

## Phase G — Background Jobs (Tasks 20-21)

### Task 20: `KbQualityRetentionJob` (18-month deletion)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/BackgroundJobs/KbQualityRetentionJob.cs`

- [ ] **Step 1: Create the IHostedService**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/BackgroundJobs/KbQualityRetentionJob.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily retention sweep at 03:00 UTC. Deletes runs older than
/// `EvalQuality:RetentionMonths` (default 18) that have a CompletedAt timestamp.
/// </summary>
public sealed class KbQualityRetentionJob(
    IServiceProvider services,
    IOptionsMonitor<EvalQualityOptions> options,
    ILogger<KbQualityRetentionJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = ComputeNextRunUtc(DateTime.UtcNow);
            var delay = nextRun - DateTime.UtcNow;
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) { return; }

            await SweepAsync(stoppingToken);
        }
    }

    internal static DateTime ComputeNextRunUtc(DateTime now)
    {
        var today = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0, DateTimeKind.Utc);
        return now < today ? today : today.AddDays(1);
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEvaluationRepository>();
            var months = options.CurrentValue.RetentionMonths;
            var cutoff = DateTime.UtcNow.AddMonths(-months);

            var deleted = await repo.DeleteOlderThanAsync(cutoff, ct);
            logger.LogInformation("KbQuality retention sweep deleted {Count} runs older than {Cutoff:o}",
                deleted, cutoff);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KbQuality retention sweep failed");
        }
    }
}
```

- [ ] **Step 2: Register the hosted service**

Modify `apps/api/src/Api/BoundedContexts/KbQuality/KbQualityModule.cs`:

```csharp
services.AddHostedService<Infrastructure.BackgroundJobs.KbQualityRetentionJob>();
```

- [ ] **Step 3: Add a unit test for `ComputeNextRunUtc`**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Unit/Infrastructure/KbQualityRetentionJobTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Infrastructure.BackgroundJobs;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class KbQualityRetentionJobTests
{
    [Fact]
    public void ComputeNextRunUtc_BeforeThreeAm_ReturnsTodayAtThree()
    {
        var now = new DateTime(2026, 6, 2, 1, 30, 0, DateTimeKind.Utc);

        var next = KbQualityRetentionJob.ComputeNextRunUtc(now);

        next.Should().Be(new DateTime(2026, 6, 2, 3, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ComputeNextRunUtc_AfterThreeAm_ReturnsTomorrowAtThree()
    {
        var now = new DateTime(2026, 6, 2, 5, 0, 0, DateTimeKind.Utc);

        var next = KbQualityRetentionJob.ComputeNextRunUtc(now);

        next.Should().Be(new DateTime(2026, 6, 3, 3, 0, 0, DateTimeKind.Utc));
    }
}
```

- [ ] **Step 4: Run tests + commit**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~KbQualityRetentionJobTests" -v normal
```
Expected: 2 tests passing.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/ tests/Api.Tests/BoundedContexts/KbQuality/Unit/Infrastructure/KbQualityRetentionJobTests.cs
git commit -m "feat(kb-quality): #1675 KbQualityRetentionJob daily 18m sweep"
```

---

### Task 21: `KbQualityCostCapResetJob` (calendar-month boundary)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/BackgroundJobs/KbQualityCostCapResetJob.cs`

- [ ] **Step 1: Create the IHostedService**

Create `apps/api/src/Api/BoundedContexts/KbQuality/Infrastructure/BackgroundJobs/KbQualityCostCapResetJob.cs`:

```csharp
using Api.BoundedContexts.SystemConfiguration.Application.Services;  // verify via grep
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KbQuality.Infrastructure.BackgroundJobs;

/// <summary>
/// Calendar-month boundary cleaner. Runs on the 1st of each month at 00:05 UTC
/// to delete prior-month spent-budget counters (keys `EvalQuality:Spent.{tenant}.{yyyy-MM}`).
/// </summary>
public sealed class KbQualityCostCapResetJob(
    IServiceProvider services,
    ILogger<KbQualityCostCapResetJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = ComputeNextRunUtc(DateTime.UtcNow);
            try
            {
                await Task.Delay(nextRun - DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) { return; }

            await SweepAsync(stoppingToken);
        }
    }

    internal static DateTime ComputeNextRunUtc(DateTime now)
    {
        var thisMonthFirstAtFive = new DateTime(now.Year, now.Month, 1, 0, 5, 0, DateTimeKind.Utc);
        return now < thisMonthFirstAtFive ? thisMonthFirstAtFive : thisMonthFirstAtFive.AddMonths(1);
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISystemConfigStore>();

            // Delete counters whose key matches EvalQuality:Spent.*.<priorMonth> via a prefix scan.
            var prior = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM");
            var deleted = await store.DeleteByKeySuffixAsync($".{prior}", ct);

            logger.LogInformation("KbQuality cost-cap reset deleted {Count} prior-month counters (suffix {Prior})",
                deleted, prior);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KbQuality cost-cap reset failed");
        }
    }
}
```

> **NOTE**: `ISystemConfigStore.DeleteByKeySuffixAsync` is illustrative. If no suffix-delete method exists, list all keys with prefix `EvalQuality:Spent.` then filter and delete sequentially.

- [ ] **Step 2: Register + commit**

Modify `KbQualityModule.cs`:

```csharp
services.AddHostedService<Infrastructure.BackgroundJobs.KbQualityCostCapResetJob>();
```

```bash
cd apps/api/src/Api
dotnet build
```
Expected: 0 errors.

```bash
git add apps/api/src/Api/BoundedContexts/KbQuality/
git commit -m "feat(kb-quality): #1675 KbQualityCostCapResetJob (calendar-month boundary)"
```

---

## Phase H — Integration Tests Backend (Tasks 22-25)

### Task 22: `KbQualityIntegrationFixture` (Testcontainers Postgres + WireMock LLM)

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/KbQuality/Integration/KbQualityIntegrationFixture.cs`

- [ ] **Step 1: Add WireMock.Net package**

```bash
cd tests/Api.Tests
dotnet add package WireMock.Net --version 1.5.49
```

- [ ] **Step 2: Create fixture**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Integration/KbQualityIntegrationFixture.cs`:

```csharp
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WireMock.Server;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Integration;

public sealed class KbQualityIntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = default!;
    public WireMockServer LlmServer { get; private set; } = default!;
    public IServiceProvider ServiceProvider { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();
        await _postgres.StartAsync();

        LlmServer = WireMockServer.Start();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDb"] = _postgres.GetConnectionString(),
                ["LlmGateway:BaseUrl"] = LlmServer.Url,
                ["EvalQuality:MonthlyCostCap"] = "50.00",
                ["EvalQuality:RateLimitPerDocMinutes"] = "10",
                ["EvalQuality:RetentionMonths"] = "18",
                ["EvalQuality:QualityBands:PrecisionAt5:RedMax"] = "0.40",
                ["EvalQuality:QualityBands:PrecisionAt5:YellowMax"] = "0.70",
                ["EvalQuality:QualityBands:Mrr:RedMax"] = "0.30",
                ["EvalQuality:QualityBands:Mrr:YellowMax"] = "0.60",
                ["EvalQuality:QualityBands:LatencyP95Ms:GreenMax"] = "30000",
                ["EvalQuality:QualityBands:LatencyP95Ms:YellowMax"] = "60000",
                ["EvalQuality:QualityBands:LatencyP95Ms:InvertedSeverity"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        // Add full app wiring — replicate Program.cs sub-call here OR reference the same registration extensions.
        Api.BoundedContexts.KbQuality.KbQualityModule.AddKbQualityModule(services, configuration);
        // TODO add other BC modules required (KnowledgeBase, DocumentProcessing, Administration, SystemConfiguration).

        ServiceProvider = services.BuildServiceProvider();

        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        LlmServer.Stop();
        await _postgres.DisposeAsync();
    }

    public void ResetLlmStubs()
    {
        LlmServer.Reset();
    }

    public void StubGoldsetCompletion(string responseJson)
    {
        LlmServer.Given(WireMock.RequestBuilders.Request.Create()
            .WithPath("/v1/chat/completions")
            .UsingPost())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));
    }
}
```

> **NOTE**: WireMock LLM stub format depends on the LLM gateway impl. Verify path + payload with `grep -rn "/v1/chat/completions\|/v1/completions" apps/api/src/Api/` and adjust the path/method.

- [ ] **Step 3: Commit (no test yet — fixture only)**

```bash
git add tests/Api.Tests/
git commit -m "test(kb-quality): #1675 integration fixture (Testcontainers PG + WireMock LLM)"
```

---

### Task 23: Scenario A (cold start) integration test

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/KbQuality/Integration/StartEvaluationIntegrationTests.cs`

- [ ] **Step 1: Write the Scenario A test**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Integration/StartEvaluationIntegrationTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Infrastructure;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Integration;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "KbQuality")]
[Collection("KbQualityIntegration")]
public sealed class StartEvaluationIntegrationTests
    : IClassFixture<KbQualityIntegrationFixture>, IAsyncLifetime
{
    private readonly KbQualityIntegrationFixture _fixture;
    private Guid _docId;

    public StartEvaluationIntegrationTests(KbQualityIntegrationFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _fixture.ResetLlmStubs();
        _docId = await SeedDocAsync(chunkCount: 30);
        StubGoldsetForChunks(top5: true);
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ScenarioA_ColdStart_TransitionsThroughLifecycle()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new StartEvaluationCommand(_docId, GoldsetVersion: null, OverrideCostCap: false));

        result.EvaluationId.Should().NotBeEmpty();

        var repo = scope.ServiceProvider.GetRequiredService<IEvaluationRepository>();
        var run = await repo.GetByIdAsync(result.EvaluationId, CancellationToken.None);
        run.Should().NotBeNull();
        run!.Status.Should().Be(EvaluationStatus.Completed);
        run.GoldsetGenerationSeed.Should().NotBe(0);
        run.GoldsetVersion.Should().Be("auto-v1");
        run.Metrics.Should().NotBeNull();
        run.Metrics!.QueryCount.Should().Be(15);  // 3 Q&A × top-5 chunks
        run.CostUsd.Should().BeGreaterThan(0);
    }

    private async Task<Guid> SeedDocAsync(int chunkCount)
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready",
        };
        db.Set<PdfDocumentEntity>().Add(pdf);
        for (var i = 0; i < chunkCount; i++)
        {
            db.Set<TextChunkEntity>().Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdf.Id,
                Position = i,
                Snippet = $"Chunk {i} content about Wingspan birds.",
            });
        }
        await db.SaveChangesAsync();
        return pdf.Id;
    }

    private void StubGoldsetForChunks(bool top5)
    {
        var json = """
        {
          "choices": [
            { "message": { "content": "{\"pairs\":[{\"question\":\"q1\",\"answer\":\"a1\"},{\"question\":\"q2\",\"answer\":\"a2\"},{\"question\":\"q3\",\"answer\":\"a3\"}]}" } }
          ]
        }
        """;
        _fixture.StubGoldsetCompletion(json);
    }
}
```

- [ ] **Step 2: Run the test**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~StartEvaluationIntegrationTests.ScenarioA" -v normal
```
Expected: 1 test passing.

If the test fails because the search step is unwired (KnowledgeBase BC search not registered in the fixture), open `KbQualityIntegrationFixture.cs` and add the KnowledgeBase module registration (or a fake `IKbSearchProvider` substitution for this test).

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/
git commit -m "test(kb-quality): #1675 Scenario A cold-start integration test"
```

---

### Task 24: Scenarios B, C, C2, D, E

**Files:**
- Modify: `tests/Api.Tests/BoundedContexts/KbQuality/Integration/StartEvaluationIntegrationTests.cs`

- [ ] **Step 1: Add Scenario B (rate limit)**

Append to `StartEvaluationIntegrationTests.cs`:

```csharp
[Fact]
public async Task ScenarioB_RateLimit_ThrowsWithin10Minutes()
{
    using var scope = _fixture.ServiceProvider.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    await mediator.Send(new StartEvaluationCommand(_docId, null, false));

    var act = async () => await mediator.Send(new StartEvaluationCommand(_docId, null, false));

    var ex = await act.Should().ThrowAsync<Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions.EvalRateLimitedException>();
    ex.Which.RetryAfter.Should().BeGreaterThan(TimeSpan.Zero);
}
```

- [ ] **Step 2: Add Scenario C (cost cap reject) + C2 (override)**

```csharp
[Fact]
public async Task ScenarioC_CostCapExceeded_Throws402Like()
{
    using var scope = _fixture.ServiceProvider.CreateScope();
    var configStore = scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.SystemConfiguration.Application.Services.ISystemConfigStore>();
    var tenantId = await GetCurrentTenantIdAsync(scope);
    await configStore.SetDecimalAsync($"EvalQuality:Spent.{tenantId}.{DateTime.UtcNow:yyyy-MM}", 49.99m, CancellationToken.None);

    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    var act = async () => await mediator.Send(new StartEvaluationCommand(_docId, null, OverrideCostCap: false));

    await act.Should().ThrowAsync<Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions.CostCapExceededException>();
}

[Fact]
public async Task ScenarioC2_OverrideWithPermission_Succeeds()
{
    using var scope = _fixture.ServiceProvider.CreateScope();
    // Caller authority MUST grant OverrideEvalCostCap permission for this test.
    // Replace `ICurrentUserService` registration in fixture w/ test double if needed.
    var configStore = scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.SystemConfiguration.Application.Services.ISystemConfigStore>();
    var tenantId = await GetCurrentTenantIdAsync(scope);
    await configStore.SetDecimalAsync($"EvalQuality:Spent.{tenantId}.{DateTime.UtcNow:yyyy-MM}", 49.99m, CancellationToken.None);

    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    var result = await mediator.Send(new StartEvaluationCommand(_docId, null, OverrideCostCap: true));

    result.EvaluationId.Should().NotBeEmpty();
}

private async Task<Guid> GetCurrentTenantIdAsync(IServiceScope scope)
{
    // Pull from fixture-provided ICurrentUserService test double.
    var user = scope.ServiceProvider.GetRequiredService<Api.Services.ICurrentUserService>();
    return user.TenantId;
}
```

- [ ] **Step 3: Add Scenario D (seed reuse within 24h)**

```csharp
[Fact]
public async Task ScenarioD_SecondRunWithin24h_ReusesSeed()
{
    using var scope = _fixture.ServiceProvider.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    var repo = scope.ServiceProvider.GetRequiredService<IEvaluationRepository>();

    var first = await mediator.Send(new StartEvaluationCommand(_docId, null, false));
    var run1 = await repo.GetByIdAsync(first.EvaluationId, CancellationToken.None);

    // To bypass the rate limit, manually shift StartedAt of run1 11 minutes back
    // (or expose a test-only mutation API — for now write SQL directly).
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var entity = await db.Set<DocumentEvaluationRun>().FirstAsync(r => r.Id == first.EvaluationId);
    db.Entry(entity).Property("StartedAt").CurrentValue = DateTime.UtcNow.AddMinutes(-11);
    await db.SaveChangesAsync();

    var second = await mediator.Send(new StartEvaluationCommand(_docId, null, false));
    var run2 = await repo.GetByIdAsync(second.EvaluationId, CancellationToken.None);

    run2!.GoldsetGenerationSeed.Should().Be(run1!.GoldsetGenerationSeed);
}
```

- [ ] **Step 4: Add Scenario E (invalid goldset version)**

```csharp
[Fact]
public async Task ScenarioE_InvalidGoldsetVersion_Returns400Like()
{
    using var scope = _fixture.ServiceProvider.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    var act = async () => await mediator.Send(new StartEvaluationCommand(_docId, "manual-v1", false));

    var ex = await act.Should().ThrowAsync<Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions.InvalidGoldsetVersionException>();
    ex.Which.RequestedVersion.Should().Be("manual-v1");
    ex.Which.AvailableVersions.Should().Contain("auto-v1");
}
```

- [ ] **Step 5: Run all integration tests**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~StartEvaluationIntegrationTests" -v normal
```
Expected: 5 tests passing (Scenario A from Task 23 + B + C + C2 + D + E from this task = 6 total).

- [ ] **Step 6: Commit**

```bash
git add tests/Api.Tests/
git commit -m "test(kb-quality): #1675 Scenarios B/C/C2/D/E integration tests"
```

---

### Task 25: Audit Level=2 + retention integration tests

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/KbQuality/Integration/AuditIntegrationTests.cs`
- Create: `tests/Api.Tests/BoundedContexts/KbQuality/Integration/RetentionJobIntegrationTests.cs`

- [ ] **Step 1: AuditIntegrationTests**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Integration/AuditIntegrationTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Integration;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "KbQuality")]
public sealed class AuditIntegrationTests : IClassFixture<KbQualityIntegrationFixture>
{
    private readonly KbQualityIntegrationFixture _fixture;
    public AuditIntegrationTests(KbQualityIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task StartEvaluation_EmitsAuditEventLevel2()
    {
        // Discover the audit table/entity via:
        //   grep -rn "AuditEvent\|class AuditLog" apps/api/src/Api/Infrastructure/Entities/
        // Replace `AuditLogEntity` below with the actual type.

        using var scope = _fixture.ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var docId = await SeedDocFor(this, scope);

        await mediator.Send(new StartEvaluationCommand(docId, null, false));

        // Replace `AuditLogEntity` with project's actual audit entity.
        var auditEvents = await db.Set<Api.Infrastructure.Entities.AuditLogEntity>()
            .Where(e => e.ActionName == "DocumentEvaluationTriggered")
            .ToListAsync();

        auditEvents.Should().NotBeEmpty();
        auditEvents.Should().AllSatisfy(e => e.Level.Should().Be(2));
        auditEvents.First().Payload.Should().Contain("GoldsetGenerationSeed");
    }

    private static async Task<Guid> SeedDocFor(AuditIntegrationTests t, IServiceScope scope)
    {
        // Re-use StartEvaluationIntegrationTests.SeedDocAsync via a static helper — for now inline.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pdf = new Api.Infrastructure.Entities.PdfDocumentEntity
        {
            Id = Guid.NewGuid(), FileName = "audit.pdf", FilePath = "/tmp/a.pdf",
            FileSizeBytes = 1024, UploadedByUserId = Guid.NewGuid(), ProcessingState = "Ready"
        };
        db.Set<Api.Infrastructure.Entities.PdfDocumentEntity>().Add(pdf);
        for (var i = 0; i < 30; i++)
        {
            db.Set<Api.Infrastructure.Entities.TextChunkEntity>().Add(new()
            { Id = Guid.NewGuid(), PdfDocumentId = pdf.Id, Position = i, Snippet = $"chunk{i}" });
        }
        await db.SaveChangesAsync();
        return pdf.Id;
    }
}
```

- [ ] **Step 2: RetentionJobIntegrationTests**

Create `tests/Api.Tests/BoundedContexts/KbQuality/Integration/RetentionJobIntegrationTests.cs`:

```csharp
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Infrastructure;
using Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Integration;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "KbQuality")]
public sealed class RetentionJobIntegrationTests : IClassFixture<KbQualityIntegrationFixture>
{
    private readonly KbQualityIntegrationFixture _fixture;
    public RetentionJobIntegrationTests(KbQualityIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesRunsCompletedBeforeCutoff()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var oldRun = DocumentEvaluationRun.Create(Guid.NewGuid(), "auto-v1", Guid.NewGuid(), reuseSeed: 1);
        oldRun.TransitionTo(EvaluationStatus.GoldsetGenerating);
        oldRun.TransitionTo(EvaluationStatus.Running);
        oldRun.MarkCompleted(new EvaluationMetrics(
            new PrecisionMetrics(0.5, 0.5, 0.5),
            new RankingMetrics(0.5),
            new LatencyMetrics(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
            10, 0.02m, QualityBand.Yellow), 0.02m);
        db.Set<DocumentEvaluationRun>().Add(oldRun);
        await db.SaveChangesAsync();

        // Force the CompletedAt to 19 months ago
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE document_evaluation_runs SET completed_at = {DateTime.UtcNow.AddMonths(-19)} WHERE id = {oldRun.Id}");

        var repo = scope.ServiceProvider.GetRequiredService<IEvaluationRepository>();
        var cutoff = DateTime.UtcNow.AddMonths(-18);

        var deleted = await repo.DeleteOlderThanAsync(cutoff, CancellationToken.None);

        deleted.Should().Be(1);
        (await db.Set<DocumentEvaluationRun>().AnyAsync(r => r.Id == oldRun.Id)).Should().BeFalse();
    }
}
```

- [ ] **Step 3: Run + commit**

```bash
cd tests/Api.Tests
dotnet test --filter "FullyQualifiedName~AuditIntegrationTests|FullyQualifiedName~RetentionJobIntegrationTests" -v normal
```
Expected: 2 tests passing.

```bash
git add tests/Api.Tests/BoundedContexts/KbQuality/Integration/
git commit -m "test(kb-quality): #1675 audit Level=2 + retention 18m integration tests"
```

---

## Phase I — Frontend (Tasks 26-30)

### Task 26: Zod schemas + API client

**Files:**
- Create: `apps/web/src/lib/api/schemas/kb-quality.schemas.ts`
- Create: `apps/web/src/lib/api/clients/kbQualityClient.ts`
- Create: `apps/web/src/lib/format/quality-band.ts`

- [ ] **Step 1: Create Zod schemas**

Create `apps/web/src/lib/api/schemas/kb-quality.schemas.ts`:

```typescript
import { z } from 'zod';

export const QualityBandSchema = z.enum(['Red', 'Yellow', 'Green']);
export type QualityBand = z.infer<typeof QualityBandSchema>;

export const EvaluationStatusSchema = z.enum([
  'Pending',
  'GoldsetGenerating',
  'Running',
  'Completed',
  'Failed',
  'RateLimited',
  'CostCapped',
]);
export type EvaluationStatus = z.infer<typeof EvaluationStatusSchema>;

export const EvaluationMetricsDtoSchema = z.object({
  precision: z.object({ at1: z.number(), at3: z.number(), at5: z.number() }),
  ranking: z.object({ mrr: z.number() }),
  latency: z.object({ p50Ms: z.number().int(), p95Ms: z.number().int() }),
  queryCount: z.number().int().nonnegative(),
  costUsd: z.number(),
  qualityBand: QualityBandSchema,
});
export type EvaluationMetricsDto = z.infer<typeof EvaluationMetricsDtoSchema>;

export const EvaluationDetailDtoSchema = z.object({
  evaluationId: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  startedAt: z.string(),
  completedAt: z.string().nullable(),
  status: EvaluationStatusSchema,
  goldsetVersion: z.string(),
  goldsetGenerationSeed: z.number(),  // long → number (precision OK up to 2^53)
  metrics: EvaluationMetricsDtoSchema.nullable(),
  costUsd: z.number().nullable(),
  triggeredByAdminId: z.string().uuid(),
  errorMessage: z.string().nullable(),
});
export type EvaluationDetailDto = z.infer<typeof EvaluationDetailDtoSchema>;

export const EvaluationRunListItemSchema = z.object({
  evaluationId: z.string().uuid(),
  startedAt: z.string(),
  completedAt: z.string().nullable(),
  status: EvaluationStatusSchema,
  goldsetVersion: z.string(),
  precisionAt5: z.number().nullable(),
  mrr: z.number().nullable(),
  latencyP95Ms: z.number().int().nullable(),
  costUsd: z.number().nullable(),
  qualityBand: QualityBandSchema.nullable(),
});
export type EvaluationRunListItem = z.infer<typeof EvaluationRunListItemSchema>;

export const PagedEvaluationsSchema = z.object({
  items: z.array(EvaluationRunListItemSchema),
  totalCount: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});
export type PagedEvaluations = z.infer<typeof PagedEvaluationsSchema>;

export const StartEvaluationRequestSchema = z.object({
  goldsetVersion: z.string().optional(),
  overrideCostCap: z.boolean().optional(),
});
export type StartEvaluationRequest = z.infer<typeof StartEvaluationRequestSchema>;

export const EvaluationStartedResultSchema = z.object({
  evaluationId: z.string().uuid(),
  locationCreatedAt: z.string(),
  rateLimitRemaining: z.number().int(),
  rateLimitReset: z.string(),
  costCapRemaining: z.number(),
  costCapEstimate: z.number(),
});
export type EvaluationStartedResult = z.infer<typeof EvaluationStartedResultSchema>;
```

- [ ] **Step 2: Create API client**

Create `apps/web/src/lib/api/clients/kbQualityClient.ts`:

```typescript
import { fetchJson } from '@/lib/api/http';  // confirm helper exists; otherwise inline a fetch wrapper
import {
  EvaluationDetailDtoSchema,
  EvaluationStartedResultSchema,
  PagedEvaluationsSchema,
  type EvaluationDetailDto,
  type EvaluationStartedResult,
  type PagedEvaluations,
  type StartEvaluationRequest,
} from '@/lib/api/schemas/kb-quality.schemas';

const base = (docId: string) => `/api/v1/admin/kb/docs/${docId}/evaluations`;

export const kbQualityClient = {
  startEvaluation: async (docId: string, body: StartEvaluationRequest): Promise<EvaluationStartedResult> => {
    const raw = await fetchJson(base(docId), { method: 'POST', body });
    return EvaluationStartedResultSchema.parse(raw);
  },
  getEvaluation: async (docId: string, evaluationId: string): Promise<EvaluationDetailDto> => {
    const raw = await fetchJson(`${base(docId)}/${evaluationId}`, { method: 'GET' });
    return EvaluationDetailDtoSchema.parse(raw);
  },
  listEvaluations: async (docId: string, page = 1, pageSize = 20): Promise<PagedEvaluations> => {
    const raw = await fetchJson(`${base(docId)}?page=${page}&pageSize=${pageSize}`, { method: 'GET' });
    return PagedEvaluationsSchema.parse(raw);
  },
};
```

> **NOTE**: confirm `fetchJson` import path via `grep -rn "export.*fetchJson" apps/web/src/lib/`. If absent, use the existing pattern from `kbClient.ts` or similar.

- [ ] **Step 3: Create quality band formatter**

Create `apps/web/src/lib/format/quality-band.ts`:

```typescript
import type { QualityBand } from '@/lib/api/schemas/kb-quality.schemas';

export interface QualityBandStyle {
  label: string;
  icon: string;
  tailwindBg: string;
  tailwindText: string;
  tailwindBorder: string;
}

export function formatQualityBand(band: QualityBand): QualityBandStyle {
  switch (band) {
    case 'Green':
      return {
        label: 'Verde',
        icon: '🟢',
        tailwindBg: 'bg-emerald-500/10',
        tailwindText: 'text-emerald-700 dark:text-emerald-300',
        tailwindBorder: 'border-emerald-500/30',
      };
    case 'Yellow':
      return {
        label: 'Giallo',
        icon: '🟡',
        tailwindBg: 'bg-amber-500/10',
        tailwindText: 'text-amber-700 dark:text-amber-300',
        tailwindBorder: 'border-amber-500/30',
      };
    case 'Red':
      return {
        label: 'Rosso',
        icon: '🔴',
        tailwindBg: 'bg-rose-500/10',
        tailwindText: 'text-rose-700 dark:text-rose-300',
        tailwindBorder: 'border-rose-500/30',
      };
  }
}
```

- [ ] **Step 4: Run typecheck + commit**

```bash
cd apps/web
pnpm typecheck
```
Expected: 0 errors.

```bash
git add apps/web/src/lib/api/schemas/kb-quality.schemas.ts apps/web/src/lib/api/clients/kbQualityClient.ts apps/web/src/lib/format/quality-band.ts
git commit -m "feat(kb-quality): #1675 FE Zod schemas + API client + band formatter"
```

---

### Task 27: TanStack Query hooks

**Files:**
- Create: `apps/web/src/hooks/queries/useStartEvaluation.ts`
- Create: `apps/web/src/hooks/queries/useEvaluation.ts`
- Create: `apps/web/src/hooks/queries/useEvaluationList.ts`

- [ ] **Step 1: Create useStartEvaluation mutation**

Create `apps/web/src/hooks/queries/useStartEvaluation.ts`:

```typescript
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { kbQualityClient } from '@/lib/api/clients/kbQualityClient';
import type { StartEvaluationRequest } from '@/lib/api/schemas/kb-quality.schemas';

export function useStartEvaluation(docId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: StartEvaluationRequest) => kbQualityClient.startEvaluation(docId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['kb-quality', 'list', docId] });
    },
  });
}
```

- [ ] **Step 2: Create useEvaluation query**

Create `apps/web/src/hooks/queries/useEvaluation.ts`:

```typescript
import { useQuery } from '@tanstack/react-query';
import { kbQualityClient } from '@/lib/api/clients/kbQualityClient';

export function useEvaluation(docId: string, evaluationId: string | null) {
  return useQuery({
    queryKey: ['kb-quality', 'detail', docId, evaluationId],
    queryFn: () => {
      if (evaluationId === null) throw new Error('evaluationId required');
      return kbQualityClient.getEvaluation(docId, evaluationId);
    },
    enabled: evaluationId !== null,
    refetchInterval: (q) => {
      const status = q.state.data?.status;
      // poll every 3s while non-terminal
      return status === 'Completed' || status === 'Failed' || status === 'RateLimited' || status === 'CostCapped'
        ? false
        : 3_000;
    },
  });
}
```

- [ ] **Step 3: Create useEvaluationList query**

Create `apps/web/src/hooks/queries/useEvaluationList.ts`:

```typescript
import { useQuery } from '@tanstack/react-query';
import { kbQualityClient } from '@/lib/api/clients/kbQualityClient';

export function useEvaluationList(docId: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: ['kb-quality', 'list', docId, page, pageSize],
    queryFn: () => kbQualityClient.listEvaluations(docId, page, pageSize),
    staleTime: 30_000,
  });
}
```

- [ ] **Step 4: Typecheck + commit**

```bash
cd apps/web
pnpm typecheck
```
Expected: 0 errors.

```bash
git add apps/web/src/hooks/queries/
git commit -m "feat(kb-quality): #1675 FE TanStack Query hooks (start, get, list)"
```

---

### Task 28: `EvaluationTriggerButton` + `QualityBandChip`

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/quality/EvaluationTriggerButton.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/quality/QualityBandChip.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/quality/EvaluationStatusChip.tsx`
- Test: `apps/web/src/components/admin/knowledge-base/explorer/quality/__tests__/EvaluationTriggerButton.test.tsx`
- Test: `apps/web/src/components/admin/knowledge-base/explorer/quality/__tests__/QualityBandChip.test.tsx`

- [ ] **Step 1: Create QualityBandChip**

Create `apps/web/src/components/admin/knowledge-base/explorer/quality/QualityBandChip.tsx`:

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB quality: amber/emerald/rose chip palette (admin convention, DS-13c) */
import { formatQualityBand } from '@/lib/format/quality-band';
import type { QualityBand } from '@/lib/api/schemas/kb-quality.schemas';

export interface QualityBandChipProps {
  readonly band: QualityBand | null;
}

export function QualityBandChip({ band }: QualityBandChipProps): JSX.Element {
  if (band === null) {
    return (
      <span
        data-testid="quality-band-chip-empty"
        className="inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border bg-muted text-muted-foreground border-border"
      >
        —
      </span>
    );
  }
  const style = formatQualityBand(band);
  return (
    <span
      data-testid={`quality-band-chip-${band.toLowerCase()}`}
      className={`inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-semibold rounded-full border ${style.tailwindBg} ${style.tailwindText} ${style.tailwindBorder}`}
    >
      <span aria-hidden="true">{style.icon}</span>
      {style.label}
    </span>
  );
}
```

- [ ] **Step 2: Create EvaluationStatusChip**

Create `apps/web/src/components/admin/knowledge-base/explorer/quality/EvaluationStatusChip.tsx`:

```tsx
/* eslint-disable local/no-hardcoded-color-utility */
import type { EvaluationStatus } from '@/lib/api/schemas/kb-quality.schemas';

const STYLE: Record<EvaluationStatus, { label: string; cls: string }> = {
  Pending: { label: 'In coda', cls: 'bg-amber-500/10 text-amber-700 border-amber-500/30' },
  GoldsetGenerating: { label: 'Generazione goldset', cls: 'bg-amber-500/10 text-amber-700 border-amber-500/30' },
  Running: { label: 'In esecuzione', cls: 'bg-amber-500/10 text-amber-700 border-amber-500/30' },
  Completed: { label: 'Completato', cls: 'bg-emerald-500/10 text-emerald-700 border-emerald-500/30' },
  Failed: { label: 'Fallito', cls: 'bg-rose-500/10 text-rose-700 border-rose-500/30' },
  RateLimited: { label: 'Rate limited', cls: 'bg-rose-500/10 text-rose-700 border-rose-500/30' },
  CostCapped: { label: 'Cost cap', cls: 'bg-rose-500/10 text-rose-700 border-rose-500/30' },
};

export function EvaluationStatusChip({ status }: { status: EvaluationStatus }): JSX.Element {
  const s = STYLE[status];
  return (
    <span className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${s.cls}`}>
      {s.label}
    </span>
  );
}
```

- [ ] **Step 3: Create EvaluationTriggerButton**

Create `apps/web/src/components/admin/knowledge-base/explorer/quality/EvaluationTriggerButton.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { useStartEvaluation } from '@/hooks/queries/useStartEvaluation';

export interface EvaluationTriggerButtonProps {
  readonly docId: string;
  readonly hasOverrideCostCapPermission: boolean;
}

export function EvaluationTriggerButton({ docId, hasOverrideCostCapPermission }: EvaluationTriggerButtonProps): JSX.Element {
  const [override, setOverride] = useState(false);
  const mutation = useStartEvaluation(docId);

  const handleClick = () => {
    mutation.mutate({ overrideCostCap: override });
  };

  return (
    <div className="flex items-center gap-3">
      <button
        type="button"
        onClick={handleClick}
        disabled={mutation.isPending}
        data-testid="eval-trigger-button"
        className="px-3 py-1.5 text-xs font-medium rounded-md border border-border bg-card hover:bg-muted disabled:opacity-60"
      >
        {mutation.isPending ? 'Avvio…' : '🔬 Lancia eval'}
      </button>

      {hasOverrideCostCapPermission && (
        <label className="inline-flex items-center gap-1 text-xs text-muted-foreground">
          <input
            type="checkbox"
            checked={override}
            onChange={(e) => setOverride(e.target.checked)}
            data-testid="eval-override-toggle"
          />
          Override cost cap
        </label>
      )}

      {mutation.isError && (
        <span data-testid="eval-error" className="text-xs text-rose-700 dark:text-rose-300">
          {(mutation.error as Error).message}
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Add unit tests**

Create `apps/web/src/components/admin/knowledge-base/explorer/quality/__tests__/QualityBandChip.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QualityBandChip } from '../QualityBandChip';

describe('QualityBandChip', () => {
  it('renders dash when band is null', () => {
    render(<QualityBandChip band={null} />);
    expect(screen.getByTestId('quality-band-chip-empty')).toBeInTheDocument();
  });

  it.each(['Green', 'Yellow', 'Red'] as const)('renders %s band variant', (band) => {
    render(<QualityBandChip band={band} />);
    expect(screen.getByTestId(`quality-band-chip-${band.toLowerCase()}`)).toBeInTheDocument();
  });
});
```

Create `apps/web/src/components/admin/knowledge-base/explorer/quality/__tests__/EvaluationTriggerButton.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { EvaluationTriggerButton } from '../EvaluationTriggerButton';

vi.mock('@/lib/api/clients/kbQualityClient', () => ({
  kbQualityClient: {
    startEvaluation: vi.fn().mockResolvedValue({
      evaluationId: '11111111-1111-1111-1111-111111111111',
      locationCreatedAt: '2026-06-02T10:00:00Z',
      rateLimitRemaining: 0,
      rateLimitReset: '2026-06-02T10:10:00Z',
      costCapRemaining: 50,
      costCapEstimate: 0.05,
    }),
  },
}));

const renderWithClient = (ui: React.ReactElement) => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
};

describe('EvaluationTriggerButton', () => {
  it('triggers mutation on click', async () => {
    renderWithClient(
      <EvaluationTriggerButton docId="22222222-2222-2222-2222-222222222222" hasOverrideCostCapPermission={false} />,
    );
    fireEvent.click(screen.getByTestId('eval-trigger-button'));
    expect(await screen.findByText(/Avvio…/i)).toBeInTheDocument();
  });

  it('shows override toggle when permission granted', () => {
    renderWithClient(
      <EvaluationTriggerButton docId="22222222-2222-2222-2222-222222222222" hasOverrideCostCapPermission={true} />,
    );
    expect(screen.getByTestId('eval-override-toggle')).toBeInTheDocument();
  });

  it('hides override toggle without permission', () => {
    renderWithClient(
      <EvaluationTriggerButton docId="22222222-2222-2222-2222-222222222222" hasOverrideCostCapPermission={false} />,
    );
    expect(screen.queryByTestId('eval-override-toggle')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 5: Run tests + commit**

```bash
cd apps/web
pnpm test -- src/components/admin/knowledge-base/explorer/quality
```
Expected: 6 tests passing (4 QualityBandChip + 3 EvaluationTriggerButton — actually `it.each` produces 3 entries so 4 total for the chip + 3 for the button = 7).

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/quality/
git commit -m "feat(kb-quality): #1675 FE trigger button + status/band chips"
```

---

### Task 29: `EvaluationHistoryList` + `EvaluationRunDetailPanel`

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/quality/EvaluationHistoryList.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/quality/EvaluationRunDetailPanel.tsx`

- [ ] **Step 1: Create EvaluationHistoryList**

Create `apps/web/src/components/admin/knowledge-base/explorer/quality/EvaluationHistoryList.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { useEvaluationList } from '@/hooks/queries/useEvaluationList';
import { EvaluationStatusChip } from './EvaluationStatusChip';
import { QualityBandChip } from './QualityBandChip';

export interface EvaluationHistoryListProps {
  readonly docId: string;
  readonly onSelect: (evaluationId: string) => void;
}

export function EvaluationHistoryList({ docId, onSelect }: EvaluationHistoryListProps): JSX.Element {
  const [page, setPage] = useState(1);
  const query = useEvaluationList(docId, page, 20);

  if (query.isLoading) {
    return <div className="text-xs text-muted-foreground" data-testid="eval-list-loading">Caricamento…</div>;
  }
  if (query.isError) {
    return <div className="text-xs text-rose-700" data-testid="eval-list-error">Errore: {(query.error as Error).message}</div>;
  }
  if (!query.data || query.data.items.length === 0) {
    return <div className="text-xs text-muted-foreground" data-testid="eval-list-empty">Nessuna eval per questo doc.</div>;
  }

  return (
    <div className="space-y-1">
      <ul className="divide-y divide-border/60">
        {query.data.items.map(item => (
          <li key={item.evaluationId} className="py-2 flex items-center gap-3">
            <button
              type="button"
              onClick={() => onSelect(item.evaluationId)}
              className="flex-1 text-left font-mono text-[11px] hover:underline"
            >
              {new Date(item.startedAt).toLocaleString('it-IT')}
            </button>
            <EvaluationStatusChip status={item.status} />
            <QualityBandChip band={item.qualityBand} />
            <span className="text-[10px] text-muted-foreground font-mono">
              p@5 {item.precisionAt5?.toFixed(2) ?? '—'} · mrr {item.mrr?.toFixed(2) ?? '—'} · ${item.costUsd?.toFixed(3) ?? '—'}
            </span>
          </li>
        ))}
      </ul>
      <div className="flex items-center justify-between text-[10px] text-muted-foreground">
        <span>Pagina {query.data.page} / {Math.ceil(query.data.totalCount / query.data.pageSize)}</span>
        <div className="flex gap-2">
          <button type="button" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>← prev</button>
          <button type="button" disabled={page * query.data.pageSize >= query.data.totalCount} onClick={() => setPage(p => p + 1)}>next →</button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Create EvaluationRunDetailPanel**

Create `apps/web/src/components/admin/knowledge-base/explorer/quality/EvaluationRunDetailPanel.tsx`:

```tsx
'use client';

import { useEvaluation } from '@/hooks/queries/useEvaluation';
import { EvaluationStatusChip } from './EvaluationStatusChip';
import { QualityBandChip } from './QualityBandChip';

export interface EvaluationRunDetailPanelProps {
  readonly docId: string;
  readonly evaluationId: string | null;
}

export function EvaluationRunDetailPanel({ docId, evaluationId }: EvaluationRunDetailPanelProps): JSX.Element {
  const query = useEvaluation(docId, evaluationId);

  if (evaluationId === null) {
    return <div className="text-xs text-muted-foreground p-4" data-testid="eval-detail-empty">Seleziona una run dall&apos;elenco.</div>;
  }
  if (query.isLoading) {
    return <div className="text-xs text-muted-foreground p-4" data-testid="eval-detail-loading">Caricamento…</div>;
  }
  if (query.isError || !query.data) {
    return <div className="text-xs text-rose-700 p-4" data-testid="eval-detail-error">Errore: {(query.error as Error)?.message ?? 'detail unavailable'}</div>;
  }

  const r = query.data;

  return (
    <article className="p-4 space-y-3" data-testid="eval-detail-panel">
      <header className="flex items-center gap-2">
        <h3 className="font-quicksand font-bold text-sm">Run {r.evaluationId.slice(0, 8)}</h3>
        <EvaluationStatusChip status={r.status} />
        {r.metrics && <QualityBandChip band={r.metrics.qualityBand} />}
      </header>

      <dl className="grid grid-cols-3 gap-2 text-[11px]">
        <Stat label="Goldset" value={r.goldsetVersion} />
        <Stat label="Seed" value={r.goldsetGenerationSeed.toString()} />
        <Stat label="Cost" value={r.costUsd !== null ? `$${r.costUsd.toFixed(3)}` : '—'} />
        {r.metrics && (
          <>
            <Stat label="Precision@1" value={r.metrics.precision.at1.toFixed(3)} />
            <Stat label="Precision@3" value={r.metrics.precision.at3.toFixed(3)} />
            <Stat label="Precision@5" value={r.metrics.precision.at5.toFixed(3)} />
            <Stat label="MRR" value={r.metrics.ranking.mrr.toFixed(3)} />
            <Stat label="p50 latency" value={`${r.metrics.latency.p50Ms} ms`} />
            <Stat label="p95 latency" value={`${r.metrics.latency.p95Ms} ms`} />
          </>
        )}
      </dl>

      {r.errorMessage && (
        <div className="text-xs text-rose-700 dark:text-rose-300 border border-rose-500/30 rounded-md p-2">
          {r.errorMessage}
        </div>
      )}
    </article>
  );
}

function Stat({ label, value }: { label: string; value: string }): JSX.Element {
  return (
    <div className="bg-muted/40 border border-border/40 rounded-md px-2 py-1.5">
      <dt className="font-mono text-[9px] uppercase tracking-wider text-muted-foreground">{label}</dt>
      <dd className="font-quicksand text-[13px] font-bold">{value}</dd>
    </div>
  );
}
```

- [ ] **Step 3: Typecheck + commit**

```bash
cd apps/web
pnpm typecheck
```
Expected: 0 errors.

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/quality/
git commit -m "feat(kb-quality): #1675 FE history list + run detail panel"
```

---

### Task 30: Wire into `KbDocDetailPanel` + `KbDocDetailTabs`

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/quality/QualityTabPanel.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`

- [ ] **Step 1: Create QualityTabPanel orchestrator**

Create `apps/web/src/components/admin/knowledge-base/explorer/quality/QualityTabPanel.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { EvaluationHistoryList } from './EvaluationHistoryList';
import { EvaluationRunDetailPanel } from './EvaluationRunDetailPanel';
import { EvaluationTriggerButton } from './EvaluationTriggerButton';

export interface QualityTabPanelProps {
  readonly docId: string;
  readonly hasOverrideCostCapPermission: boolean;
}

export function QualityTabPanel({ docId, hasOverrideCostCapPermission }: QualityTabPanelProps): JSX.Element {
  const [selected, setSelected] = useState<string | null>(null);

  return (
    <div className="grid grid-cols-12 gap-4 p-4">
      <section className="col-span-5 space-y-3">
        <EvaluationTriggerButton docId={docId} hasOverrideCostCapPermission={hasOverrideCostCapPermission} />
        <h3 className="font-quicksand font-semibold text-sm">Storico eval</h3>
        <EvaluationHistoryList docId={docId} onSelect={setSelected} />
      </section>
      <section className="col-span-7 border-l border-border/60 dark:border-zinc-700/60">
        <EvaluationRunDetailPanel docId={docId} evaluationId={selected} />
      </section>
    </div>
  );
}
```

- [ ] **Step 2: Add 'quality' to KbDocDetailTabs**

Modify `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`:

Find the `KbDocTabKey` type and append `'quality'`:
```typescript
export type KbDocTabKey = 'overview' | 'ingestion' | 'used-by' | 'preview' | 'quality';
```

Find the tab rendering map (likely a `const TABS = [...]` array) and add an entry:
```typescript
{ key: 'quality', label: 'Quality', icon: '🔬' }
```

- [ ] **Step 3: Wire QualityTabPanel into KbDocDetailPanel**

Modify `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`:

At the top:
```typescript
import { QualityTabPanel } from './quality/QualityTabPanel';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';  // verify path; provides permissions list
```

Update the `activeTab` resolution to accept `quality`:
```typescript
if (tab === 'quality') return 'quality';
```

Inside the `ready` rendering block, alongside the other tab branches, add:
```tsx
{activeTab === 'quality' && (
  <QualityTabPanel
    docId={doc.id}
    hasOverrideCostCapPermission={useCurrentUser().data?.permissions.includes('OverrideEvalCostCap') ?? false}
  />
)}
```

- [ ] **Step 4: Typecheck + commit**

```bash
cd apps/web
pnpm typecheck
```
Expected: 0 errors.

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/
git commit -m "feat(kb-quality): #1675 FE wire Quality tab into KbDocDetailPanel"
```

---

## Phase J — E2E (Task 31)

### Task 31: Playwright happy-path E2E

**Files:**
- Create: `apps/web/e2e/admin/kb-quality-eval-happy-path.spec.ts`

- [ ] **Step 1: Create the E2E spec**

Create `apps/web/e2e/admin/kb-quality-eval-happy-path.spec.ts`:

```typescript
import { test, expect } from '@playwright/test';

test.describe('Admin KB Quality — happy-path eval trigger', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin/knowledge-base');
    // Adjust selectors based on actual auth/login flow used by other admin e2e tests
  });

  test('admin triggers eval and sees Completed status with metrics', async ({ page }) => {
    // 1. Select a doc from the KB tree (uses an existing test fixture doc)
    await page.getByTestId('kb-tree-doc').first().click();

    // 2. Switch to Quality tab
    await page.getByRole('tab', { name: /Quality/i }).click();

    // 3. Trigger eval
    await page.getByTestId('eval-trigger-button').click();

    // 4. Wait for run to appear in list
    const firstRow = page.getByTestId('eval-detail-panel');
    await expect(firstRow).toBeVisible({ timeout: 90_000 });

    // 5. Verify metrics rendered
    await expect(page.getByText(/Precision@5/i)).toBeVisible();
    await expect(page.getByText(/MRR/i)).toBeVisible();
    await expect(page.getByTestId(/quality-band-chip-(green|yellow|red)/)).toBeVisible();
  });
});
```

- [ ] **Step 2: Run E2E locally**

Backend + frontend MUST be running. Then:
```bash
cd apps/web
pnpm test:e2e -- admin/kb-quality-eval-happy-path.spec.ts
```
Expected: 1 test passing.

> **NOTE**: this test depends on a seeded doc with non-empty chunks AND a wired LLM (DeepSeek or stub). If a CI gating-job runs without an LLM, mark the test `test.skip` in CI-only mode via env guard.

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/admin/kb-quality-eval-happy-path.spec.ts
git commit -m "test(kb-quality): #1675 E2E Playwright happy-path"
```

---

## Phase K — Wrap-up

### Task 32: Push branch + open PR

- [ ] **Step 1: Final build/test sweep**

```bash
cd apps/api/src/Api && dotnet build
cd ../../../../tests/Api.Tests
dotnet test --filter "BoundedContext=KbQuality" -v normal
```
Expected: all KbQuality tests passing.

```bash
cd ../../apps/web
pnpm typecheck && pnpm lint && pnpm test -- src/components/admin/knowledge-base/explorer/quality
```
Expected: 0 typecheck errors, 0 lint errors, all FE tests passing.

- [ ] **Step 2: Push branch**

```bash
git push -u origin feature/issue-1675-per-doc-quality-eval
```

- [ ] **Step 3: Open PR**

```bash
gh pr create --base main-dev --head feature/issue-1675-per-doc-quality-eval \
  --title "feat(kb-quality): #1675 per-doc quality eval (new KbQuality BC)" \
  --body "Implements #1675 per the design spec §3.3 (post brainstorm 2026-06-02). Closes #1675."
```

- [ ] **Step 4: Update issue tracker**

After CI green:
- Comment on #1675 with PR link
- Mark D-F trigger conditions in OQ-5 as "monitoring TBD post-merge"
- Verify spec `2026-06-01-sp5-admin-kb-fu4-spinouts-design.md` §4 ordering (1676→1673→1675→1674) reflects shipped state

---

## Self-Review Checklist (run BEFORE handoff)

**Spec coverage**: every G/W/T scenario A-E + every R-* recommendation + every D-F/G/H/I decision has at least one task implementing it.

| Spec item | Task(s) |
|---|---|
| D-F Fase 1 LLM tooling-only | Tasks 4 (GoldsetVersion), 10 (LlmGoldsetGenerator) |
| D-G Quality bands config | Task 7 (QualityBandResolver + config) |
| D-H Cost cap mirror ME M1.2 | Tasks 9 (estimator), 11 (CostCapBehavior), 21 (reset job) |
| D-I New BC KbQuality | Task 1 (scaffold) + all tasks |
| R-2 GoldsetVersion registry | Task 4 |
| R-4 G/W/T scenarios A-E | Tasks 23-24 |
| R-6 Audit Level=2 | Task 13 + integration Task 25 |
| R-7 Retention 18m | Task 20 + integration Task 25 |
| R-8 Seed pinning | Tasks 3 (aggregate factory) + 14 (re-use lookup) + 24 (Scenario D) |
| R-9 Test pyramid 4+8+1 | Tasks 3, 4, 6, 7, 9, 20 (unit, 6) + Tasks 22-25 (integration, 6) + Task 31 (E2E, 1) — total 13 |
| R-10 BC boundary | Task 1 |
| R-11 Body evolution path | Tasks 11, 14 (`goldsetVersion?` body field) |
| R-12 Metrics decomposition | Task 3 (stub) + Task 5 (full?) — note: in this plan §3.3 metrics shipped in Task 3 stub. Adjust if real impl needed sooner. |
| R-13 RESTful endpoints | Task 19 |
| Sliding rate-limit | Task 12 + integration Scenario B Task 24 |
| Calendar-month cost cap reset | Task 21 |

**Placeholder scan**: no "TBD/TODO/implement later" lines in step bodies (✓ verified inline). The `NOTE:` callouts are *guidance for the executor* about cross-BC API discovery — not unresolved placeholders within the plan's own scope.

**Type consistency**:
- `DocumentEvaluationRun.GoldsetGenerationSeed` is `long` across aggregate (Task 3), repo lookup (Task 17), tests (Task 23), FE schema (Task 26).
- `GoldsetVersion` symbol consistent across Tasks 4, 14, 24 (`Registry`, `TryGet`, `AutoCurrent.Version`).
- `EvaluationMetrics` field names (`Precision`, `Ranking`, `Latency`, `QueryCount`, `CostUsd`, `QualityBand`) consistent across Tasks 3, 14, 15.
- FE `EvaluationStatus` mirror of BE enum names with PascalCase preserved.

---

## Notes for the executor (cross-BC discovery)

The plan references several symbols from other BCs (`ILlmGateway`, `ICurrentUserService`, `IAuditService`, `ISystemConfigStore`, `SearchQuery`, `PdfDocumentEntity`, `TextChunkEntity`, `AuditableActionAttribute`, `AuditLogEntity`). Before writing the adapters/behaviors that touch them, run targeted greps to:

1. Confirm exact namespace
2. Confirm method signatures
3. Adjust `using` directives + call sites

If a symbol doesn't exist OR its surface differs materially, prefer ADDING the missing capability to the source BC (with a single-line follow-up note in the PR description) over silently working around it. Do NOT introduce hidden coupling.









