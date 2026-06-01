# SP5 Admin KB — F3-FU-4 spin-outs design (#1673, #1674, #1675, #1676)

**Issues**: [#1673](https://github.com/meepleAi-app/meepleai-monorepo/issues/1673) · [#1674](https://github.com/meepleAi-app/meepleai-monorepo/issues/1674) · [#1675](https://github.com/meepleAi-app/meepleai-monorepo/issues/1675) · [#1676](https://github.com/meepleAi-app/meepleai-monorepo/issues/1676)
**Date**: 2026-06-01
**Status**: SPEC ONLY — no implementation in this doc; produces 4 ready-for-plan designs
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

#### Current state

Global `GET /admin/rag-quality/report` returns corpus-wide metrics (precision@K, MRR, latency). No per-doc breakdown.

#### Use case (Cockburn)

Primary actor: **admin investigating a complaint** ("queries about Wingspan return wrong answers"). Today they can't tell whether the regression is in that doc's chunks, the model, or the query path. Per-doc eval lets them isolate.

#### Architecture

```csharp
// New aggregate: per-doc eval run
public sealed class DocumentEvaluationRun
{
    public Guid Id { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public EvaluationStatus Status { get; private set; }
    public string? GoldsetVersion { get; private set; }   // "wingspan-goldset-v1"
    public EvaluationMetrics? Metrics { get; private set; } // precision@1/3/5, MRR, latency p50/p95
    public string? ErrorMessage { get; private set; }
}

// Storage: document_evaluation_runs table

// Endpoints
POST /api/v1/admin/kb/docs/{docId}/eval → kicks off async run, returns runId
GET /api/v1/admin/kb/docs/{docId}/eval/runs → history
GET /api/v1/admin/kb/docs/{docId}/eval/runs/{runId} → detail
```

#### Goldset dependency

A per-doc eval needs a per-doc **goldset** (gold-standard Q&A pairs). MeepleAI today has corpus-wide goldsets only. Options:

1. Tag existing goldset entries with `pdfDocumentId` (manual curation effort)
2. Auto-derive a "smoke" goldset from existing user feedback (chat threads with thumbs-up + citations from this doc) — **⚠️ requires net-new infrastructure**: `KbUserFeedback` (`KbUserFeedback.cs:11-19`) today captures only `Outcome` keyed to `MessageId + ChatSessionId + GameId` — NO `PdfDocumentId` link, NO citation storage on the feedback entity. Resolving which doc a "helpful" message cited requires joining feedback → message → citation → chunk → pdf, none of which is persisted on the feedback path. Implementation gates on adding `PdfDocumentId[]` (or citation ref) to `KbUserFeedback` first.
3. LLM-generated goldset (chunk → 3 Q&A pairs per chunk) — fastest, lowest quality, NO infrastructure dependency

**Recommendation**: ship with **option 3 (LLM-generated)** as the actual minimal-friction starter (option 2 was the originally-planned starter but the citation-linkage prerequisite makes it heavier than #1675 itself). Curate option 1 entries over time once #1675 has shipped + admins identify high-value docs. **Decision flipped 2026-06-01** post-spec-panel review of PR #1790.

#### Compute cost

Each eval run executes N queries (5-20) against the doc's chunks. At LLM-llama-3-8b cost ~$0.001/query → ~$0.02/eval. Negligible — but rate-limit to 1 eval per doc per 10 min to avoid abuse.

#### Effort

- New aggregate + migration: ~2h
- Eval handler + goldset auto-derivation: ~6h
- 3 endpoints + admin auth: ~3h
- FE: trigger button + history list + run-detail panel: ~5h
- Tests: ~4h

**Total: ~20 hours** (largest of the 4). Defer until #1676/#1673 land.

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

## 6. Open questions (deferred to plan phase per issue)

- **OQ-1 (#1676)**: backfill `OcrApplied` retroactively from logs, or accept "—" for historical docs?
- **~~OQ-2 (#1673)~~** ✅ ANSWERED 2026-06-01 (spec-panel review of PR #1790): historical indexer version deprecation owner = **#1673 implementation PR author + reviewer** (set at code-review time); retention SLA = **18 months post-supersession by a newer version** (matches MeepleAI's broader API deprecation policy). Rationale: without an explicit deprecation policy the code-resident registry grows unbounded and forces the container-registry migration D-B explicitly wants to avoid. The 18m window lets at-rest historical re-indexes (compliance, audit replay) cover typical retention needs without bloating the strategy registry past the ≤3-version assumption.
- **OQ-3 (#1675)**: who curates per-doc goldsets long-term? Tooling-only or part of contributor workflow?
- **OQ-4 (#1674)**: if M2 proves insufficient, what's the threshold to escalate to M4? Define measurable signal (e.g. "≥3 admin debug sessions per month blocked by lack of raw vectors").

## 7. Out of scope (do NOT include in any of the 4)

- Cross-doc bulk operations (re-index N docs, eval batch, compare embeddings) → separate epic
- Public/user-facing display of any of the data above → strictly admin
- Real-time streaming eval (#1675 is async only)

## 8. References

- FU-4 spec: `docs/superpowers/specs/2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md`
- FU-4 implementation: PR #1653 (merged 2026-05-30, addendum sections describe the exact persistence model the spin-outs reference)
- spec-panel review 2026-05-29 (Newman risk identification for #1674)
- embedding inversion attack reference: Morris, Kuleshov, Shmatikov, Rush (2023), "Text Embeddings Reveal (Almost) As Much As Text", ACL 2023 — establishes baseline that ~70-90% text recovery is possible from embedding vectors when the embedding model is known to the attacker. Additional academic references should be sourced and verified at the time #1674 enters plan-phase (do not pre-cite uncited authors in this design doc).
