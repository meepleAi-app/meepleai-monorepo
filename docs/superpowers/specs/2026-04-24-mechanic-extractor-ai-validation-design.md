# Mechanic Extractor — AI Comprehension Validation Design

**Date**: 2026-04-24
**Status**: Design approved, pending implementation plan
**Author**: Brainstorming session (`/sc:spec-panel` + `superpowers:brainstorming`)
**Bounded Contexts**: SharedGameCatalog (primary — golden claims + validation extend M1.2), SystemConfiguration (certification thresholds)
**Depends on**: M1.2 Mechanic Extractor (ISSUE-524 / ADR-051) — already shipped in `SharedGameCatalog` BC
**Note**: All FKs use `SharedGameId` (not `GameId`) to match existing `MechanicAnalysis.SharedGameId` pattern.

## 1. Purpose & Scope

### Vision
> _"Il mechanic extractor dovrebbe creare con l'AI e il rulebook selezionato le meccaniche per due scopi:
> 1) creare specchietti per facilitare la consultazione delle regole/meccaniche del gioco,
> 2) dimostrare che l'AI comprende le regole del gioco."_

This spec addresses **(2) — demonstrate AI comprehension**.

A companion project **(A — rulebook specchietti catalog)** is architecturally independent and may be built later consuming `WHERE CertificationStatus = Certified` from this system. Not in scope here.

### Goal
Establish an objective, data-driven mechanism to certify that a `MechanicAnalysis` faithfully represents the rulebook content, by comparing AI output against a curator-maintained **golden set** and computing reproducible quality metrics over time.

### Non-goals
- Public-facing mechanic catalog / specchietti UI (Project A)
- Auto-generation of golden set from rulebook (out of scope MVP — manual curation)
- Multi-language golden (EN + IT parallele) — post-shipping backlog
- Downgrade override (Certified → NotCertified) — MVP supports only upgrade override
- Real-time auto-recompute on golden change — MVP requires manual recalc

### Pilot games
Three games spanning complexity range:
1. **Catan** — simple, short rules (~40 golden claims expected)
2. **Puerto Rico** — medium complexity (~50 claims)
3. **Mage Knight Board Game** — high complexity (~80 claims) — confirmed present in `data/rulebook/manifest.json` (bggId 96848, id 5)

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Curator (admin)                                             │
│   ├─ CRUD atomic golden claims per game (Sezione 1..6)     │
│   └─ Import BGG category/mechanics tags                     │
└────────────┬────────────────────────────────────────────────┘
             │
             ▼
  ┌─────────────────────────┐         ┌─────────────────────┐
  │ MechanicGoldenClaim     │◀────────│ MechanicGoldenBggTag│
  │  (aggregate root)       │         │  (entity)           │
  │  - statement (≤500)     │         │  - name             │
  │  - expectedPage         │         │  - category         │
  │  - embedding (768)      │         │                     │
  │  - keywords[]           │         └─────────────────────┘
  │  - VersionHash (derived)│
  └────────────┬────────────┘
               │
               ▼
  ┌──────────────────────────────┐
  │ MechanicMatchingEngine       │   uses: IEmbeddingService + BagOfWordsKeywordExtractor
  │  - hybrid keyword + semantic │
  │  - page tolerance ±1          │
  │  - section-scoped             │
  │  - greedy first-match         │
  └────────────┬─────────────────┘
               │
               ▼
  ┌───────────────────────────────────────────┐      ┌─────────────────────┐
  │ MechanicAnalysisMetrics (IMMUTABLE)       │─────▶│ MechanicAnalysis    │
  │  - coveragePct, pageAccuracyPct,          │      │  + CertificationStatus
  │    bggMatchPct, overallScorePct           │      │  + LastMetricsId    │
  │  - goldenVersionHash (snapshot)           │      │  + CertifiedAt/By   │
  │  - promptVersion, modelName (snapshot)    │      │  + OverrideReason   │
  │  - matchDetails (JSONB, audit)            │      └─────────────────────┘
  │  - insert-only per history/trend          │
  └───────────────────────────────────────────┘
               ▲
               │ thresholds (singleton, runtime-configurable)
  ┌──────────────────────────────┐
  │ CertificationThresholdsConfig│   bounded context: SystemConfiguration
  │  - coverageMinPct (70)       │
  │  - pageAccuracyMinPct (10)   │
  │  - bggMatchMinPct (80)       │
  │  - overallMinPct (60)        │
  └──────────────────────────────┘
