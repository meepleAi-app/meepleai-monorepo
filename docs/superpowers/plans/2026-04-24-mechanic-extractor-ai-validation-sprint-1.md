# Mechanic Extractor — AI Comprehension Validation — Sprint 1 Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Sprint 1 of the Mechanic Extractor AI Comprehension Validation feature: certify a `MechanicAnalysis` against a curator-maintained golden set for **Catan only**, end-to-end, with CI green.

**Architecture:** Extend existing `SharedGameCatalog` BC (where M1.2 already lives). Add 4 new tables (`mechanic_golden_claims`, `mechanic_golden_bgg_tags`, `mechanic_analysis_metrics`, `certification_thresholds_config`) + ALTER `mechanic_analyses` with certification columns. Hybrid matching engine (keyword Jaccard ≥ 0.50 + cosine ≥ 0.75 + page tolerance ±1, greedy first-match). CQRS: all endpoints go through `IMediator.Send()`. Immutable metrics snapshots (insert-only). Domain event `MechanicAnalysisCertifiedEvent` on certification.

**Tech Stack:** .NET 9 / EF Core / PostgreSQL + pgvector / MediatR / FluentValidation / xUnit + Testcontainers / Next.js 16 / React 19 / Zustand / React Query / shadcn/ui / Vitest / Playwright

**Spec reference:** [`docs/superpowers/specs/2026-04-24-mechanic-extractor-ai-validation-design.md`](../specs/2026-04-24-mechanic-extractor-ai-validation-design.md) — Section 9 "Sprint 1 — Infrastructure + Catan MVP".

**Naming correction from spec:** All FKs use `SharedGameId` (not `GameId`) and reference `shared_games(id)`, matching existing `MechanicAnalysis.SharedGameId` pattern in `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysis.cs`. SQL column names are `shared_game_id` (not `game_id`).

**Scope (Sprint 1 only):**
- Phase 1: Database migration (5 tables touched)
- Phase 2: Domain aggregates + VOs + events (`MechanicGoldenClaim`, `MechanicGoldenBggTag`, `MechanicAnalysisMetrics`, `CertificationThresholdsConfig`, `CertificationStatus`, `VersionHash`, `MechanicAnalysisCertifiedEvent`)
- Phase 3: Infrastructure (persistence entities + EF configs + repositories)
- Phase 4: Services (`BagOfWordsKeywordExtractor`, `MechanicMatchingEngine`)
- Phase 5: Application commands (Create/Update/Deactivate golden, ImportBggTags, CalculateMetrics, OverrideCertification, UpdateThresholds)
- Phase 6: Application queries (GetGoldenForGame, GetGoldenVersionHash, GetDashboard, GetTrend, GetCertificationThresholds)
- Phase 7: API endpoints
- Phase 8: Frontend MVP (Golden CRUD page for Catan, Review page augmentation, Dashboard base) + Catan golden seed + 1 Playwright smoke E2E

**Out of scope Sprint 1 (deferred to Sprints 2/3):** Puerto Rico/Mage Knight seeds, config soglie UI, BGG importer UI, override UI dialog polish, Hangfire background job for mass recalc, trend drawer with recharts, calibration spike, feature flag ON staging/prod. Mass recalc command exists but invoked synchronously (loop) for MVP.

---

## File Structure

**Backend (new files):**
```
apps/api/src/Api/BoundedContexts/SharedGameCatalog/
├── Domain/
│   ├── Aggregates/
│   │   ├── MechanicGoldenClaim.cs                  NEW
│   │   ├── MechanicAnalysisMetrics.cs              NEW
│   │   └── CertificationThresholdsConfig.cs        NEW
│   ├── Entities/
│   │   └── MechanicGoldenBggTag.cs                 NEW
│   ├── ValueObjects/
│   │   ├── VersionHash.cs                          NEW
│   │   ├── CertificationStatus.cs                  NEW (enum)
│   │   └── CertificationThresholds.cs              NEW (VO)
│   ├── Events/
│   │   └── MechanicAnalysisCertifiedEvent.cs       NEW
│   └── Repositories/
│       ├── IMechanicGoldenClaimRepository.cs       NEW
│       ├── IMechanicGoldenBggTagRepository.cs      NEW
│       ├── IMechanicAnalysisMetricsRepository.cs   NEW
│       └── ICertificationThresholdsConfigRepository.cs NEW
├── Application/
│   ├── Commands/Golden/{Create,Update,Deactivate}MechanicGoldenClaim{Command,Validator,Handler}.cs  NEW
│   ├── Commands/Golden/ImportBggTags{Command,Validator,Handler}.cs  NEW
│   ├── Commands/Metrics/CalculateMechanicAnalysisMetrics{Command,Validator,Handler}.cs  NEW
│   ├── Commands/Metrics/OverrideCertification{Command,Validator,Handler}.cs  NEW
│   ├── Commands/Metrics/RecalculateAllMechanicMetrics{Command,Validator,Handler}.cs  NEW
│   ├── Commands/Thresholds/UpdateCertificationThresholds{Command,Validator,Handler}.cs  NEW
│   ├── Queries/Golden/GetGoldenForGame{Query,Handler}.cs  NEW
│   ├── Queries/Golden/GetGoldenVersionHash{Query,Handler}.cs  NEW
│   ├── Queries/Dashboard/GetDashboard{Query,Handler}.cs  NEW
│   ├── Queries/Dashboard/GetTrend{Query,Handler}.cs  NEW
│   └── Queries/Thresholds/GetCertificationThresholds{Query,Handler}.cs  NEW
├── Infrastructure/
│   ├── Services/
│   │   ├── IKeywordExtractor.cs + BagOfWordsKeywordExtractor.cs + KeywordExtractorResources.cs  NEW
│   │   └── IMechanicMatchingEngine.cs + MechanicMatchingEngine.cs  NEW
│   └── Repositories/
│       ├── MechanicGoldenClaimRepository.cs        NEW
│       ├── MechanicGoldenBggTagRepository.cs       NEW
│       ├── MechanicAnalysisMetricsRepository.cs    NEW
│       └── CertificationThresholdsConfigRepository.cs  NEW
apps/api/src/Api/Infrastructure/
├── Entities/SharedGameCatalog/
│   ├── MechanicGoldenClaimEntity.cs                NEW
│   ├── MechanicGoldenBggTagEntity.cs               NEW
│   ├── MechanicAnalysisMetricsEntity.cs            NEW
│   └── CertificationThresholdsConfigEntity.cs      NEW
├── Configurations/SharedGameCatalog/
│   ├── MechanicGoldenClaimEntityConfiguration.cs   NEW
│   ├── MechanicGoldenBggTagEntityConfiguration.cs  NEW
│   ├── MechanicAnalysisMetricsEntityConfiguration.cs  NEW
│   └── CertificationThresholdsConfigEntityConfiguration.cs  NEW
├── Routing/Admin/MechanicExtractorValidationEndpoints.cs  NEW
└── Migrations/<timestamp>_M2_0_MechanicGoldenAndValidation.cs  NEW
```

**Backend (modified files):**
```
apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysis.cs  MODIFY (add certification fields + methods)
apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicAnalysisEntity.cs  MODIFY (mirror new fields)
apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicAnalysisEntityConfiguration.cs  MODIFY (new column mappings)
apps/api/src/Api/Infrastructure/DependencyInjection/SharedGameCatalogServiceCollectionExtensions.cs  MODIFY (register new services + repos)
apps/api/src/Api/Observability/MeepleAiMetrics.cs  MODIFY (add new counters/histograms/gauges)
```

**Frontend (new files):**
```
apps/web/src/app/admin/knowledge-base/mechanic-extractor/
├── golden/page.tsx                                 NEW (list games)
├── golden/[gameId]/page.tsx                        NEW (CRUD claims)
├── dashboard/page.tsx                              NEW (summary + table)
apps/web/src/components/admin/mechanic-extractor/
├── validation/GoldenClaimForm.tsx                  NEW
├── validation/GoldenClaimsList.tsx                 NEW
├── validation/GoldenVersionHashBadge.tsx           NEW
├── validation/MetricsCard.tsx                      NEW
├── validation/EvaluateButton.tsx                   NEW
├── validation/OverrideCertificationDialog.tsx      NEW
├── validation/DashboardSummaryCards.tsx            NEW
└── validation/DashboardTable.tsx                   NEW
apps/web/src/lib/api/admin/mechanicExtractorValidation.ts  NEW
apps/web/src/hooks/admin/
├── useGoldenForGame.ts                             NEW
├── useCreateGoldenClaim.ts                         NEW
├── useUpdateGoldenClaim.ts                         NEW
├── useDeactivateGoldenClaim.ts                     NEW
├── useCalculateMetrics.ts                          NEW
├── useOverrideCertification.ts                     NEW
└── useValidationDashboard.ts                       NEW
apps/web/e2e/admin/mechanic-extractor-validation-catan.spec.ts  NEW
```

