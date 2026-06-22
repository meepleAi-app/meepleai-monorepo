# SP5 Admin KB — F3-FU-4 spin-outs design (#1673, #1674, #1675, #1676)

**Issues**: [#1673](https://github.com/meepleAi-app/meepleai-monorepo/issues/1673) · [#1674](https://github.com/meepleAi-app/meepleai-monorepo/issues/1674) · [#1675](https://github.com/meepleAi-app/meepleai-monorepo/issues/1675) · [#1676](https://github.com/meepleAi-app/meepleai-monorepo/issues/1676)
**Date**: 2026-06-01
**Last update**: 2026-06-02 — §3.3 #1675 expanded post brainstorm session: D-F/G/H/I added to decision log, OQ-3 closed, OQ-5 opened, KbQuality BC introduced
**Status**: SPEC ONLY — no implementation in this doc; produces 4 ready-for-plan designs (§3.3 #1675 ready-for-plan post 2026-06-02 update)
**Parent**: F3-FU-4 #1653 ✅ (closed) · design doc `2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md`
**Priority**: P3 (all 4)

---

## 1. Why a consolidated spec

All 4 spin-outs share the same root cause — **net-new backend foundation absent at FU-4 implementation time**. Consolidating the spec makes the foundation dependencies visible end-to-end and lets us prioritize the 4 issues against each other (and against unrelated KB work) with full information.

The 4 issues are NOT a single deliverable. Each lands as its own PR, on its own schedule. This doc establishes their data-model + API contracts so that:
- #1676 can ship FIRST (smallest foundation: DTO+data extension, no security surface)
- #1673 + #1675 can share an indexer-versioning column (one migration, two consumers)
- #1674 is gated on a security design review (Newman, FU-4 spec-panel) before any code

## 2. Foundation matrix

| Issue | Title | Foundation required | Risk |
|-------|-------|--------------------|------|
| **#1676** | Hero metadata enrichment | DTO extension + 5 new entity columns (3 on PdfDocument, 2 on TextChunk; all nullable, backfilled async) | Low — additive, no behavioral change |
| **#1673** | Re-index with version selector | Indexer-versioning column (`pdf_documents.indexer_version`) + version registry + admin selector | Medium — must guarantee re-runnability of historical versions |
| **#1675** | Per-doc quality eval | Per-doc eval pipeline (currently only global `/admin/rag-quality/report`) + result storage table | Medium — compute cost per eval run; storage growth |
| **#1674** | Per-doc embeddings viewer | Per-doc embeddings retrieval API + access control + view-mode strategy | **High — corpus reconstruction / data leak (Newman)** |

## 3. Per-issue design

### 3.1 #1676 — Hero metadata enrichment

**Mockup**: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb.html` L134, L146-152

#### Data audit (post-recon)

| Field | Status | Location |
|-------|--------|----------|
| Size | ✅ EXISTS | `PdfDocumentEntity.FileSizeBytes` (long, nullable) |
| Avg confidence | ❌ ABSENT | requires aggregation over `TextChunkEntity` chunk-level confidence (also absent) |
| Indexer version | ❌ ABSENT | requires new `PdfDocumentEntity.IndexerVersion` (see #1673 — shared column) |
| OCR flag | ❌ ABSENT | requires new `PdfDocumentEntity.OcrApplied` boolean |
| Chunk token size | ❌ ABSENT | requires new `TextChunkEntity.TokenCount` int |
| Last reindex | ⚠️ DERIVABLE | from `PdfDocumentEntity.UpdatedAt` IF reindex updates it (verify) |

#### Schema extension (proposed)

```csharp
// PdfDocumentEntity additive columns (nullable, no migration data movement)
public bool? OcrApplied { get; set; }            // null = unknown, true/false post-ingestion
public string? IndexerVersion { get; set; }      // e.g. "v1", "v2.semantic" — shared with #1673
public DateTime? LastReindexedAt { get; set; }   // distinct from UpdatedAt (which may bump on metadata edits)

// TextChunkEntity additive column
public int? TokenCount { get; set; }             // populated by chunking pipeline
public double? ConfidenceScore { get; set; }    // populated by language detection / OCR pipeline (already partial via LanguageConfidence on PDF level)
```

#### DTO extension

`KbDocDetailSchema` (`kb-chunks.schemas.ts:33`) → add `heroMetadata` block:

```typescript
heroMetadata: {
  fileSizeBytes: number | null;     // already wired, surface in hero
  avgChunkConfidence: number | null; // computed: AVG(TextChunks.ConfidenceScore) WHERE PdfDocumentId=docId
  indexerVersion: string | null;
  ocrApplied: boolean | null;
  avgChunkTokens: number | null;    // computed: AVG(TextChunks.TokenCount) WHERE PdfDocumentId=docId
  lastReindexedAt: string | null;   // ISO timestamp
}
```

#### Backfill strategy

- New columns are nullable; existing docs show "—" in the hero.
- Backfill job (separate PR after schema migration lands) populates historical data where computable:
  - `FileSizeBytes` from blob `Content-Length` or `ContentLength` (file system stat)
  - `OcrApplied` from existing language-detection logs if available
  - `TokenCount` — re-tokenize existing chunks (expensive; gate behind `--backfill-tokens` flag)
  - `IndexerVersion` — backfill to `"v0"` (pre-versioning marker)

#### Effort estimate

- Migration: 3 columns on `PdfDocumentEntity` (`OcrApplied`, `IndexerVersion`, `LastReindexedAt`) + 2 columns on `TextChunkEntity` (`TokenCount`, `ConfidenceScore`) → ~45 min
- DTO + endpoint: extend `GetKbDocDetailQuery` → +1 LEFT JOIN aggregate on `TextChunks` → ~1h
- FE wiring: 5 new fields in hero card → ~1h
- Tests: integration test for query + unit test for hero render → ~2h

**Total: ~4-5 hours** (smallest of the 4 spin-outs — ship FIRST).

---

### 3.2 #1673 — Re-index with version selector

**Mockup**: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb.html` L235 (`⟳ Re-index with version`)

#### Current state

`POST /api/v1/admin/pdfs/{docId}/reindex` re-indexes with the **current pipeline only**. There is no concept of an indexer version. A re-index against an older chunking strategy or embedding model is impossible.

#### Why this matters (Adzic — concrete scenario)

> Given Wingspan-Oceania-EN.pdf was ingested 2025-09 with `v1.0` (sentence-window chunking, e5-base embeddings),
> When the team ships `v2.0` (semantic chunking, e5-large embeddings, 2026-01),
> Then admins need to re-index against `v1.0` to A/B compare retrieval quality before global rollout — or to rollback a problematic `v2.0` deployment doc-by-doc.

#### Architecture

```csharp
// New value object
public sealed record IndexerVersion(string Version, string DisplayName, IndexerCapabilities Capabilities)
{
    public static IndexerVersion Current { get; } = new("v2.0", "v2.0 Semantic + e5-large", ...);
    public static IReadOnlyList<IndexerVersion> Registry { get; } = [v1_0, v1_5, v2_0];
}

// New endpoint
GET /api/v1/admin/indexer/versions → IndexerVersionRegistryDto[]

// Extended endpoint
POST /api/v1/admin/pdfs/{docId}/reindex
Body: { "indexerVersion": "v2.0" }  // optional — defaults to current
```

#### Re-runnability constraint (Newman)

A historical version is only valid if its pipeline is still executable. Two options:
1. **Code-resident registry**: each `IndexerVersion` enum value maps to a `IDocumentIndexerStrategy` impl. Removing an old version requires bumping a major version of the entire app.
2. **Container-tagged registry**: each version has a tagged Docker image. The reindex job spawns a pod with that image. Heavier infra, but allows old versions to coexist without code bloat.

**Recommendation**: option 1 for ≤3 versions (current strategy). Reassess at v4.

#### Data model

`pdf_documents.indexer_version` (NEW, shared with #1676). The handler reads `command.IndexerVersion ?? PdfDocument.IndexerVersion ?? IndexerVersion.Current` and writes the resolved version back to the entity on completion.

#### Audit

`[AuditableAction("DocumentReindex", "Document", Level=2)]` already on `ReindexDocumentCommand`. Extended payload includes `IndexerVersion` so operators can trace which version was used.

#### Effort

- New `IndexerVersion` value object + registry: ~2h
- Endpoint extension + command property: ~1h
- Strategy interface + 2 impls (v1, v2 stubs): ~4h (depends on actual divergence)
- FE: version dropdown + confirm: ~2h
- Tests: ~3h

**Total: ~12 hours.** Depends on #1676 column landing first.

---

### 3.3 #1675 — Per-doc quality eval

**Mockup**: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb.html` L238 (`🔬 Quality eval`)

**Brainstorm update 2026-06-02** (spec-panel + brainstorming session, 4 sezioni approvate): chiude le 4 critical gap **R-1/R-5/R-6/R-8** + **OQ-3** emerse nel spec-panel 2026-06-02. Tutte le decisioni hanno ID `D-F/G/H/I` nel decision log §5.

#### Current state

Global `GET /admin/rag-quality/report` returns corpus-wide metrics (precision@K, MRR, latency). No per-doc breakdown.

#### Use case (Cockburn)

Primary actor: **admin investigating a complaint** ("queries about Wingspan return wrong answers"). Today they can't tell whether the regression is in that doc's chunks, the model, or the query path. Per-doc eval lets them isolate.

#### Architecture

##### Bounded Context

`DocumentEvaluationRun` aggregate lives in **new BC `KbQuality`** (not in `KnowledgeBase`). Rationale: evolves independently from core KB retrieval; future cross-doc eval / A/B testing / RAG-quality dashboards converge here; parity with BC dimensionati come Gamification/UserLibrary.

```
apps/api/src/Api/BoundedContexts/KbQuality/
├── Domain/
│   ├── Evaluation/
│   │   ├── DocumentEvaluationRun.cs           // aggregate
│   │   ├── EvaluationStatus.cs                // enum
│   │   ├── EvaluationMetrics.cs               // 4-record composition (R-12)
│   │   └── QualityBand.cs                     // enum
│   └── Goldset/
│       ├── GoldsetVersion.cs                  // value object + registry (R-2)
│       └── GoldsetStrategy.cs                 // enum
├── Application/
│   ├── Commands/StartEvaluation/              // POST handler
│   ├── Queries/{GetEvaluation,ListEvaluations}/
│   ├── Services/
│   │   ├── IGoldsetGenerator.cs               // abstraction (LLM auto-gen impl)
│   │   ├── IEvaluationExecutor.cs             // runs queries against KB
│   │   └── IQualityBandResolver.cs            // config-driven banding (D-G)
│   └── Behaviors/EvalCostCapBehavior.cs       // MediatR pipeline (mirror ME M1.2)
├── Infrastructure/
│   ├── EvaluationRepository.cs                // EF Core repo
│   ├── Migrations/<TS>_AddKbQualityTables.cs  // 1 nuova table: document_evaluation_runs
│   ├── Adapters/
│   │   ├── KbSearchProviderAdapter.cs         // impl IKbSearchProvider (calls KnowledgeBase BC)
│   │   ├── PdfDocumentReadModelAdapter.cs     // impl IPdfDocReadModel (calls DocumentProcessing)
│   │   └── EvalCostBudgetCheckerAdapter.cs    // impl IEvalCostBudget (calls SystemConfiguration)
│   └── LlmGoldsetGenerator.cs                 // impl IGoldsetGenerator via LLM gateway
└── Routing/AdminKbQualityEndpoints.cs          // MapPost/MapGet using IMediator
```

Cross-BC ports & adapters:
- `IKbSearchProvider` ← KnowledgeBase
- `IPdfDocumentReadModel` ← DocumentProcessing
- `IEvalCostBudgetChecker` ← SystemConfiguration
- `IAuditLogger` ← Administration (audit Level=2)

##### Aggregate (R-2, R-8, R-12)

```csharp
namespace Api.BoundedContexts.KbQuality.Domain.Evaluation;

public sealed class DocumentEvaluationRun
{
    public Guid Id { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public EvaluationStatus Status { get; private set; }
    public string GoldsetVersion { get; private set; }       // R-2 — enum-backed registry
    public long GoldsetGenerationSeed { get; private set; }  // R-8 — deterministic re-runs
    public EvaluationMetrics? Metrics { get; private set; }  // R-12 — composed record
    public decimal? CostUsd { get; private set; }            // D-H — actual cost tracked
    public Guid TriggeredByAdminId { get; private set; }     // R-6 audit
    public string? ErrorMessage { get; private set; }

    public static DocumentEvaluationRun Create(Guid docId, string goldsetVersion, Guid triggeredBy, long? reuseSeed)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = docId,
            GoldsetVersion = goldsetVersion,
            GoldsetGenerationSeed = reuseSeed ?? unchecked((long)Random.Shared.NextInt64()),
            TriggeredByAdminId = triggeredBy,
            StartedAt = DateTime.UtcNow,
            Status = EvaluationStatus.Pending,
        };
    }
}

public enum EvaluationStatus { Pending, GoldsetGenerating, Running, Completed, Failed, RateLimited, CostCapped }
```

##### GoldsetVersion value object + registry (R-2, parity con IndexerVersion #1673)

```csharp
public sealed record GoldsetVersion(string Version, string DisplayName, GoldsetStrategy Strategy)
{
    public static GoldsetVersion AutoCurrent { get; } = new("auto-v1", "Auto LLM v1", GoldsetStrategy.LlmAutoGen);
    public static IReadOnlyList<GoldsetVersion> Registry { get; } = [AutoCurrent];
    // Fase 2 (post D-F trigger): Manual + Feedback strategies aggiunte al registry
    // Retention SLA: 18 months post-supersession (parity OQ-2 #1673)
}

public enum GoldsetStrategy { LlmAutoGen, Manual, Feedback }
```

##### Metrics decomposition (R-12)

```csharp
public sealed record PrecisionMetrics(double At1, double At3, double At5);
public sealed record RankingMetrics(double Mrr);
public sealed record LatencyMetrics(TimeSpan P50, TimeSpan P95);
public sealed record EvaluationMetrics(
    PrecisionMetrics Precision,
    RankingMetrics Ranking,
    LatencyMetrics Latency,
    int QueryCount,
    decimal CostUsd,
    QualityBand QualityBand   // D-G — derived from QualityBands config
);

public enum QualityBand { Red, Yellow, Green }
```

##### Endpoints (R-13 RESTful + R-11 evolution path)

```
POST   /api/v1/admin/kb/docs/{docId}/evaluations
       Body: { "goldsetVersion"?: "auto-v1", "overrideCostCap"?: false }
       → 202 Accepted + Location: /evaluations/{id} + headers:
           X-RateLimit-Remaining, X-RateLimit-Reset,
           X-Cost-Cap-Remaining, X-Cost-Cap-Estimate
       → 429 Too Many Requests (rate limit, Scenario B)
       → 402 Payment Required (cost cap, Scenario C)
       → 400 Bad Request (invalid GoldsetVersion, Scenario E)

GET    /api/v1/admin/kb/docs/{docId}/evaluations
       Query: ?page=1&pageSize=20&sort=-startedAt
       → 200 OK { items: EvaluationRunListItem[], totalCount, page, pageSize }

GET    /api/v1/admin/kb/docs/{docId}/evaluations/{evaluationId}
       → 200 OK { ...detail with full Metrics + Goldset Q&A pairs }
       → 423 Locked if Status=Pending|Running|GoldsetGenerating
```

#### Audit & Compliance (R-6, R-7)

```csharp
[AuditableAction("DocumentEvaluationTriggered", "Document", Level=2)]
public sealed record StartEvaluationCommand(Guid DocId, string? GoldsetVersion, bool OverrideCostCap)
    : IRequest<EvaluationStartedResult>;
```

Audit payload (pre-handle):
```
{ DocId, GoldsetVersion, GoldsetGenerationSeed, EstimatedCostUsd, CostCapRemaining, TriggeredByAdminId }
```

Audit payload (terminal status, separate Level=1 event `DocumentEvaluationCompleted`):
```
{ EvaluationId, Status, ActualCostUsd, MetricsSummary, ElapsedMs }
```

**Retention 18m**: nuovo background job `KbQualityRetentionJob` (Hangfire/IHostedService pattern, ref #941 unify async queue). Cron `0 3 * * *` (daily 03:00 UTC), query `DELETE FROM document_evaluation_runs WHERE CompletedAt < NOW() - INTERVAL '18 months'`. Audit aggregato (count deleted, no per-record) per evitare audit growth unbounded.

#### Determinism & Re-runnability (R-8)

**Seed pinning obbligatorio**:
- `GoldsetGenerationSeed: long` non-nullable, default `unchecked((long)Random.Shared.NextInt64())` at run creation
- Goldset auto-gen consumes seed → LLM gateway invocation prompt includes `seed=N`
- Modelli con seed support: OpenAI (`seed` param), Anthropic Claude (`temperature=0` + `seed`). DeepSeek: seed documented ma variance accettata.

**Re-run logic**: `POST /evaluations` con `(docId, goldsetVersion)` invariante entro 24h → riusa ultimo seed (`OrderByDescending(StartedAt).First().GoldsetGenerationSeed`). Altrimenti nuovo seed.

**Test contract**:
```csharp
[Fact]
public async Task Eval_SameDoc_SameGoldset_SameSeed_ProducesDeterministicMetrics()
{
    var run1 = await ExecEval(docId, "auto-v1", seed: 42);
    var run2 = await ExecEval(docId, "auto-v1", seed: 42);
    run1.Metrics.Precision.At5.Should().BeApproximately(run2.Metrics.Precision.At5, 0.001);
}
```

#### Cost cap + Rate limit (D-H mirror Mechanic Extractor M1.2, ADR-051)

```csharp
public sealed class EvalCostCapBehavior<TRequest, TResponse>(
    IEvalCostBudgetChecker budget,
    IEvaluationCostEstimator estimator,
    ICurrentUserService user,
    IOptions<EvalQualityOptions> options
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : StartEvaluationCommand
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var estimated = await estimator.EstimateAsync(request.DocId, ct);
        var remaining = await budget.GetRemainingAsync(user.TenantId, ct);

        if (estimated > remaining && !request.OverrideCostCap)
            throw new CostCapExceededException(estimated, remaining);

        if (request.OverrideCostCap && !user.HasPermission("OverrideEvalCostCap"))
            throw new ForbiddenException("OverrideEvalCostCap permission required");

        var result = await next();  // run eval

        await budget.IncrementSpentAsync(user.TenantId, actualCost, ct);
        return result;
    }
}
```

**Rate limit**: `EvalRateLimitBehavior` keyed `(docId, adminId)`, 1 eval/doc/10min **sliding window** (computed da `MAX(StartedAt) WHERE docId=X AND adminId=Y AND StartedAt > NOW() - 10min`), headers `X-RateLimit-Remaining`/`X-RateLimit-Reset`. 429 con `Retry-After`.

**Cost cap reset semantics**: per-tenant monthly cap si resetta su **calendar month boundary** (primo del mese 00:00 UTC), NOT rolling 30 days. Counter persistito su `system_config` (key `EvalQuality:Spent.{tenantId}.{yyyy-MM}`). Background job `KbQualityCostCapResetJob` (cron `0 0 1 * *`) garantisce la pulizia counter dei mesi precedenti (parity con ME M1.2 pattern).

#### Quality bands config (D-G)

```yaml
EvalQuality:
  MonthlyCostCap: 50.00              # USD per tenant (D-H default)
  RateLimitPerDocMinutes: 10
  RetentionMonths: 18                # R-7
  QualityBands:
    precisionAt5:
      red:    { max: 0.40 }
      yellow: { min: 0.40, max: 0.70 }
      green:  { min: 0.70 }
    mrr:
      red:    { max: 0.30 }
      yellow: { min: 0.30, max: 0.60 }
      green:  { min: 0.60 }
    latencyP95Ms:
      green:  { max: 30000 }
      yellow: { min: 30000, max: 60000 }
      red:    { min: 60000 }
```

Bands resolved via `IQualityBandResolver` injecting `IOptionsMonitor<EvalQualityOptions>` (config hot-reload via `ConfigReloadOnChange`). UI mostra band overall = `max(severità per ciascuna metric)`. Bands calibrate manualmente su 3-5 docs noti; adjustable via config senza redeploy.

**Interval boundary semantics** (resolve ambiguity): right-exclusive intervals — `precisionAt5 = 0.40` esatto cade in `yellow` (i.e. `red = [0, 0.40)`, `yellow = [0.40, 0.70)`, `green = [0.70, 1.0]`). Stessa convention per `mrr`. Per `latencyP95Ms` la severity è invertita (più basso = meglio): `green = [0, 30_000)`, `yellow = [30_000, 60_000)`, `red = [60_000, +∞)`.

#### Goldset Fase 2 trigger (D-F)

`GoldsetVersion.Registry` Fase 1 contains only `AutoCurrent`. Fase 2 trigger oggettivo:
- **≥3 docs con eval ricorrente >5/mese** AND
- **variance precision@5 >0.10 fra run successivi**

Quando trigger fires (monitorato via dashboard Prometheus + alert), apre **epic separato** per UI curation + contributor workflow. Versioni `Manual` + `Feedback` aggiunte al registry. Soglia variance 0.10 è placeholder calibrare post-ship (vedi OQ-5).

#### Acceptance criteria — Scenari Given/When/Then (R-4)

**Scenario A — Cold start (first eval per doc)**
```
Given Wingspan-EN.pdf has 234 chunks, no prior eval runs
When admin POST /api/v1/admin/kb/docs/{id}/evaluations  body: {}
Then 202 Accepted + Location: /evaluations/{id} + Status=GoldsetGenerating
And within 30s: GoldsetGenerationSeed pinned, 15 Q&A pairs generated (3 per top-5 chunks)
And within 90s: Status=Completed, EvaluationMetrics populated
And QualityBand derived from EvalQuality:QualityBands config (red|yellow|green)
And audit event "DocumentEvaluationTriggered" Level=2 with full payload emitted
```

**Scenario B — Rate limit hit**
```
Given doc has 1 completed run within last 10 min
When admin POST /evaluations on same doc
Then 429 Too Many Requests + X-RateLimit-Reset header + Retry-After
And no audit event emitted (rate-limited request not counted)
```

**Scenario C — Cost cap exceeded (default reject)**
```
Given tenant has consumed $49.50 of $50.00 monthly cap (EvalQuality:MonthlyCostCap=50)
When admin POST /evaluations (estimator = $0.60)
Then 402 Payment Required
And body: { estimated: 0.60, remaining: 0.50, hint: "Set overrideCostCap=true with OverrideEvalCostCap permission" }
And no DocumentEvaluationRun created
```

**Scenario C2 — Cost cap override (with permission)**
```
Given same as C, AND admin has OverrideEvalCostCap permission
When admin POST /evaluations  body: { overrideCostCap: true }
Then 202 Accepted, run created
And audit event includes warning flag "CostCapOverridden=true" + actualCostUsd post-completion
```

**Scenario D — Re-run within 24h reuses seed (R-8 determinism)**
```
Given doc has run R1 with seed=42 completed 2h ago, goldsetVersion=auto-v1
When admin POST /evaluations  body: { goldsetVersion: "auto-v1" }
Then new run R2 created with GoldsetGenerationSeed=42 (reused from R1)
And after Status=Completed:
  R2.Metrics.Precision.At5 should equal R1.Metrics.Precision.At5 ± 0.05
  (variance threshold per LLM-judge non-determinism, models without seed support)
```

**Scenario E — Fase 2 not yet triggered (D-F gating)**
```
Given GoldsetVersion.Registry contains only [AutoCurrent]
When admin POST /evaluations  body: { goldsetVersion: "manual-v1" }
Then 400 Bad Request
And body: { error: "InvalidGoldsetVersion", message: "Goldset 'manual-v1' not registered. Available: ['auto-v1']" }
```

#### Test pyramid (R-9)

| Layer | Count | Effort | Focus |
|---|---:|---:|---|
| **Unit** | 4 | 1h | `EvaluationMetricsCalculator` math (precision@K, MRR formulas), `QualityBandResolver` threshold boundary, `GoldsetVersionRegistry` validation |
| **Integration (Testcontainers Postgres + WireMock LLM)** | 8 | 3h | Cold start (A), Rate limit (B), Cost cap reject (C), Override w/ perm (C2), Seed reuse (D), Fase 2 gating (E), Audit Level=2 payload assertion, Retention job deletion 18m |
| **E2E (Playwright admin)** | 1 | 1h | Happy-path UI: trigger → wait → see metrics + band chip in detail panel |
| **Total** | **13** | **~5h** | |

#### Effort revisione

| Layer | Spec original | Post brainstorm 2026-06-02 | Δ |
|---|---:|---:|---|
| Domain/Aggregate | 2h | 3h | +1h (R-2 + R-12 + cross-BC adapters) |
| Eval handler + goldset auto-gen | 6h | 7h | +1h (R-8 seed pinning + R-11 evolution body) |
| Endpoints + admin auth | 3h | 4h | +1h (R-13 + R-6 audit + 429/402 status semantics) |
| Cost cap behavior | 0h | 2h | +2h (D-H mirror ME M1.2) |
| Quality bands resolver + config | 0h | 1h | +1h (D-G infrastructure) |
| Retention job | 0h | 1h | +1h (R-7 background job 18m) |
| FE: trigger + history + run-detail | 5h | 6h | +1h (cost cap UI + band chip + override prompt) |
| Tests | 4h | 5h | +1h (R-9 expanded pyramid) |
| **Total** | **20h** | **29h** | **+9h** |

**Defer until #1676/#1673 land** (entrambi shipped 2026-06-02 ✅ via PR #1792 + #1800).

---

### 3.4 #1674 — Per-doc embeddings viewer (⚠️ SECURITY REVIEW REQUIRED)

**Mockup**: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb.html` L236 (`📋 View embeddings`)

#### Risk statement (Newman, FU-4 spec-panel 2026-05-29)

Exposing **raw per-doc vectors** enables **corpus reconstruction** by a malicious admin (or by an actor who compromises an admin account). Embedding vectors are essentially lossy text representations — with a known embedding model and enough vectors, an attacker can recover ~70-90% of the original text via embedding inversion attacks (Morris et al., 2023; Pan et al., 2024).

**Source documents** in MeepleAI's KB include **copyrighted board game rulebooks**. Reconstruction → distribution = IP violation by the platform.

#### Mitigation strategies (must pick BEFORE building UI)

| # | Strategy | Pros | Cons |
|---|----------|------|------|
| **M1** | **Refuse the feature** | Zero risk | Loses debugging utility for legitimate use cases (vector-quality investigation) |
| **M2** | **Aggregated views only** — show distribution histograms, dimension stats, similarity heatmap to peer chunks. NO raw vectors. | Low risk | Limited debugging utility |
| **M3** | **Quantized 8-bit vectors** — round to int8 before display, losing ~75% precision. Inversion attacks degrade ~50%. | Some utility for shape inspection | Still partial reconstruction possible |
| **M4** | **Raw vectors, audit + step-up auth** — display raw float32 vectors, but require step-up 2FA (S3 strict mode) + write a `VectorViewed` audit entry with vector ID, viewer, timestamp. | Full utility | Audit trail mitigates breach detection, NOT prevention. Compromised admin = full corpus leak. |
| **M5** | **Owner-only access** — vector view limited to the upload's owner (the user who originally uploaded the doc), not all admins. | Reduces blast radius | Admin workflow degraded (cross-doc debugging blocked) |

**Recommended**: **M2 (aggregated views)** as a first cut. If M2 proves insufficient for debugging, escalate to M4 with mandatory step-up auth + per-action audit.

#### Aggregated view spec (M2 — recommended starter)

```typescript
// Per-doc embedding summary (no raw vectors)
type EmbeddingSummary = {
  vectorDocumentId: string;
  chunkCount: number;
  embeddingDimension: number;        // e.g. 768 for e5-base, 1024 for e5-large
  modelVersion: string;              // "e5-base-v1.0"
  perDimensionStats: Array<{         // truncated to first 8 dimensions for UI
    dimensionIndex: number;
    mean: number;
    stdDev: number;
    p50: number;
    p95: number;
  }>;
  intraDocSimilarityDistribution: {  // chunk-to-chunk cosine similarity within the doc
    p50: number;
    p95: number;
    histogram: Array<{ bucket: number; count: number }>;  // 10 buckets [-1, 1]
  };
  nearestNeighborsPreview: Array<{   // closest 5 chunks in the corpus (excluding self)
    chunkId: string;
    similarity: number;
    fromSameDoc: boolean;
  }>;
};
```

This gives admins:
- "Is this doc's embeddings shape healthy?" (per-dimension stats — flag dead dimensions)
- "Are chunks too similar to each other?" (intra-doc distribution — flag over-fragmentation)
- "What does the corpus think this doc is most like?" (NN preview — flag misclassification)

**Without exposing the vectors themselves.**

**Residual risk acknowledgment (spec-panel review 2026-06-01)**: `nearestNeighborsPreview` returns cross-doc `chunkId` + similarity scores. This exposes partial corpus topology — repeated probing across docs builds a similarity graph of the corpus. Risk is **meaningfully lower than M3/M4** (no raw vectors → embedding-inversion attacks still require the embedding model running independently, which an external attacker doesn't have), but non-zero. Mitigations to consider at implementation time: (1) cap NN preview to same-doc-only (drop `fromSameDoc:false` results), (2) apply k-anonymity by aggregating NN clusters instead of individual chunks, (3) add rate limit per resource (5 NN previews per doc per hour per admin). Recommendation: ship M2 with cap #1 (same-doc-only) as the default and revisit if cross-doc visibility proves essential.

#### Authorization (regardless of M2/M3/M4 choice)

- Endpoint: `GET /api/v1/admin/kb/docs/{docId}/embeddings/summary` (M2 shape)
- Auth: admin/superadmin + step-up token (S3 strict mode)
- Audit: `[AuditableAction("DocumentEmbeddingsViewed", "Document", Level=3)]` (Level=3 = sensitive read)
- Rate limit: 10 views/hour/admin (prevent scraping)

#### Effort

- Spec review with security-engineer agent: ~2h (REQUIRED before code)
- Aggregated summary endpoint (M2): ~6h
- Step-up auth integration: ~3h (reuse S3 pattern)
- FE: stats display + histogram chart + NN list: ~6h
- Tests + audit verification: ~4h

**Total: ~21 hours** if M2. Add ~10h if escalating to M4 with raw vector display.

---

## 4. Cross-issue dependencies

```
#1676 (hero metadata)
   │
   └─ adds PdfDocumentEntity.IndexerVersion column
            │
            ├─ #1673 (version selector) reads/writes this column
            └─ #1675 (per-doc eval) stores `goldsetVersion` analogously

#1674 (embeddings viewer) — independent foundation, gated on security review

Suggested order:
1. #1676 — small, additive, unblocks indexer-versioning column for #1673/#1675
2. #1673 — depends on #1676 column
3. #1675 — independent of #1673, can run in parallel after #1676
4. #1674 — last; requires explicit security sign-off before scoping
```

## 5. Decision log (this spec)

| ID | Decision | Rationale |
|----|----------|-----------|
| D-A | #1676 first | Smallest foundation; unblocks #1673/#1675 column |
| D-B | #1673 strategy registry = code-resident (Option 1) | ≤3 versions = no need for container infra |
| D-C | #1675 goldset = LLM-generated (Option 3) | Originally Option 2 (auto-smoke from feedback) but flipped post-spec-panel review (PR #1790) — `KbUserFeedback` has no `PdfDocumentId`/citation link, so Option 2 has net-new infra cost ≥ Option 3 |
| D-D | #1674 starter = M2 aggregated views | Lowest risk; covers ~80% of debugging use cases |
| D-E | All 4 issues retain P3 priority | No business demand surfaced; foundational work for future deep-dive admin debug workflows |
| **D-F** | #1675 ownership Fase 1 = tooling-only LLM auto-gen (D-C). Fase 2 trigger oggettivo: ≥3 docs con eval ricorrente >5/mese AND variance precision@5 >0.10 fra run successivi → apre epic separato per UI curation + contributor workflow | Parity con OQ-4 (#1674) "measurable signal" pattern; zero ops Fase 1; evolution data-driven (chiude OQ-3 brainstorm 2026-06-02) |
| **D-G** | #1675 success criteria: run-level SMART hard ("completa <60s p95 per docs ≤500 chunks; cost <$0.05/eval") + metric-level qualitative bands (red/yellow/green) via `EvalQuality:QualityBands` config calibrate manualmente su 3-5 docs noti | YAGNI sulla baseline qualitativa (non esistente); config-driven evolve senza redeploy; pattern coerente con `FeatureFlags:Tiers` (chiude R-1 brainstorm 2026-06-02) |
| **D-H** | #1675 cost cap = mirror Mechanic Extractor M1.2 (ADR-051): per-tenant monthly cap `EvalQuality:MonthlyCostCap` (default $50/mo) + admin `OverrideEvalCostCap` permission + UI pre-trigger estimate + audit | Reuse `CostCapBehavior` MediatR pipeline pattern; admin workflow già onboardato; ADR-051 precedent (chiude R-5 brainstorm 2026-06-02) |
| **D-I** | #1675 aggregate vive in **new BC `KbQuality`**, NOT in `KnowledgeBase` BC | Evolves independently from core KB retrieval; future cross-doc eval + A/B testing + RAG-quality dashboards converge here; parity con BC dimensionati come Gamification/UserLibrary (chiude R-10 brainstorm 2026-06-02) |

## 6. Open questions (deferred to plan phase per issue)

- **OQ-1 (#1676)**: backfill `OcrApplied` retroactively from logs, or accept "—" for historical docs?
- **~~OQ-2 (#1673)~~** ✅ ANSWERED 2026-06-01 (spec-panel review of PR #1790): historical indexer version deprecation owner = **#1673 implementation PR author + reviewer** (set at code-review time); retention SLA = **18 months post-supersession by a newer version** (matches MeepleAI's broader API deprecation policy). Rationale: without an explicit deprecation policy the code-resident registry grows unbounded and forces the container-registry migration D-B explicitly wants to avoid. The 18m window lets at-rest historical re-indexes (compliance, audit replay) cover typical retention needs without bloating the strategy registry past the ≤3-version assumption.
- **~~OQ-3 (#1675)~~** ✅ ANSWERED 2026-06-02 (brainstorm session): Fase 1 = tooling-only LLM auto-gen (D-F); Fase 2 trigger oggettivo data-driven (D-F) → epic separato per contributor workflow quando trigger fires. Rationale parity con OQ-4 "measurable signal".
- **OQ-4 (#1674)**: if M2 proves insufficient, what's the threshold to escalate to M4? Define measurable signal (e.g. "≥3 admin debug sessions per month blocked by lack of raw vectors").
- **OQ-5 (#1675)** ⚠️ NEW: variance threshold 0.10 in D-F Fase 2 trigger è placeholder. Calibrate post-ship con 30gg di dati reali (variance osservata su 3-5 docs noti tra run successivi con goldset auto-v1). Acceptance signal: se 90% dei docs ha variance <X, X-buffer è la soglia calibrata.

## 7. Out of scope (do NOT include in any of the 4)

- Cross-doc bulk operations (re-index N docs, eval batch, compare embeddings) → separate epic
- Public/user-facing display of any of the data above → strictly admin
- Real-time streaming eval (#1675 is async only)

## 8. References

- FU-4 spec: `docs/superpowers/specs/2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md`
- FU-4 implementation: PR #1653 (merged 2026-05-30, addendum sections describe the exact persistence model the spin-outs reference)
- spec-panel review 2026-05-29 (Newman risk identification for #1674)
- embedding inversion attack reference: Morris, Kuleshov, Shmatikov, Rush (2023), "Text Embeddings Reveal (Almost) As Much As Text", ACL 2023 — establishes baseline that ~70-90% text recovery is possible from embedding vectors when the embedding model is known to the attacker. Additional academic references should be sourced and verified at the time #1674 enters plan-phase (do not pre-cite uncited authors in this design doc).