```

## 3. Domain Model

### 3.1 MechanicGoldenClaim (aggregate root)

Atomic assertion authored by curator that the AI must match.

- **Properties**: `Id`, `GameId`, `Section` (enum `MechanicSection`, 1–6 coerente M1.2), `Statement` (max 500 char), `ExpectedPage` (≥ 1), `ExpectedSourceQuote` (optional, max 1000), `StatementEmbedding` (`vector(768)`, computed at factory/update), `Keywords` (`text[]`, computed at factory/update), `CuratorUserId`, `IsActive` (soft-delete), timestamps, `RowVersion` (xmin).
- **Invariants**: statement non vuoto, keywords non vuoto, section valida, expectedPage > 0.
- **Factory**: `Create(gameId, section, statement, expectedPage, sourceQuote, curatorUserId, IEmbeddingService, IKeywordExtractor)` — embed + extract keywords inline.
- **Methods**: `Update(...)` (ricalcola embedding + keywords se statement cambia), `Deactivate()` (soft-delete).

### 3.2 MechanicGoldenBggTag (entity)

Community-sourced tag from BoardGameGeek for the game.

- **Properties**: `Id`, `GameId`, `Name`, `Category` (string libera: "mechanic", "category", …), `ImportedByUserId`, `ImportedAt`.
- **Uniqueness**: `(GameId, Name)` — import idempotente.

### 3.3 VersionHash (derived VO)

`SHA256(ordered(claim.Id + claim.Statement + claim.ExpectedPage) + ordered(bggTag.Name))` → hex string.

Calcolato on-demand da `IMechanicGoldenClaimRepository.GetVersionHashAsync(gameId)`, esposto come query `GetGoldenVersionHashQuery` (HybridCache TTL 60s, invalidato da ogni golden CRUD). Serve a:
- Invalidare metriche stale (analysis con `goldenVersionHash != current` → richiede ricalcolo)
- Audit trail: sapere quale versione golden è stata usata per ogni valutazione

### 3.4 MechanicAnalysisMetrics (aggregate, immutable)

Snapshot di una valutazione AI vs. golden. Insert-only (per trend history).

- **Properties**: `Id`, `AnalysisId` (FK deferrabile → `MechanicAnalysis.Id`), `GoldenVersionHash`, `PromptVersion` (snapshot da `MechanicAnalysis`), `ModelName` (snapshot), `CoveragePct`, `PageAccuracyPct`, `BggMatchPct`, `OverallScorePct`, `CertificationStatus` (computed al create), `MatchDetails` (JSONB — lista match per claim), `CalculatedAt`, `CalculatedByUserId`.
- **Invariants**: ogni `*Pct ∈ [0, 100]`.
- **No update**: modificare una metrica significa crearne una nuova (nuova row).
- **Formula `OverallScorePct`**: `weighted_avg(coverage*0.4, pageAccuracy*0.2, bggMatch*0.4)` — pesi hardcoded MVP, spostabili in config post-shipping.

### 3.5 CertificationStatus (enum)

- `0 = NotEvaluated` (default, M1.2 analyses pre-feature)
- `1 = Certified` (tutte e 4 le soglie rispettate, o override manuale)
- `2 = NotCertified` (almeno una soglia violata)

### 3.6 CertificationThresholds (VO) + CertificationThresholdsConfig (aggregate singleton)

VO immutabile con 4 float `[0,100]`. Config singleton (id=1) runtime-modificabile, `RowVersion` (xmin) concurrency, audit `UpdatedByUserId` + `UpdatedAt`.

Default seed: `70 / 10 / 80 / 60` (coverage / page / bgg / overall).

### 3.7 MechanicAnalysisCertifiedEvent (domain event)

Pubblicato quando `CertificationStatus` transita a `Certified` (naturale o override). Consumato da:
- `IAuditLogger` per audit trail
- (futuro) Project A per aggiornare catalog specchietti

## 4. Infrastructure

### 4.1 Migrations

```sql
-- Golden claims
CREATE TABLE mechanic_golden_claims (
    id uuid PRIMARY KEY,
    game_id uuid NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    section int NOT NULL CHECK (section BETWEEN 1 AND 6),
    statement varchar(500) NOT NULL,
    expected_page int NOT NULL CHECK (expected_page >= 1),
    expected_source_quote varchar(1000) NULL,
    statement_embedding vector(768) NULL,
    keywords text[] NOT NULL,
    curator_user_id uuid NOT NULL REFERENCES users(id),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    deleted_at timestamptz NULL
);
CREATE INDEX ix_golden_claims_game_section ON mechanic_golden_claims(game_id, section) WHERE is_active = true AND deleted_at IS NULL;
CREATE INDEX ix_golden_claims_embedding ON mechanic_golden_claims USING ivfflat (statement_embedding vector_cosine_ops) WITH (lists = 100);