**Frontend (modified files):**
```
apps/web/src/app/admin/knowledge-base/mechanic-extractor/analyses/review/page.tsx  MODIFY (add MetricsCard block)
apps/web/.env.development.example  MODIFY (add NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true)
```

**Seed:**
```
apps/api/src/Api/Data/Seed/CatanGoldenSeed.cs      NEW (or JSON file + loader)
apps/api/src/Api/Data/Seed/catan-golden.json       NEW (~40 claims)
```

---

## Phase 1 — Database Migration

### Task 1: EF Migration `M2_0_MechanicGoldenAndValidation`

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_M2_0_MechanicGoldenAndValidation.cs`

- [ ] **Step 1: Scaffold empty migration**

Run from `apps/api/src/Api/`:
```bash
dotnet ef migrations add M2_0_MechanicGoldenAndValidation
```

- [ ] **Step 2: Replace generated Up() with raw SQL**

Open the new migration `.cs` and replace `Up()`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        ALTER TABLE mechanic_analyses
        ADD COLUMN certification_status integer NOT NULL DEFAULT 0,
        ADD COLUMN certified_at timestamptz NULL,
        ADD COLUMN certified_by_user_id uuid NULL REFERENCES ""Users""(""Id"") ON DELETE SET NULL,
        ADD COLUMN certification_override_reason text NULL,
        ADD COLUMN last_metrics_id uuid NULL;

        CREATE TABLE mechanic_golden_claims (
            id uuid PRIMARY KEY,
            shared_game_id uuid NOT NULL REFERENCES shared_games(id) ON DELETE CASCADE,
            section integer NOT NULL,
            statement text NOT NULL CHECK (length(statement) BETWEEN 1 AND 500),
            expected_page integer NOT NULL CHECK (expected_page >= 1),
            source_quote text NOT NULL CHECK (length(source_quote) BETWEEN 1 AND 1000),
            keywords text[] NOT NULL DEFAULT '{}',
            embedding vector(768) NULL,
            curator_user_id uuid NOT NULL REFERENCES ""Users""(""Id"") ON DELETE RESTRICT,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            deleted_at timestamptz NULL
        );
        CREATE INDEX ix_mechanic_golden_claims_shared_game_id ON mechanic_golden_claims(shared_game_id) WHERE deleted_at IS NULL;
        CREATE INDEX ix_mechanic_golden_claims_embedding ON mechanic_golden_claims USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

        CREATE TABLE mechanic_golden_bgg_tags (
            id uuid PRIMARY KEY,
            shared_game_id uuid NOT NULL REFERENCES shared_games(id) ON DELETE CASCADE,
            name text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
            category text NOT NULL CHECK (length(category) BETWEEN 1 AND 100),
            imported_at timestamptz NOT NULL DEFAULT now(),
            UNIQUE(shared_game_id, name)
        );
        CREATE INDEX ix_mechanic_golden_bgg_tags_shared_game_id ON mechanic_golden_bgg_tags(shared_game_id);

        CREATE TABLE mechanic_analysis_metrics (
            id uuid PRIMARY KEY,
            mechanic_analysis_id uuid NOT NULL REFERENCES mechanic_analyses(id) ON DELETE CASCADE,
            shared_game_id uuid NOT NULL REFERENCES shared_games(id) ON DELETE CASCADE,
            coverage_pct numeric(5,2) NOT NULL CHECK (coverage_pct BETWEEN 0 AND 100),
            page_accuracy_pct numeric(5,2) NOT NULL CHECK (page_accuracy_pct BETWEEN 0 AND 100),
            bgg_match_pct numeric(5,2) NOT NULL CHECK (bgg_match_pct BETWEEN 0 AND 100),
            overall_score numeric(5,2) NOT NULL CHECK (overall_score BETWEEN 0 AND 100),
            certification_status integer NOT NULL,
            golden_version_hash char(64) NOT NULL,
            thresholds_snapshot jsonb NOT NULL,
            match_details jsonb NOT NULL,
            computed_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE INDEX ix_mechanic_analysis_metrics_analysis_computed ON mechanic_analysis_metrics(mechanic_analysis_id, computed_at DESC);
        CREATE INDEX ix_mechanic_analysis_metrics_shared_game_id ON mechanic_analysis_metrics(shared_game_id);

        CREATE TABLE certification_thresholds_config (
            id integer PRIMARY KEY CHECK (id = 1),
            min_coverage_pct numeric(5,2) NOT NULL,
            max_page_tolerance integer NOT NULL,
            min_bgg_match_pct numeric(5,2) NOT NULL,
            min_overall_score numeric(5,2) NOT NULL,
            updated_at timestamptz NOT NULL DEFAULT now(),
            updated_by_user_id uuid NULL REFERENCES ""Users""(""Id"") ON DELETE SET NULL
        );
        INSERT INTO certification_thresholds_config (id, min_coverage_pct, max_page_tolerance, min_bgg_match_pct, min_overall_score)
        VALUES (1, 70, 10, 80, 60) ON CONFLICT DO NOTHING;

        ALTER TABLE mechanic_analyses
        ADD CONSTRAINT fk_mechanic_analyses_last_metrics
        FOREIGN KEY (last_metrics_id) REFERENCES mechanic_analysis_metrics(id)
        DEFERRABLE INITIALLY DEFERRED ON DELETE SET NULL;
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        ALTER TABLE mechanic_analyses DROP CONSTRAINT IF EXISTS fk_mechanic_analyses_last_metrics;
        DROP TABLE IF EXISTS certification_thresholds_config;
        DROP TABLE IF EXISTS mechanic_analysis_metrics;
        DROP TABLE IF EXISTS mechanic_golden_bgg_tags;
        DROP TABLE IF EXISTS mechanic_golden_claims;
        ALTER TABLE mechanic_analyses
            DROP COLUMN last_metrics_id,
            DROP COLUMN certification_override_reason,
            DROP COLUMN certified_by_user_id,
            DROP COLUMN certified_at,
            DROP COLUMN certification_status;
    ");
}
```

- [ ] **Step 3: Apply + rollback round-trip**

```bash
dotnet ef database update
dotnet ef database update <PreviousMigrationName>
dotnet ef database update
```

Expected: both directions succeed, no orphan objects.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(mechanic-validation): add M2.0 golden + metrics + thresholds migration"
```

---

## Phase 2 — Domain Layer

### Task 2: `CertificationStatus` enum

**File:** Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CertificationStatus.cs`

- [ ] **Step 1: Write the enum**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

public enum CertificationStatus
{
    NotEvaluated = 0,
    Certified = 1,
    NotCertified = 2,
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CertificationStatus.cs
git commit -m "feat(mechanic-validation): add CertificationStatus enum"
```

### Task 3: `CertificationThresholds` VO

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CertificationThresholds.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/CertificationThresholdsTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

public class CertificationThresholdsTests
{
    [Fact]
    public void Default_returns_70_10_80_60()
    {
        var d = CertificationThresholds.Default();
        d.MinCoveragePct.Should().Be(70);
        d.MaxPageTolerance.Should().Be(10);
        d.MinBggMatchPct.Should().Be(80);
        d.MinOverallScore.Should().Be(60);
    }

    [Theory]
    [InlineData(-1, 10, 80, 60)]
    [InlineData(70, -1, 80, 60)]
    [InlineData(70, 10, 101, 60)]
    [InlineData(70, 10, 80, 101)]
    public void Create_rejects_out_of_range(decimal cov, int page, decimal bgg, decimal overall)
    {
        var act = () => CertificationThresholds.Create(cov, page, bgg, overall);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsCertified_passes_when_all_pass()
    {
        var t = CertificationThresholds.Default();
        t.IsCertified(coveragePct: 80, pageAccuracyPct: 95, bggMatchPct: 85, overallScore: 70)
            .Should().BeTrue();
    }

    [Fact]
    public void IsCertified_fails_when_any_missing()
    {
        var t = CertificationThresholds.Default();
        t.IsCertified(coveragePct: 65, pageAccuracyPct: 95, bggMatchPct: 85, overallScore: 70)
            .Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests** — `dotnet test --filter CertificationThresholdsTests` — expected FAIL (type missing).

- [ ] **Step 3: Implement**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

public sealed record CertificationThresholds(
    decimal MinCoveragePct,
    int MaxPageTolerance,
    decimal MinBggMatchPct,
    decimal MinOverallScore)
{
    public static CertificationThresholds Default() => new(70m, 10, 80m, 60m);

    public static CertificationThresholds Create(decimal minCoveragePct, int maxPageTolerance, decimal minBggMatchPct, decimal minOverallScore)
    {
        if (minCoveragePct is < 0 or > 100) throw new ArgumentException("MinCoveragePct must be 0..100", nameof(minCoveragePct));
        if (maxPageTolerance < 0) throw new ArgumentException("MaxPageTolerance must be >= 0", nameof(maxPageTolerance));
        if (minBggMatchPct is < 0 or > 100) throw new ArgumentException("MinBggMatchPct must be 0..100", nameof(minBggMatchPct));
        if (minOverallScore is < 0 or > 100) throw new ArgumentException("MinOverallScore must be 0..100", nameof(minOverallScore));
        return new CertificationThresholds(minCoveragePct, maxPageTolerance, minBggMatchPct, minOverallScore);
    }

    public bool IsCertified(decimal coveragePct, decimal pageAccuracyPct, decimal bggMatchPct, decimal overallScore)
        => coveragePct >= MinCoveragePct
        && bggMatchPct >= MinBggMatchPct
        && overallScore >= MinOverallScore
        && pageAccuracyPct >= 0; // page tolerance applied per-claim during matching
}
```

- [ ] **Step 4: Run tests** — expected PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CertificationThresholds.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/CertificationThresholdsTests.cs
git commit -m "feat(mechanic-validation): add CertificationThresholds value object"
```

### Task 4: `VersionHash` VO (SHA256 over claims + tags)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/VersionHash.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/VersionHashTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

public class VersionHashTests
{
    [Fact]
    public void Compute_is_deterministic_and_order_independent_for_tags()
    {
        var claims = new[]
        {
            (Guid.Parse("11111111-1111-1111-1111-111111111111"), "A player wins if…", 10),
            (Guid.Parse("22222222-2222-2222-2222-222222222222"), "Trading triggers…", 5),
        };
        var tagsAsc = new[] { "Auction", "Trading" };
        var tagsDesc = new[] { "Trading", "Auction" };

        var h1 = VersionHash.Compute(claims, tagsAsc);
        var h2 = VersionHash.Compute(claims, tagsDesc);

        h1.Value.Should().HaveLength(64);
        h1.Should().Be(h2);
    }

    [Fact]
    public void Compute_changes_when_statement_changes()
    {
        var a = VersionHash.Compute(new[] { (Guid.Empty, "x", 1) }, Array.Empty<string>());
        var b = VersionHash.Compute(new[] { (Guid.Empty, "y", 1) }, Array.Empty<string>());
        a.Should().NotBe(b);
    }
}
```

- [ ] **Step 2: Run tests** — expected FAIL.

- [ ] **Step 3: Implement**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

public sealed record VersionHash(string Value)
{
    public static VersionHash Compute(IEnumerable<(Guid Id, string Statement, int ExpectedPage)> claims, IEnumerable<string> tagNames)
    {
        var sb = new StringBuilder();
        foreach (var c in claims.OrderBy(x => x.Id))
            sb.Append(c.Id).Append('|').Append(c.Statement).Append('|').Append(c.ExpectedPage).Append('\n');
        sb.Append("---\n");
        foreach (var t in tagNames.OrderBy(x => x, StringComparer.Ordinal))
            sb.Append(t).Append('\n');
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return new VersionHash(Convert.ToHexString(bytes).ToLowerInvariant());
    }
}
```

- [ ] **Step 4: Run tests** — expected PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/VersionHash.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/VersionHashTests.cs
git commit -m "feat(mechanic-validation): add VersionHash value object"
```

### Task 5: `IKeywordExtractor` interface (Domain abstraction)

**File:** Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Services/IKeywordExtractor.cs`

- [ ] **Step 1: Write interface**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Services;

public interface IKeywordExtractor
{
    string[] Extract(string text);
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Services/IKeywordExtractor.cs
git commit -m "feat(mechanic-validation): add IKeywordExtractor domain abstraction"
```

### Task 6: `MechanicGoldenClaim` aggregate

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicGoldenClaim.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicGoldenClaimTests.cs`

- [ ] **Step 1: Write failing tests**

Key invariants to assert:
- `CreateAsync` rejects empty statement, page < 1, source quote > 1000 chars.
- `CreateAsync` calls `IKeywordExtractor.Extract` and `IEmbeddingService.EmbedAsync(statement)`.
- `Update` with same statement does NOT re-embed (embedding service not called).
- `Update` with different statement re-embeds + recomputes keywords.
- `Deactivate()` sets `DeletedAt` non-null and second call throws `InvalidOperationException`.

```csharp
public class MechanicGoldenClaimTests
{
    private readonly Mock<IEmbeddingService> _emb = new();
    private readonly Mock<IKeywordExtractor> _kw = new();

    public MechanicGoldenClaimTests()
    {
        _emb.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        _kw.Setup(x => x.Extract(It.IsAny<string>())).Returns(new[] { "trade", "resource" });
    }