-- BGG tags
CREATE TABLE mechanic_golden_bgg_tags (
    id uuid PRIMARY KEY,
    game_id uuid NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    name varchar(200) NOT NULL,
    category varchar(100) NOT NULL,
    imported_by_user_id uuid NOT NULL REFERENCES users(id),
    imported_at timestamptz NOT NULL,
    UNIQUE (game_id, name)
);

-- Metrics snapshots (insert-only)
CREATE TABLE mechanic_analysis_metrics (
    id uuid PRIMARY KEY,
    analysis_id uuid NOT NULL,
    golden_version_hash char(64) NOT NULL,
    prompt_version varchar(50) NOT NULL,
    model_name varchar(100) NOT NULL,
    coverage_pct numeric(5,2) NOT NULL CHECK (coverage_pct BETWEEN 0 AND 100),
    page_accuracy_pct numeric(5,2) NOT NULL CHECK (page_accuracy_pct BETWEEN 0 AND 100),
    bgg_match_pct numeric(5,2) NOT NULL CHECK (bgg_match_pct BETWEEN 0 AND 100),
    overall_score_pct numeric(5,2) NOT NULL CHECK (overall_score_pct BETWEEN 0 AND 100),
    certification_status int NOT NULL CHECK (certification_status IN (1, 2)),
    match_details jsonb NOT NULL,
    calculated_at timestamptz NOT NULL,
    calculated_by_user_id uuid NOT NULL REFERENCES users(id)
);
CREATE INDEX ix_metrics_analysis ON mechanic_analysis_metrics(analysis_id, calculated_at DESC);

-- Thresholds (singleton)
-- NOTE: updated_by_user_id nullable per consentire seed migration prima che esistano admin user.
-- Al primo UPDATE admin via API, il campo diventa valorizzato e poi sempre NOT NULL a livello applicativo.
CREATE TABLE certification_thresholds_config (
    id int PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    coverage_min_pct numeric(5,2) NOT NULL DEFAULT 70,
    page_accuracy_min_pct numeric(5,2) NOT NULL DEFAULT 10,
    bgg_match_min_pct numeric(5,2) NOT NULL DEFAULT 80,
    overall_min_pct numeric(5,2) NOT NULL DEFAULT 60,
    updated_at timestamptz NOT NULL,
    updated_by_user_id uuid NULL REFERENCES users(id)  -- NULL solo per seed singleton iniziale
);
INSERT INTO certification_thresholds_config (id, updated_at, updated_by_user_id)
VALUES (1, NOW(), NULL) ON CONFLICT DO NOTHING;