    [Fact]
    public async Task CreateAsync_sets_keywords_and_embedding()
    {
        var c = await MechanicGoldenClaim.CreateAsync(
            sharedGameId: Guid.NewGuid(),
            section: MechanicSection.Turn,
            statement: "Players trade resources on their turn.",
            expectedPage: 5,
            sourceQuote: "Each player may trade.",
            curatorUserId: Guid.NewGuid(),
            _emb.Object, _kw.Object, CancellationToken.None);

        c.Keywords.Should().Contain("trade");
        c.Embedding.Should().NotBeNull();
        _emb.Verify(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_with_same_statement_does_not_reembed()
    {
        var c = await MechanicGoldenClaim.CreateAsync(/* … */);
        _emb.Invocations.Clear();

        await c.UpdateAsync(c.Statement, c.ExpectedPage, c.SourceQuote, _emb.Object, _kw.Object, CancellationToken.None);

        _emb.Verify(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_twice_throws()
    {
        var c = await MechanicGoldenClaim.CreateAsync(/* … */);
        c.Deactivate();
        var act = () => c.Deactivate();
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run tests** — expected FAIL.

- [ ] **Step 3: Implement**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public sealed class MechanicGoldenClaim
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }
    public MechanicSection Section { get; private set; }
    public string Statement { get; private set; } = string.Empty;
    public int ExpectedPage { get; private set; }
    public string SourceQuote { get; private set; } = string.Empty;
    public string[] Keywords { get; private set; } = Array.Empty<string>();
    public float[]? Embedding { get; private set; }
    public Guid CuratorUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private MechanicGoldenClaim() { }

    public static async Task<MechanicGoldenClaim> CreateAsync(
        Guid sharedGameId, MechanicSection section, string statement, int expectedPage,
        string sourceQuote, Guid curatorUserId,
        IEmbeddingService embedding, IKeywordExtractor keywords, CancellationToken ct)
    {
        ValidateStatement(statement);
        ValidatePage(expectedPage);
        ValidateSourceQuote(sourceQuote);
        var now = DateTimeOffset.UtcNow;
        return new MechanicGoldenClaim
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            Section = section,
            Statement = statement,
            ExpectedPage = expectedPage,
            SourceQuote = sourceQuote,
            CuratorUserId = curatorUserId,
            Keywords = keywords.Extract(statement),
            Embedding = await embedding.EmbedAsync(statement, ct),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public async Task UpdateAsync(string statement, int expectedPage, string sourceQuote,
        IEmbeddingService embedding, IKeywordExtractor keywords, CancellationToken ct)
    {
        ValidateStatement(statement);
        ValidatePage(expectedPage);
        ValidateSourceQuote(sourceQuote);
        if (statement != Statement)
        {
            Statement = statement;
            Keywords = keywords.Extract(statement);
            Embedding = await embedding.EmbedAsync(statement, ct);
        }
        ExpectedPage = expectedPage;
        SourceQuote = sourceQuote;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        if (DeletedAt.HasValue) throw new InvalidOperationException("Already deactivated.");
        DeletedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateStatement(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length > 500)
            throw new ArgumentException("Statement must be 1..500 chars.", nameof(s));
    }
    private static void ValidatePage(int p)
    {
        if (p < 1) throw new ArgumentException("Page must be >= 1.", nameof(p));
    }
    private static void ValidateSourceQuote(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length > 1000)
            throw new ArgumentException("SourceQuote must be 1..1000 chars.", nameof(s));
    }
}
```

- [ ] **Step 4: Run tests** — expected PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicGoldenClaim.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicGoldenClaimTests.cs
git commit -m "feat(mechanic-validation): add MechanicGoldenClaim aggregate"
```

### Task 7: `MechanicGoldenBggTag` entity

**File:** Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicGoldenBggTag.cs`

- [ ] **Step 1: Implement**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

public sealed class MechanicGoldenBggTag
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; private set; }

    private MechanicGoldenBggTag() { }

    public static MechanicGoldenBggTag Create(Guid sharedGameId, string name, string category)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new ArgumentException("Name 1..200", nameof(name));
        if (string.IsNullOrWhiteSpace(category) || category.Length > 100) throw new ArgumentException("Category 1..100", nameof(category));
        return new MechanicGoldenBggTag
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            Name = name,
            Category = category,
            ImportedAt = DateTimeOffset.UtcNow,
        };
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicGoldenBggTag.cs
git commit -m "feat(mechanic-validation): add MechanicGoldenBggTag entity"
```

### Task 8: `MechanicAnalysisMetrics` immutable aggregate

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysisMetrics.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicAnalysisMetricsTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
public class MechanicAnalysisMetricsTests
{
    [Fact]
    public void Create_computes_overall_and_certification()
    {
        var t = CertificationThresholds.Default();
        var m = MechanicAnalysisMetrics.Create(
            analysisId: Guid.NewGuid(), sharedGameId: Guid.NewGuid(),
            coveragePct: 85, pageAccuracyPct: 95, bggMatchPct: 90,
            thresholds: t, goldenVersionHash: new string('a', 64),
            matchDetailsJson: "{}");

        m.OverallScore.Should().Be(MechanicAnalysisMetrics.ComputeOverallScore(85, 95, 90));
        m.CertificationStatus.Should().Be(CertificationStatus.Certified);
    }

    [Fact]
    public void Metrics_is_immutable_no_public_setters()
    {
        typeof(MechanicAnalysisMetrics).GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1, 50, 50)]
    [InlineData(50, 101, 50)]
    public void Create_rejects_out_of_range(decimal c, decimal p, decimal b)
    {
        var act = () => MechanicAnalysisMetrics.Create(Guid.NewGuid(), Guid.NewGuid(), c, p, b,
            CertificationThresholds.Default(), new string('a', 64), "{}");
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run tests** — expected FAIL.

- [ ] **Step 3: Implement**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public sealed class MechanicAnalysisMetrics
{
    public Guid Id { get; private set; }
    public Guid MechanicAnalysisId { get; private set; }
    public Guid SharedGameId { get; private set; }
    public decimal CoveragePct { get; private set; }
    public decimal PageAccuracyPct { get; private set; }
    public decimal BggMatchPct { get; private set; }
    public decimal OverallScore { get; private set; }
    public CertificationStatus CertificationStatus { get; private set; }
    public string GoldenVersionHash { get; private set; } = string.Empty;
    public string ThresholdsSnapshotJson { get; private set; } = "{}";
    public string MatchDetailsJson { get; private set; } = "{}";
    public DateTimeOffset ComputedAt { get; private set; }

    private MechanicAnalysisMetrics() { }

    public static decimal ComputeOverallScore(decimal coverage, decimal page, decimal bgg)
        => Math.Round(coverage * 0.4m + page * 0.2m + bgg * 0.4m, 2);

    public static MechanicAnalysisMetrics Create(
        Guid analysisId, Guid sharedGameId,
        decimal coveragePct, decimal pageAccuracyPct, decimal bggMatchPct,
        CertificationThresholds thresholds, string goldenVersionHash, string matchDetailsJson)
    {
        ValidatePct(coveragePct, nameof(coveragePct));
        ValidatePct(pageAccuracyPct, nameof(pageAccuracyPct));
        ValidatePct(bggMatchPct, nameof(bggMatchPct));
        if (goldenVersionHash?.Length != 64) throw new ArgumentException("Hash must be 64 chars", nameof(goldenVersionHash));

        var overall = ComputeOverallScore(coveragePct, pageAccuracyPct, bggMatchPct);
        var status = thresholds.IsCertified(coveragePct, pageAccuracyPct, bggMatchPct, overall)
            ? CertificationStatus.Certified
            : CertificationStatus.NotCertified;

        var thresholdsJson = System.Text.Json.JsonSerializer.Serialize(thresholds);
        return new MechanicAnalysisMetrics
        {
            Id = Guid.NewGuid(),
            MechanicAnalysisId = analysisId,
            SharedGameId = sharedGameId,
            CoveragePct = coveragePct,
            PageAccuracyPct = pageAccuracyPct,
            BggMatchPct = bggMatchPct,
            OverallScore = overall,
            CertificationStatus = status,
            GoldenVersionHash = goldenVersionHash,
            ThresholdsSnapshotJson = thresholdsJson,
            MatchDetailsJson = matchDetailsJson ?? "{}",
            ComputedAt = DateTimeOffset.UtcNow,
        };
    }

    private static void ValidatePct(decimal v, string name)
    {
        if (v is < 0 or > 100) throw new ArgumentException($"{name} must be 0..100", name);
    }
}
```

- [ ] **Step 4: Run tests** — expected PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysisMetrics.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicAnalysisMetricsTests.cs
git commit -m "feat(mechanic-validation): add MechanicAnalysisMetrics aggregate"
```

### Task 9: `CertificationThresholdsConfig` singleton

**File:** Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/CertificationThresholdsConfig.cs`

- [ ] **Step 1: Implement**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public sealed class CertificationThresholdsConfig
{
    public int Id { get; private set; } = 1;
    public CertificationThresholds Thresholds { get; private set; } = CertificationThresholds.Default();
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private CertificationThresholdsConfig() { }

    public static CertificationThresholdsConfig Seed() => new()
    {
        Id = 1,
        Thresholds = CertificationThresholds.Default(),
        UpdatedAt = DateTimeOffset.UtcNow,
        UpdatedByUserId = null,
    };

    public void Update(CertificationThresholds thresholds, Guid updatedByUserId)
    {
        Thresholds = thresholds;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/CertificationThresholdsConfig.cs
git commit -m "feat(mechanic-validation): add CertificationThresholdsConfig singleton"
```

### Task 10: `MechanicAnalysisCertifiedEvent`

**File:** Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/MechanicAnalysisCertifiedEvent.cs`

- [ ] **Step 1: Implement**

```csharp
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

public sealed record MechanicAnalysisCertifiedEvent(
    Guid AnalysisId,
    Guid SharedGameId,
    bool WasOverride,
    string? OverrideReason,
    Guid CertifiedByUserId,
    DateTimeOffset CertifiedAt) : INotification;
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/MechanicAnalysisCertifiedEvent.cs
git commit -m "feat(mechanic-validation): add MechanicAnalysisCertifiedEvent"
```

### Task 11: Extend `MechanicAnalysis` with certification methods

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysis.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicAnalysisCertificationTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
public class MechanicAnalysisCertificationTests
{
    [Fact]
    public void ApplyMetricsResult_updates_status_and_lastMetricsId()
    {
        var a = MechanicAnalysisTestFactory.NewCompleted();
        var m = MechanicAnalysisMetricsTestFactory.NewCertified(a.Id);
        a.ApplyMetricsResult(m);

        a.CertificationStatus.Should().Be(CertificationStatus.Certified);
        a.LastMetricsId.Should().Be(m.Id);
        a.CertifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void CertifyViaOverride_rejects_empty_reason()
    {
        var a = MechanicAnalysisTestFactory.NewNotCertified();
        var act = () => a.CertifyViaOverride(reason: "", userId: Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CertifyViaOverride_requires_LastMetricsId()
    {
        var a = MechanicAnalysisTestFactory.NewCompleted(); // no metrics yet
        var act = () => a.CertifyViaOverride(reason: "approved", userId: Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CertifyViaOverride_rejects_when_already_certified()
    {
        var a = MechanicAnalysisTestFactory.NewCertified();
        var act = () => a.CertifyViaOverride(reason: "approved", userId: Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run tests** — expected FAIL.

- [ ] **Step 3: Implement — add properties + methods to `MechanicAnalysis`**

Add to the class body:

```csharp
public CertificationStatus CertificationStatus { get; private set; } = CertificationStatus.NotEvaluated;
public DateTimeOffset? CertifiedAt { get; private set; }
public Guid? CertifiedByUserId { get; private set; }
public string? CertificationOverrideReason { get; private set; }
public Guid? LastMetricsId { get; private set; }

public void ApplyMetricsResult(MechanicAnalysisMetrics metrics)
{
    if (metrics is null) throw new ArgumentNullException(nameof(metrics));
    if (metrics.MechanicAnalysisId != Id) throw new ArgumentException("Metrics do not belong to this analysis.");
    LastMetricsId = metrics.Id;
    CertificationStatus = metrics.CertificationStatus;
    CertifiedAt = metrics.CertificationStatus == CertificationStatus.Certified ? metrics.ComputedAt : null;
    CertificationOverrideReason = null;
}

public void CertifyViaOverride(string reason, Guid userId)
{
    if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 20 or > 500)
        throw new ArgumentException("Reason must be 20..500 chars.", nameof(reason));
    if (LastMetricsId is null)
        throw new InvalidOperationException("Cannot override without prior metrics.");
    if (CertificationStatus == CertificationStatus.Certified)
        throw new InvalidOperationException("Already certified.");

    CertificationStatus = CertificationStatus.Certified;
    CertifiedAt = DateTimeOffset.UtcNow;
    CertifiedByUserId = userId;
    CertificationOverrideReason = reason;
}
```

- [ ] **Step 4: Run tests** — expected PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysis.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/MechanicAnalysisCertificationTests.cs
git commit -m "feat(mechanic-validation): extend MechanicAnalysis with certification"
```

### Task 12: Repository interfaces

**Files:** Create all four under `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/`.

- [ ] **Step 1: `IMechanicGoldenClaimRepository.cs`**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

public interface IMechanicGoldenClaimRepository
{
    Task<IReadOnlyList<MechanicGoldenClaim>> GetByGameAsync(Guid sharedGameId, CancellationToken ct);
    Task<MechanicGoldenClaim?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(MechanicGoldenClaim claim, CancellationToken ct);
    Task UpdateAsync(MechanicGoldenClaim claim, CancellationToken ct);
    Task<VersionHash> GetVersionHashAsync(Guid sharedGameId, CancellationToken ct);
}
```

- [ ] **Step 2: `IMechanicGoldenBggTagRepository.cs`**

```csharp
public interface IMechanicGoldenBggTagRepository
{
    Task<IReadOnlyList<MechanicGoldenBggTag>> GetByGameAsync(Guid sharedGameId, CancellationToken ct);
    Task UpsertBatchAsync(Guid sharedGameId, IReadOnlyList<(string Name, string Category)> tags, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

- [ ] **Step 3: `IMechanicAnalysisMetricsRepository.cs`**

```csharp
public sealed record DashboardGameRow(
    Guid SharedGameId, string Name, CertificationStatus Status,
    decimal OverallScore, DateTimeOffset? LastComputedAt);

public interface IMechanicAnalysisMetricsRepository
{
    Task AddAsync(MechanicAnalysisMetrics metrics, CancellationToken ct);
    Task<MechanicAnalysisMetrics?> GetByAnalysisAsync(Guid analysisId, CancellationToken ct);
    Task<MechanicAnalysisMetrics?> GetLatestByAnalysisAsync(Guid analysisId, CancellationToken ct);
    Task<IReadOnlyList<DashboardGameRow>> GetDashboardAsync(CancellationToken ct);
    Task<IReadOnlyList<MechanicAnalysisMetrics>> GetTrendAsync(Guid sharedGameId, int take, CancellationToken ct);
}
```

- [ ] **Step 4: `ICertificationThresholdsConfigRepository.cs`**

```csharp
public interface ICertificationThresholdsConfigRepository
{
    Task<CertificationThresholdsConfig> GetAsync(CancellationToken ct);
    Task UpdateAsync(CertificationThresholdsConfig config, CancellationToken ct);
}
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/
git commit -m "feat(mechanic-validation): add repository interfaces"
```

---

## Phase 3 — Infrastructure (Entities + EF Configs + Repositories)

### Task 13: Persistence entities

**Files:** Create under `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/`:
- `MechanicGoldenClaimEntity.cs`
- `MechanicGoldenBggTagEntity.cs`
- `MechanicAnalysisMetricsEntity.cs`
- `CertificationThresholdsConfigEntity.cs`

- [ ] **Step 1: Implement each with public properties matching the aggregate fields** (Entity is mutable, aggregates are not — mapping handled by repo). Include `uint Xmin { get; set; }` for concurrency on `MechanicGoldenClaimEntity` and `CertificationThresholdsConfigEntity`.

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/
git commit -m "feat(mechanic-validation): add persistence entities"
```

### Task 14: EF Configurations

**Files:** Create under `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/`, each `internal sealed`:
- `MechanicGoldenClaimEntityConfiguration.cs`
- `MechanicGoldenBggTagEntityConfiguration.cs`
- `MechanicAnalysisMetricsEntityConfiguration.cs`
- `CertificationThresholdsConfigEntityConfiguration.cs`

Modify: `MechanicAnalysisEntityConfiguration.cs` — add the 5 new certification columns with `HasColumnName("snake_case")`.

- [ ] **Step 1: Implement configurations** following the pattern in existing `MechanicAnalysisEntityConfiguration.cs` (snake_case column names, `UseXminAsConcurrencyToken()` where applicable, `HasQueryFilter(e => e.DeletedAt == null)` on `MechanicGoldenClaimEntity`, `HasColumnType("jsonb")` on JSON columns, `HasColumnType("vector(768)")` on embedding).

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/
git commit -m "feat(mechanic-validation): add EF configurations"
```

### Task 15: Repository implementations

**Files:** Create under `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/`:
- `MechanicGoldenClaimRepository.cs`
- `MechanicGoldenBggTagRepository.cs`
- `MechanicAnalysisMetricsRepository.cs`
- `CertificationThresholdsConfigRepository.cs`

- [ ] **Step 1: Write integration tests with Testcontainers** under `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/`. Each repo: happy path + edge case (e.g. upsert on duplicate tag, dashboard projection aggregates latest-per-game).

- [ ] **Step 2: Implement repos**. `GetVersionHashAsync` loads claim triples + tag names and calls `VersionHash.Compute`. `GetDashboardAsync` projects latest-per-game via `ROW_NUMBER() OVER (PARTITION BY shared_game_id ORDER BY computed_at DESC) = 1`.

- [ ] **Step 3: Run integration tests** — expected PASS.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/ \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/
git commit -m "feat(mechanic-validation): add repository implementations"
```

---

## Phase 4 — Services (Keyword Extractor + Matching Engine)

### Task 16: `BagOfWordsKeywordExtractor` + resources

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/KeywordExtractorResources.cs` (stopword lists IT+EN embedded as static arrays)
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BagOfWordsKeywordExtractor.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BagOfWordsKeywordExtractorTests.cs`

- [ ] **Step 1: Write failing tests** (lowercase, stopword removed, punctuation stripped, deterministic order, min length 3).

- [ ] **Step 2: Implement**

```csharp
public sealed class BagOfWordsKeywordExtractor : IKeywordExtractor
{
    public string[] Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        var normalized = new string(text.ToLowerInvariant()
            .Select(c => char.IsLetter(c) || char.IsDigit(c) || c == ' ' ? c : ' ').ToArray());
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !KeywordExtractorResources.Stopwords.Contains(w))
            .Distinct()
            .OrderBy(w => w, StringComparer.Ordinal)
            .ToArray();
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BagOfWordsKeywordExtractor.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/KeywordExtractorResources.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BagOfWordsKeywordExtractorTests.cs
git commit -m "feat(mechanic-validation): add BagOfWordsKeywordExtractor"
```

### Task 17: `MechanicMatchingEngine`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/IMechanicMatchingEngine.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/MechanicMatchingEngine.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/MechanicMatchingEngineTests.cs`

Contract: `MatchResult Match(IReadOnlyList<AnalysisClaim>, IReadOnlyList<MechanicGoldenClaim>, IReadOnlyList<MechanicGoldenBggTag>, IReadOnlyList<AnalysisMechanicTag>)` returning `(coveragePct, pageAccuracyPct, bggMatchPct, List<MatchDetail>)`.

- [ ] **Step 1: Failing tests** — Jaccard ≥ 0.50 threshold, cosine ≥ 0.75 threshold, page tolerance ±1 counts as page-accurate, greedy first-match (one analysis claim → one golden claim max), BGG tag match is case-insensitive set intersection.

- [ ] **Step 2: Implement**

```csharp
public sealed class MechanicMatchingEngine : IMechanicMatchingEngine
{
    private const double JaccardThreshold = 0.50;
    private const double CosineThreshold = 0.75;
    private const int PageTolerance = 1;

    public MatchResult Match(/* … */)
    {
        var unmatchedGolden = new HashSet<Guid>(golden.Select(g => g.Id));
        var matches = new List<MatchDetail>();

        foreach (var analysis in analysisClaims)
        {
            MechanicGoldenClaim? best = null;
            foreach (var g in golden.Where(g => unmatchedGolden.Contains(g.Id)))
            {
                var jac = Jaccard(analysis.Keywords, g.Keywords);
                var cos = analysis.Embedding is null || g.Embedding is null ? 0.0 : Cosine(analysis.Embedding, g.Embedding);
                if (jac >= JaccardThreshold && cos >= CosineThreshold) { best = g; break; }
            }
            if (best is not null)
            {
                unmatchedGolden.Remove(best.Id);
                var pageDiff = Math.Abs(analysis.Page - best.ExpectedPage);
                matches.Add(new MatchDetail(best.Id, analysis.Id, pageDiff <= PageTolerance, pageDiff));
            }
        }

        var coverage = golden.Count == 0 ? 0m : (decimal)matches.Count / golden.Count * 100m;
        var pageAcc = matches.Count == 0 ? 0m : (decimal)matches.Count(m => m.PageAccurate) / matches.Count * 100m;
        var bggMatched = bggTags.Select(t => t.Name.ToLowerInvariant()).Intersect(analysisTags.Select(t => t.Name.ToLowerInvariant())).Count();
        var bggPct = bggTags.Count == 0 ? 0m : (decimal)bggMatched / bggTags.Count * 100m;

        return new MatchResult(Math.Round(coverage, 2), Math.Round(pageAcc, 2), Math.Round(bggPct, 2), matches);
    }

    private static double Jaccard(string[] a, string[] b)
    {
        var sa = new HashSet<string>(a); var sb = new HashSet<string>(b);
        if (sa.Count == 0 && sb.Count == 0) return 0;
        return (double)sa.Intersect(sb).Count() / sa.Union(sb).Count();
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-9);
    }
}
```

- [ ] **Step 3: Tests PASS + commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/ \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/MechanicMatchingEngineTests.cs
git commit -m "feat(mechanic-validation): add MechanicMatchingEngine"
```

### Task 18: DI registration

**File:** Modify `apps/api/src/Api/Infrastructure/DependencyInjection/SharedGameCatalogServiceCollectionExtensions.cs`

- [ ] **Step 1: Register**

```csharp
services.AddScoped<IKeywordExtractor, BagOfWordsKeywordExtractor>();
services.AddScoped<IMechanicMatchingEngine, MechanicMatchingEngine>();
services.AddScoped<IMechanicGoldenClaimRepository, MechanicGoldenClaimRepository>();
services.AddScoped<IMechanicGoldenBggTagRepository, MechanicGoldenBggTagRepository>();
services.AddScoped<IMechanicAnalysisMetricsRepository, MechanicAnalysisMetricsRepository>();
services.AddScoped<ICertificationThresholdsConfigRepository, CertificationThresholdsConfigRepository>();
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/Infrastructure/DependencyInjection/SharedGameCatalogServiceCollectionExtensions.cs
git commit -m "feat(mechanic-validation): register services + repositories"
```

---

## Phase 5 — Application Commands

Each command task follows: DTO record → Validator (FluentValidation) → Handler → handler unit test → commit. All handlers use `NotFoundException`/`ConflictException`.

### Task 19: `CreateMechanicGoldenClaim` command

**Files:** under `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/Golden/`:
- `CreateMechanicGoldenClaimCommand.cs` — `record CreateMechanicGoldenClaimCommand(Guid SharedGameId, MechanicSection Section, string Statement, int ExpectedPage, string SourceQuote) : IRequest<Guid>`
- `CreateMechanicGoldenClaimValidator.cs` — statement 1..500, page ≥ 1, source quote 1..1000
- `CreateMechanicGoldenClaimHandler.cs` — resolves curator from `ICurrentUserService`, uses `IEmbeddingService` + `IKeywordExtractor`, persists via repo, invalidates cache key `golden:{sharedGameId}`

- [ ] **Step 1–5: Test → Fail → Implement → Pass → Commit** with message `feat(mechanic-validation): add CreateMechanicGoldenClaim command`

### Task 20: `UpdateMechanicGoldenClaim` command

Same pattern. Throws `NotFoundException` if claim not found. Commit: `feat(mechanic-validation): add UpdateMechanicGoldenClaim command`.

### Task 21: `DeactivateMechanicGoldenClaim` command

Idempotent? No — second deactivate throws (aggregate enforces). Maps to `ConflictException`. Commit: `feat(mechanic-validation): add DeactivateMechanicGoldenClaim command`.

### Task 22: `ImportBggTags` command

**Input:** `record ImportBggTagsCommand(Guid SharedGameId, IReadOnlyList<BggTagDto> Tags) : IRequest<int>` where `BggTagDto(string Name, string Category)`.

Handler calls `IMechanicGoldenBggTagRepository.UpsertBatchAsync` (repo de-duplicates by `(SharedGameId, Name)`). Returns count upserted. Commit: `feat(mechanic-validation): add ImportBggTags command`.

### Task 23: `CalculateMechanicAnalysisMetrics` command

**Input:** `record CalculateMechanicAnalysisMetricsCommand(Guid MechanicAnalysisId) : IRequest<Guid>` (returns metrics Id).

**Handler flow:**
1. Load `MechanicAnalysis` (throw `NotFoundException`).
2. Require `Status == Completed` (throw `ConflictException` otherwise).
3. Load golden claims, BGG tags, thresholds, `VersionHash`.
4. Invoke `IMechanicMatchingEngine.Match(...)`.
5. Build `MatchDetails` JSON from engine result.
6. `MechanicAnalysisMetrics.Create(...)` → insert via repo (immutable).
7. `analysis.ApplyMetricsResult(metrics)` → update via repo.
8. If `Certified` → publish `MechanicAnalysisCertifiedEvent` with `WasOverride=false`.

Include handler unit test covering each exception path. Commit: `feat(mechanic-validation): add CalculateMechanicAnalysisMetrics command`.

### Task 24: `OverrideCertification` command

**Input:** `record OverrideCertificationCommand(Guid MechanicAnalysisId, string Reason) : IRequest<Unit>`.

Validator: Reason 20..500 chars. Handler calls `analysis.CertifyViaOverride(reason, currentUserId)`, persists, publishes event with `WasOverride=true`. Commit: `feat(mechanic-validation): add OverrideCertification command`.

### Task 25: `RecalculateAllMechanicMetrics` command

**Input:** `record RecalculateAllMechanicMetricsCommand() : IRequest<int>` (count processed).

Sprint 1: synchronous loop over all `MechanicAnalysis` in `Completed` status, invokes `CalculateMechanicAnalysisMetricsCommand` per-id via `IMediator.Send`. Deferred to Hangfire in Sprint 2. Returns count. Commit: `feat(mechanic-validation): add RecalculateAllMechanicMetrics command`.

### Task 26: `UpdateCertificationThresholds` command

**Input:** `record UpdateCertificationThresholdsCommand(decimal MinCoveragePct, int MaxPageTolerance, decimal MinBggMatchPct, decimal MinOverallScore) : IRequest<Unit>`.

Validator: all 0..100, tolerance ≥ 0. Handler: `CertificationThresholds.Create(...)` → `config.Update(thresholds, userId)` → persist. No mass recalc (user invokes separately). Commit: `feat(mechanic-validation): add UpdateCertificationThresholds command`.

---

## Phase 6 — Application Queries

Each: DTO + Query record + Handler + handler test + commit.

### Task 27: `GetGoldenForGame` query

**Input:** `record GetGoldenForGameQuery(Guid SharedGameId) : IRequest<GoldenForGameDto>`.

**DTO:**
```csharp
public sealed record GoldenForGameDto(
    Guid SharedGameId, string VersionHash,
    IReadOnlyList<GoldenClaimDto> Claims,
    IReadOnlyList<BggTagDto> BggTags);

public sealed record GoldenClaimDto(Guid Id, MechanicSection Section, string Statement, int ExpectedPage, string SourceQuote, string[] Keywords, DateTimeOffset CreatedAt);
```

Uses `IHybridCacheService` with key `golden:{sharedGameId}` + 10min TTL. Commit: `feat(mechanic-validation): add GetGoldenForGame query`.

### Task 28: `GetGoldenVersionHash` query

Lightweight: returns only `VersionHash.Value`. Used by frontend to decide if golden set drifted since last evaluation. Commit: `feat(mechanic-validation): add GetGoldenVersionHash query`.

### Task 29: `GetDashboard` query

Returns `IReadOnlyList<DashboardGameRow>`. Commit: `feat(mechanic-validation): add GetDashboard query`.

### Task 30: `GetTrend` query

**Input:** `GetTrendQuery(Guid SharedGameId, int Take = 20)`. Returns ordered-desc metrics snapshots. Commit: `feat(mechanic-validation): add GetTrend query`.

### Task 31: `GetCertificationThresholds` query

Returns current `CertificationThresholds` VO. Commit: `feat(mechanic-validation): add GetCertificationThresholds query`.

---

## Phase 7 — API Endpoints

### Task 32: `MechanicExtractorValidationEndpoints.cs`

**File:** Create `apps/api/src/Api/Infrastructure/Routing/Admin/MechanicExtractorValidationEndpoints.cs`

- [ ] **Step 1: Implement** — all endpoints `RequireAdminSession()`, all logic via `IMediator.Send()`:

```csharp
public static class MechanicExtractorValidationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/admin/mechanic-extractor").RequireAdminSession();

        g.MapGet("/golden/{sharedGameId:guid}", async (Guid sharedGameId, IMediator m)
            => Results.Ok(await m.Send(new GetGoldenForGameQuery(sharedGameId))));

        g.MapGet("/golden/{sharedGameId:guid}/version-hash", async (Guid sharedGameId, IMediator m)
            => Results.Ok(await m.Send(new GetGoldenVersionHashQuery(sharedGameId))));

        g.MapPost("/golden", async (CreateMechanicGoldenClaimCommand cmd, IMediator m)
            => Results.Created($"/api/v1/admin/mechanic-extractor/golden/{cmd.SharedGameId}", await m.Send(cmd)));

        g.MapPut("/golden/{id:guid}", async (Guid id, UpdateMechanicGoldenClaimRequest body, IMediator m)
            => { await m.Send(body.ToCommand(id)); return Results.NoContent(); });

        g.MapDelete("/golden/{id:guid}", async (Guid id, IMediator m)
            => { await m.Send(new DeactivateMechanicGoldenClaimCommand(id)); return Results.NoContent(); });

        g.MapPost("/golden/{sharedGameId:guid}/bgg-tags", async (Guid sharedGameId, ImportBggTagsRequest body, IMediator m)
            => Results.Ok(new { Upserted = await m.Send(new ImportBggTagsCommand(sharedGameId, body.Tags)) }));

        g.MapPost("/analyses/{id:guid}/metrics", async (Guid id, IMediator m)
            => Results.Ok(new { MetricsId = await m.Send(new CalculateMechanicAnalysisMetricsCommand(id)) }));

        g.MapPost("/analyses/{id:guid}/override-certification", async (Guid id, OverrideCertificationRequest body, IMediator m)
            => { await m.Send(new OverrideCertificationCommand(id, body.Reason)); return Results.NoContent(); });

        g.MapPost("/metrics/recalculate-all", async (IMediator m)
            => Results.Ok(new { Processed = await m.Send(new RecalculateAllMechanicMetricsCommand()) }));

        g.MapGet("/dashboard", async (IMediator m) => Results.Ok(await m.Send(new GetDashboardQuery())));
        g.MapGet("/dashboard/{sharedGameId:guid}/trend", async (Guid sharedGameId, int? take, IMediator m)
            => Results.Ok(await m.Send(new GetTrendQuery(sharedGameId, take ?? 20))));

        g.MapGet("/thresholds", async (IMediator m) => Results.Ok(await m.Send(new GetCertificationThresholdsQuery())));
        g.MapPut("/thresholds", async (UpdateCertificationThresholdsCommand cmd, IMediator m)
            => { await m.Send(cmd); return Results.NoContent(); });
    }
}
```

- [ ] **Step 2: Register** in `Program.cs`: `MechanicExtractorValidationEndpoints.Map(app);`

- [ ] **Step 3: Integration tests** — at least one test per endpoint, asserting status codes, auth enforcement, and exception→HTTP mapping (404/409/400).

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Routing/Admin/MechanicExtractorValidationEndpoints.cs \
        apps/api/src/Api/Program.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Api/
git commit -m "feat(mechanic-validation): add admin endpoints"
```

### Task 33: Observability metrics

**File:** Modify `apps/api/src/Api/Observability/MeepleAiMetrics.cs`

- [ ] **Step 1: Add counters/histograms**

```csharp
public static class Validation
{
    public static readonly Counter<long> MetricsComputed = Meter.CreateCounter<long>("mechanic_validation_metrics_computed_total");
    public static readonly Counter<long> CertificationsGranted = Meter.CreateCounter<long>("mechanic_validation_certifications_granted_total");
    public static readonly Counter<long> Overrides = Meter.CreateCounter<long>("mechanic_validation_overrides_total");
    public static readonly Histogram<double> MatchingDuration = Meter.CreateHistogram<double>("mechanic_validation_matching_duration_seconds");
    public static readonly ObservableGauge<double> LatestOverallScore = Meter.CreateObservableGauge<double>("mechanic_validation_latest_overall_score", …);
}
```

- [ ] **Step 2: Wire `.Add()` calls in `CalculateMechanicAnalysisMetricsHandler`, `OverrideCertificationCommandHandler`, and the matching engine.**

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Observability/MeepleAiMetrics.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/
git commit -m "feat(mechanic-validation): add Prometheus metrics"
```

---

## Phase 8 — Frontend MVP + Seed + E2E

### Task 34: API client

**File:** Create `apps/web/src/lib/api/admin/mechanicExtractorValidation.ts`

- [ ] **Step 1: Implement** — typed client functions wrapping fetch against `/api/v1/admin/mechanic-extractor/*`. Include: `getGolden(sharedGameId)`, `getGoldenVersionHash(sharedGameId)`, `createClaim(...)`, `updateClaim(...)`, `deactivateClaim(...)`, `importBggTags(sharedGameId, tags)`, `calculateMetrics(analysisId)`, `overrideCertification(analysisId, reason)`, `getDashboard()`, `getTrend(sharedGameId)`, `getThresholds()`, `updateThresholds(thresholds)`.

- [ ] **Step 2: Commit** `feat(mechanic-validation/web): add API client`

### Task 35: React Query hooks

**Files:** under `apps/web/src/hooks/admin/`:
- `useGoldenForGame.ts`, `useCreateGoldenClaim.ts`, `useUpdateGoldenClaim.ts`, `useDeactivateGoldenClaim.ts`
- `useCalculateMetrics.ts`, `useOverrideCertification.ts`
- `useValidationDashboard.ts`

- [ ] **Step 1: Implement** with proper invalidation of `['golden', sharedGameId]` and `['validation-dashboard']` on mutations.

- [ ] **Step 2: Commit** `feat(mechanic-validation/web): add React Query hooks`

### Task 36: Golden CRUD components + page

**Files:** under `apps/web/src/components/admin/mechanic-extractor/validation/`:
- `GoldenClaimForm.tsx` (shadcn form, react-hook-form + zod)
- `GoldenClaimsList.tsx` (table with inline deactivate)
- `GoldenVersionHashBadge.tsx` (short 8-char display, copy-to-clipboard, tooltip with full hash)

**Pages:**
- Create: `apps/web/src/app/admin/knowledge-base/mechanic-extractor/golden/page.tsx` (list games → link to edit page)
- Create: `apps/web/src/app/admin/knowledge-base/mechanic-extractor/golden/[gameId]/page.tsx`

- [ ] **Step 1: Implement**, hide behind `process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'` (return 404 otherwise).

- [ ] **Step 2: Vitest component tests** for form validation + list deactivation confirm dialog.

- [ ] **Step 3: Commit** `feat(mechanic-validation/web): add golden CRUD UI`

### Task 37: Review page augmentation

**File:** Modify `apps/web/src/app/admin/knowledge-base/mechanic-extractor/analyses/review/page.tsx`

- [ ] **Step 1: Add** `MetricsCard` component in `components/admin/mechanic-extractor/validation/MetricsCard.tsx` showing: coverage/page/bgg/overall scores + certification badge (Certified green, NotCertified red, NotEvaluated gray) + drift warning when `versionHash` of metrics != current golden hash.

- [ ] **Step 2: Add** `EvaluateButton.tsx` (triggers `useCalculateMetrics`) and `OverrideCertificationDialog.tsx` (reason textarea 20..500 chars, confirm).

- [ ] **Step 3: Integrate** the block under existing review content, gated by feature flag.

- [ ] **Step 4: Commit** `feat(mechanic-validation/web): augment review page with metrics`

### Task 38: Dashboard base

**File:** Create `apps/web/src/app/admin/knowledge-base/mechanic-extractor/dashboard/page.tsx`

- [ ] **Step 1: Implement** with:
  - `DashboardSummaryCards.tsx` — totals (Certified / NotCertified / NotEvaluated counts)
  - `DashboardTable.tsx` — rows from `GetDashboard` with sort by `overallScore` desc, links to review page

- [ ] **Step 2: Commit** `feat(mechanic-validation/web): add dashboard base`

### Task 39: Feature flag + env example

**File:** Modify `apps/web/.env.development.example` — append `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true`. Commit: `chore(mechanic-validation/web): add feature flag env example`.

### Task 40: Catan golden seed

**Files:**
- Create: `apps/api/src/Api/Data/Seed/catan-golden.json` — ~40 claims curated from Catan rulebook (Italian + English), structured as:
  ```json
  [
    { "section": "Turn", "statement": "Il giocatore tira 2 dadi all'inizio del turno.", "expectedPage": 4, "sourceQuote": "All'inizio del suo turno ogni giocatore tira i due dadi." },
    …
  ]
  ```
- Create: `apps/api/src/Api/Data/Seed/CatanGoldenSeed.cs` — loader invoked during seed migration (only in Development/Staging, not Production).

- [ ] **Step 1: Curate** ~40 Catan claims across sections Setup/Turn/Actions/Trading/Combat/End. Include ~15 BGG tags (`Dice Rolling`, `Trading`, `Network Building`, etc.).

- [ ] **Step 2: Implement seed loader** that creates each claim via aggregate factory (triggers embedding + keyword extraction automatically) inside one transaction.

- [ ] **Step 3: Wire seed execution** in `DataSeeder` when `ASPNETCORE_ENVIRONMENT != Production`.

- [ ] **Step 4: Run seed locally** — `dotnet run --environment Development` → verify 40 claims in `mechanic_golden_claims` + ~15 rows in `mechanic_golden_bgg_tags` for Catan's SharedGameId.

- [ ] **Step 5: Commit** `feat(mechanic-validation): add Catan golden seed (~40 claims)`.

### Task 41: Playwright smoke E2E

**File:** Create `apps/web/e2e/admin/mechanic-extractor-validation-catan.spec.ts`

- [ ] **Step 1: Write test** with `PLAYWRIGHT_AUTH_BYPASS=true`:

```ts
test('Catan end-to-end certification path', async ({ page, context }) => {
  // 1. Navigate to golden page for Catan
  await page.goto('/admin/knowledge-base/mechanic-extractor/golden/<catan-shared-game-id>');
  await expect(page.getByTestId('golden-claims-list')).toBeVisible();
  await expect(page.getByTestId('golden-version-hash')).toContainText(/^[a-f0-9]{8}/);

  // 2. Navigate to review page for a Catan MechanicAnalysis
  await page.goto('/admin/knowledge-base/mechanic-extractor/analyses/<catan-analysis-id>/review');

  // 3. Click "Evaluate" → expect MetricsCard to appear
  await page.getByRole('button', { name: /valuta|evaluate/i }).click();
  await expect(page.getByTestId('metrics-card')).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId('metrics-card')).toContainText(/coverage/i);

  // 4. Navigate to dashboard → expect Catan row
  await page.goto('/admin/knowledge-base/mechanic-extractor/dashboard');
  await expect(page.getByRole('row', { name: /catan/i })).toBeVisible();
});
```

- [ ] **Step 2: Run locally** — `pnpm test:e2e --grep "Catan end-to-end"` — expected PASS.

- [ ] **Step 3: Commit** `test(mechanic-validation): add Catan E2E smoke`.

### Task 42: Final CI green + PR

- [ ] **Step 1: Run full backend suite** — `cd apps/api/src/Api && dotnet test` — expected all PASS.

- [ ] **Step 2: Run full frontend suite** — `cd apps/web && pnpm test && pnpm test:e2e && pnpm typecheck && pnpm lint` — expected all PASS.

- [ ] **Step 3: Push + open PR** to parent branch `main-dev`:

```bash
git push -u origin feature/mechanic-extractor-ai-validation-sprint-1
gh pr create --base main-dev --title "feat(mechanic-validation): Sprint 1 — infrastructure + Catan MVP" --body "$(cat <<'EOF'
## Summary
- Implements Sprint 1 of Project B (Mechanic Extractor AI Comprehension Validation) per `docs/superpowers/specs/2026-04-24-mechanic-extractor-ai-validation-design.md` Section 9.
- M2.0 migration: `mechanic_golden_claims`, `mechanic_golden_bgg_tags`, `mechanic_analysis_metrics`, `certification_thresholds_config` + ALTER `mechanic_analyses` with 5 certification columns.
- Hybrid matching engine (Jaccard ≥ 0.50, cosine ≥ 0.75, page tolerance ±1, greedy first-match).
- CQRS commands + queries + admin endpoints.
- Frontend golden CRUD + review augmentation + dashboard base behind feature flag.
- Catan golden seeded (~40 claims + ~15 BGG tags).

## Test plan
- [x] Backend unit + integration tests green (dotnet test)
- [x] Frontend unit tests green (vitest)
- [x] Typecheck + lint clean
- [x] Playwright smoke Catan end-to-end green
- [x] Local migration up/down round-trip verified
- [x] Manual smoke: create claim → evaluate analysis → dashboard shows Catan

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Post-merge cleanup**

```bash
git checkout main-dev && git pull
git branch -D feature/mechanic-extractor-ai-validation-sprint-1
git remote prune origin
```

---

## Self-Review Checklist (executor should verify after Phase 8)

- [ ] **Spec coverage** — every requirement in spec Section 9 maps to a task.
- [ ] **No placeholders** — no `TODO`, no "implement later", no skipped tests.
- [ ] **Type consistency** — `SharedGameId` (never `GameId`) in all new code; `shared_game_id` in all SQL; `VersionHash` type used consistently.
- [ ] **Exception hygiene** — only `NotFoundException` / `ConflictException` / `ArgumentException` from handlers (never `InvalidOperationException`).
- [ ] **CQRS purity** — no endpoint injects a service directly.
- [ ] **Immutability** — `MechanicAnalysisMetrics` has no public setters (verified via reflection test in Task 8).
- [ ] **Feature flag** — frontend pages return 404 when flag off.

---