-- ALTER MechanicAnalysis
ALTER TABLE mechanic_analyses
    ADD COLUMN certification_status int NOT NULL DEFAULT 0 CHECK (certification_status IN (0, 1, 2)),
    ADD COLUMN certified_at timestamptz NULL,
    ADD COLUMN certified_by_user_id uuid NULL REFERENCES users(id),
    ADD COLUMN certification_override_reason varchar(500) NULL,
    ADD COLUMN last_metrics_id uuid NULL REFERENCES mechanic_analysis_metrics(id) DEFERRABLE INITIALLY DEFERRED ON DELETE SET NULL;

-- FK circolare: metrics.analysis_id → analyses.id già esiste via FK standard
-- + analyses.last_metrics_id → metrics.id DEFERRABLE → risolve race su insert
ALTER TABLE mechanic_analysis_metrics
    ADD CONSTRAINT fk_metrics_analysis
    FOREIGN KEY (analysis_id) REFERENCES mechanic_analyses(id) DEFERRABLE INITIALLY DEFERRED;
```

### 4.2 EF Core configuration pattern

```csharp
public sealed class MechanicGoldenClaimConfiguration : IEntityTypeConfiguration<MechanicGoldenClaim>
{
    public void Configure(EntityTypeBuilder<MechanicGoldenClaim> b)
    {
        b.ToTable("mechanic_golden_claims");
        b.HasKey(x => x.Id);
        b.Property(x => x.GameId).HasColumnName("game_id");
        b.Property(x => x.Section).HasColumnName("section").HasConversion<int>();
        b.Property(x => x.Statement).HasColumnName("statement").HasMaxLength(500).IsRequired();
        b.Property(x => x.ExpectedPage).HasColumnName("expected_page");
        b.Property(x => x.ExpectedSourceQuote).HasColumnName("expected_source_quote").HasMaxLength(1000);
        b.Property(x => x.StatementEmbedding).HasColumnName("statement_embedding").HasColumnType("vector(768)");
        b.Property(x => x.Keywords).HasColumnName("keywords").HasColumnType("text[]");
        b.Property(x => x.CuratorUserId).HasColumnName("curator_user_id");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.UseXminAsConcurrencyToken();
        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
```

### 4.3 Repositories (interfaces in Domain, impl in Infrastructure)

- `IMechanicGoldenClaimRepository` — `GetByGameAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeactivateAsync`, `GetVersionHashAsync`
- `IMechanicGoldenBggTagRepository` — `GetByGameAsync`, `UpsertBatchAsync`, `DeleteAsync`
- `IMechanicAnalysisMetricsRepository` — `AddAsync`, `GetByAnalysisAsync` (history), `GetLatestByAnalysisAsync`, `GetDashboardAsync`, `GetTrendAsync(gameId, limit)`
- `ICertificationThresholdsConfigRepository` — `GetAsync`, `UpdateAsync` (with xmin check)

### 4.4 MechanicMatchingEngine

```csharp
public sealed class MechanicMatchingEngine : IMechanicMatchingEngine
{
    private const double KEYWORD_OVERLAP_THRESHOLD = 0.50;
    private const double SEMANTIC_SIMILARITY_THRESHOLD = 0.75;
    private const int PAGE_TOLERANCE = 1;

    public async Task<MatchingResult> MatchAsync(
        IReadOnlyList<MechanicGoldenClaim> golden,
        MechanicAnalysis analysis,
        IReadOnlyList<MechanicGoldenBggTag> bggTags,
        CancellationToken ct)
    {
        // 1. Per ogni golden claim in section S, cerca match tra AI claims in section S
        //    - keyword overlap ≥ 0.50 (Jaccard su keywords[])
        //    - cosine similarity embedding ≥ 0.75
        //    - greedy first-match (consume AI claim)
        //    - page accuracy: |aiPage - expectedPage| ≤ 1
        // 2. BGG tags: keyword-only match tra tags.Name e AI output flat text
        // 3. Calcola CoveragePct, PageAccuracyPct (solo su matched), BggMatchPct
        // 4. OverallScorePct = 0.4*coverage + 0.2*pageAccuracy + 0.4*bgg
        // 5. Produce MatchDetails JSON audit
    }
}
```

**Known limitations MVP** (documentate):
- Greedy first-match (non bipartite optimal)
- Page tolerance hardcoded (non configurabile per gioco)
- Embedding fallback: se `IEmbeddingService` fallisce → calcolo metriche **fallisce** (409), no degradation a solo keyword (evita metriche inconsistenti)

### 4.5 BagOfWordsKeywordExtractor

Stopwords IT + EN (liste statiche in file `KeywordExtractorResources.cs`), normalize lowercase, simple stemming (troncamento -e/-i/-o/-a/-s/-ed/-ing), dedup. Input: stringa. Output: `string[]`.

## 5. Application Layer (CQRS)

Tutti i handler sotto `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/`. Pattern M1.2: command/query + validator FluentValidation + handler MediatR.

### 5.1 Golden commands

- `CreateMechanicGoldenClaimCommand { GameId, Section, Statement, ExpectedPage, ExpectedSourceQuote?, CuratorUserId }`
  - Validator: Statement 1–500, Keywords extracted ≥ 1, Section 1–6, ExpectedPage ≥ 1
  - Handler: embed + extract → persist
- `UpdateMechanicGoldenClaimCommand { Id, Statement, ExpectedPage, ExpectedSourceQuote? }`
  - Handler: re-embed se Statement cambiato, re-extract keywords
- `DeactivateMechanicGoldenClaimCommand { Id }` → soft-delete
- `ImportBggTagsCommand { GameId, Tags[] { Name, Category }, ImportedByUserId }` → upsert idempotente

### 5.2 Metrics commands

- `CalculateMechanicAnalysisMetricsCommand { AnalysisId, CalculatedByUserId }`
  - Validator: AnalysisId non-empty
  - Handler:
    1. Load analysis (404 se assente) + verifica `Status == Published` (409 altrimenti)
    2. Load golden per `analysis.GameId` (409 se vuoto)
    3. Run `MechanicMatchingEngine`
    4. Insert `MechanicAnalysisMetrics` row (immutabile)
    5. Update `MechanicAnalysis.LastMetricsId` + `CertificationStatus` + `CertifiedAt` (se Certified)
    6. Publish `MechanicAnalysisCertifiedEvent` (solo se transita a Certified)
    7. Invalidate caches: `mechanic-analysis:{id}`, `mechanic-dashboard:*`
- `OverrideCertificationCommand { AnalysisId, Reason, OverriddenByUserId }`
  - Validator: Reason 20–500 char
  - Handler:
    1. Load analysis (404), verifica `LastMetricsId != null` (409 altrimenti), verifica `CertificationStatus != Certified` (409 se già Certified)
    2. Set `CertificationStatus = Certified`, `CertifiedAt = NOW`, `CertifiedByUserId = session.user`, `CertificationOverrideReason = Reason`
    3. **No** new row in metrics (override non è valutazione)
    4. `IAuditLogger` audit entry
    5. Publish `MechanicAnalysisCertifiedEvent`
- `RecalculateAllMechanicMetricsCommand { GameId?, EnqueuedByUserId }`
  - Handler: enqueue Hangfire job (existing M1.2 pattern), return `{ jobId }` con 202 Accepted
  - Job: itera analysis Published del gioco (o tutti) + calcola metriche per ciascuna

### 5.3 Thresholds commands (SystemConfiguration BC)

- `UpdateCertificationThresholdsCommand { CoverageMinPct, PageAccuracyMinPct, BggMatchMinPct, OverallMinPct, UpdatedByUserId }`
  - Validator: ogni pct ∈ [0,100]
  - Handler: load singleton, update, xmin concurrency check, audit, invalidate cache `certification-thresholds:v1`
  - **Non** ricalcola metriche automaticamente (troppo costoso); admin invoca manualmente `/metrics/recalculate-all`

### 5.4 Queries

- `GetGoldenForGameQuery { GameId }` → claims + bggTags + versionHash + metadata
- `GetGoldenVersionHashQuery { GameId }` → solo hash (lightweight)
- `GetDashboardQuery { }` → lista giochi con ultima metrica + trend last-5 + summary KPI (HybridCache TTL 60s)
- `GetTrendQuery { GameId, Limit = 20 }` → lista puntiforme (HybridCache TTL 120s)
- `GetCertificationThresholdsQuery { }` → singleton (HybridCache TTL 300s)

## 6. API Endpoints

Tutti sotto `/api/v1/admin/*`, protetti da `RequireAdminSessionFilter`. Endpoint delegano **solo** `IMediator.Send()` (CLAUDE.md CQRS rule).

### Golden CRUD
- `POST /admin/mechanic-extractor/golden/claims` → 201
- `PUT /admin/mechanic-extractor/golden/claims/{id}` → 200
- `DELETE /admin/mechanic-extractor/golden/claims/{id}` → 204
- `POST /admin/mechanic-extractor/golden/bgg-tags/import` → 200
- `GET /admin/mechanic-extractor/golden/games/{gameId}` → 200
- `GET /admin/mechanic-extractor/golden/games/{gameId}/version` → 200 (hash only)

### Metrics
- `POST /admin/mechanic-extractor/analyses/{analysisId}/metrics/calculate` → 200 (+ domain event Certified)
- `POST /admin/mechanic-extractor/analyses/{analysisId}/certification/override` → 200 (admin-trusted)
- `POST /admin/mechanic-extractor/metrics/recalculate-all` → 202 + jobId (Hangfire)

### Dashboard
- `GET /admin/mechanic-extractor/dashboard` → 200
- `GET /admin/mechanic-extractor/dashboard/trend/{gameId}?limit=20` → 200

### Thresholds (SystemConfiguration BC)
- `GET /admin/config/certification-thresholds` → 200
- `PUT /admin/config/certification-thresholds` → 200 (xmin concurrency → 409 on conflict)

### Error model (coerente M1.2)
- `NotFoundException` → 404
- `ConflictException` → 409
- `ValidationException` → 400
- `DbUpdateConcurrencyException` → 409 `CONCURRENCY_CONFLICT`

## 7. Frontend

### 7.1 Nuove route

```
/admin/knowledge-base/mechanic-extractor/
├── golden/                         ← lista giochi con golden
│   └── [gameId]/                   ← CRUD claim + BGG tag per gioco
├── dashboard/                      ← certification dashboard + trend
└── analyses/review                 ← esistente, AUGMENTED
/admin/config/certification-thresholds/  ← standalone config page
```

### 7.2 Golden CRUD UI

- Lista giochi con golden (dalla dashboard API con filter `hasGolden`)
- Detail page per gioco: due pannelli (claims accordion per section + BGG tag importer)
- Componenti: `GoldenClaimForm.tsx` (react-hook-form + zod), `BggTagImporter.tsx` (bulk TSV paste + preview), `GoldenVersionHashBadge.tsx`
- Hooks React Query: `useGoldenForGame`, `useCreateGoldenClaim`, `useUpdateGoldenClaim`, `useDeactivateGoldenClaim`, `useImportBggTags`, `useGoldenVersionHash`

### 7.3 Review page augmented

Aggiungere (non sostituire UI M1.2) a `/admin/knowledge-base/mechanic-extractor/analyses/review/page.tsx`:

1. **Card "Valutazione AI vs. Golden"**: pulsante "Valuta contro Golden" (quando Published + no metrics) o card metriche (quando metrics presenti)
2. **Card metriche**: progress bar per Coverage/PageAccuracy/BggMatch/Overall con soglie visibili + stato Certified/NotCertified + metadata (model, promptVersion, goldenHash) + pulsanti `[Ricalcola]` `[Override a Certified]`
3. **Expandable "Dettagli matching"**: breakdown per section da `MatchDetails` JSONB

Componenti: `MetricsCard.tsx`, `EvaluateButton.tsx`, `OverrideCertificationDialog.tsx` (AlertDialog + textarea reason 20–500), `MatchingDetailsAccordion.tsx`

### 7.4 Dashboard

- Summary KPI (giochi con golden, analisi certificate, avg overall score)
- Tabella giochi con sparkline trend last-5 + stato badge + actions
- Drawer full trend con `recharts AreaChart` + soglie come linee orizzontali

Componenti: `DashboardSummaryCards.tsx`, `DashboardTable.tsx`, `TrendSparkline.tsx`, `GameTrendDrawer.tsx`

### 7.5 Config soglie (standalone)

`/admin/config/certification-thresholds/page.tsx` — 4 slider + numeric input + "Salva" + pulsante danger "Ricalcola tutte le metriche" (AlertDialog conferma → POST `/metrics/recalculate-all` → banner progress)

### 7.6 Feature flag

`NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED` — gating frontend solo (admin trusted, backend endpoint sempre attivi con admin role).

## 8. Testing Strategy

### 8.1 Backend unit tests

- `MechanicMatchingEngineTests` — 10+ casi pinnati (thresholds, section-scoping, page tolerance, greedy, BGG keyword-only, empty golden contract, MatchDetails JSON output)
- `BagOfWordsKeywordExtractorTests` — stopwords IT+EN, lowercase, dedup, hyphens
- `MechanicGoldenClaimTests`, `MechanicAnalysisMetricsTests` (immutability), `CertificationThresholdsTests`

### 8.2 Backend integration tests (Testcontainers)

- `CalculateMechanicAnalysisMetricsCommandHandlerTests` (happy path + 409/404 + event publication + versionHash snapshot)
- `OverrideCertificationCommandHandlerTests` (already-certified 409, null-metrics 409, audit, session user)
- `CreateMechanicGoldenClaimCommandHandlerTests` (embedding 768-dim, versionHash update, validation)
- `UpdateCertificationThresholdsCommandHandlerTests` (xmin concurrency 409, cache invalidation)
- Query tests: soft-delete filter, ordering, dashboard/trend projections

### 8.3 Calibration spike (manuale, fuori CI)

`apps/api/tests/Api.Tests/Spikes/MechanicMatchingCalibrationSpike.cs` con trait `[Category=Spike]`. Input: golden + AI output fixture + ground truth manuale. Output: CSV matrix soglie → precision/recall/F1. Eseguito prima di recalibration, non in CI.

### 8.4 Frontend Vitest

- Hook tests: `useGoldenForGame`, `useCalculateMetrics`, `useOverrideCertification` (invalidation cascade)
- Component tests: `MetricsCard`, `OverrideCertificationDialog` (validation 20–500), `GoldenClaimForm` (zod), `BggTagImporter`, `TrendSparkline`, `DashboardTable`

### 8.5 Playwright E2E

5 flow in `apps/web/e2e/admin/mechanic-extractor-validation.spec.ts`:
1. Curator crea golden per Catan
2. Admin valuta analysis Published
3. Override certification + audit
4. Dashboard trend drawer
5. Config soglie + ricalcolo massa (smoke, ~30s accettabile)

### 8.6 Coverage targets

- Domain 95%+, Application 90%+, Infrastructure 85%+ (excl spike), Frontend 85%+ — CI gate

### 8.7 Performance/load test

**Rimandato post-shipping** (backlog). MVP assume golden < 100 claim, AI output < 100 items.

## 9. Rollout Plan

### Sprint 1 — Infrastructure + Catan MVP
Migration + domain + matching engine + endpoint + frontend golden/review/dashboard base + seed Catan golden + valutazione iniziale.
**Exit**: Catan certificabile end-to-end, CI verde.

### Sprint 2 — Puerto Rico + tooling
Seed Puerto Rico + config soglie UI + override UI + ricalcolo massa + BGG importer UI + spike calibration + feature flag ON dev/staging.
**Exit**: 2 giochi + UI completa + audit trail.

### Sprint 3 — Mage Knight + stabilization
Seed Mage Knight + matching details UI + trend chart enriched + threshold recalibration + runbook ops + feature flag ON prod.
**Exit**: 3 giochi trend, almeno 1 Certified naturalmente, doc ADR soglie.

### Post-shipping backlog
- Performance/load test
- Bipartite optimal matching
- Page tolerance configurabile
- Override downgrade (NotCertified manuale)
- Auto-recompute on golden change
- Cross-language golden
- Project A consumption (catalog specchietti)

### Rollback
Revert migration + drop tabelle + feature flag OFF. Nessun impatto su M1.2 esistente (`LastMetricsId` NULL-tollerante).

## 10. Observability

### Prometheus metrics (`MeepleAiMetrics`)

- `mechanic_metrics_calculations_total{status, certification}` (counter)
- `mechanic_metrics_calculation_duration_seconds` (histogram)
- `mechanic_certifications_overridden_total` (counter)
- `mechanic_golden_claims_count{game_id}` (gauge)
- `mechanic_matching_engine_match_rate{section}` (gauge, ultima valutazione)

### Logs (`ILogger<T>`)

- INFO: calcolo start/end + overallScorePct + certificationStatus
- WARN: golden assente richiesta → 409
- ERROR: embedding service failure → fallire con 409 (no silent degradation)

## 11. Security & Audit

- Tutti endpoint `RequireAdminSessionFilter` (admin trusted)
- `IAuditLogger` entries per:
  - Override certification (admin + reason + timestamp)
  - Update thresholds (admin + before/after + timestamp)
  - Create/Update/Deactivate golden claim
  - Import BGG tags
- `MatchDetails` JSONB contiene snapshot decisioni matcher (audit forensico)

## 12. Configuration

### Backend `appsettings.json`
Nessuna nuova config app-level. Thresholds in DB singleton (DB-driven config).

### Frontend env
- `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED` (default `false`, gating UI)

### Seed data
- `certification_thresholds_config` singleton id=1, default 70/10/80/60 (via migration `INSERT ... ON CONFLICT DO NOTHING`)

## 13. References

- M1.2 parent: ISSUE-524 / ADR-051 `docs/adr/adr-051-mechanic-extractor.md`
- Embedding model: `intfloat/multilingual-e5-base` (768-dim) — MEMORY.md snapshot 2026-04-15 PR#413
- pgvector: `ivfflat lists=100` — coerente batch embedding RAG
- DeepSeek LLM: ADR-007
- CLAUDE.md CQRS rule: endpoints → `IMediator.Send()` only
- Exception mapping (NotFoundException/ConflictException): issue #2568
- DI rule (both interface + impl): issue #2565
- Pilot games manifest: `data/rulebook/manifest.json`

## 14. Open decisions deferred (not blockers)

1. **Recalibration automation**: se soglie vanno recalibrate trimestralmente, prevedere job schedulato vs. process manuale (Sprint 2+)
2. **Embedding fallback policy**: conferma in Sprint 1 che fail-on-error è preferito (no silent keyword-only mode)
3. **OverallScore weighted formula**: pesi `0.4/0.2/0.4` hardcoded — spostare in config post-shipping se feedback indica
4. **Project A wiring**: contract per esporre `WHERE CertificationStatus = Certified` al catalog specchietti — design separato
