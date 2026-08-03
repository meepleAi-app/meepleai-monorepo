# RAG Golden Evaluation Set (#3467)

**Status**: active · **Epic**: #3397 (in-session agent grounding) · **Unblocks**: #3390 (live-path enhancement wiring)

The golden evaluation set is the **gate** the in-session live retrieval path must pass before any RAG
enhancement (AdaptiveRouting / CRAG / RAPTOR / RAG-Fusion / Graph) is wired onto it in #3390. It exists
so grounding quality is a **measured invariant**, not an unverified side-effect of turning enhancements on.

## The 5 golden games

Catan · Wingspan · Dominion · Ark Nova · 7 Wonders.

Dataset: [`tests/evaluation-datasets/meepleai-en-seed.json`](../../../tests/evaluation-datasets/meepleai-en-seed.json)
(EN, ≥30 samples, all 5 games). `relevant_chunk_ids` are intentionally empty pending the #3427 re-index —
recall labeling is applied later via the labeling-assist workflow (chunk ids churn on re-index). Page-level
`expected_citations` are authored **now** because pages are stable across re-index.

## Sample schema (citation ground truth)

Each sample follows `EvaluationSample`; the citation-relevant field (mirrors
[`tests/llm-eval/golden-set/schema.md`](../../../tests/llm-eval/golden-set/schema.md)):

```jsonc
"expected_citations": {
  "primary_pages": [7],                 // ground-truth page(s) where the answer lives
  "match_policy": "overlap_at_least_one" // exact | overlap_at_least_one | subset | superset
}
```

| `match_policy` | Passes when the answer's actual cited pages… |
| --- | --- |
| `exact` | equal the expected set exactly (order-independent) |
| `overlap_at_least_one` | intersect the expected set (**pragmatic default**) |
| `subset` | are a subset of expected (no extra pages cited) |
| `superset` | are a superset of expected (all expected pages present) |

Use `exact` with an empty `primary_pages` for "no-answer expected" edge cases. A sample **without**
`expected_citations` is not citation-graded (it is excluded from the CitationAccuracy aggregate).

## Metrics

Computed by `DatasetEvaluationService` → `EvaluationMetrics`, reported by `EvaluationReportFormatter`
(Markdown + snake_case JSON: `recall_at_5/10`, `ndcg_at_10`, `mrr`, `answer_correctness`,
`citation_accuracy`, `citation_structural_validity`, `cited_sample_count`, `p95_latency_ms`, coverage).

- **Recall@5 / @10, nDCG@10, MRR** — retrieval quality over recall-labeled samples (unlabeled excluded).
- **Citation accuracy** — page-level: actual cited pages (from `InlineCitationMatcherService`) vs
  `expected_citations` per `match_policy`, over citation-graded samples only.
- **Citation structural validity** — trust floor: fraction of the response's citations that are well-formed
  (`PDF:guid` + existing document + page in range), via `CitationValidationService`.
- **Answer correctness** — deterministic keyword / word-overlap heuristic (NOT an LLM-as-judge; kept
  keyword-based for CI stability).

## Thresholds (the #3390 per-enhancement gate)

Encoded on `EvaluationMetrics` (`MeetsPhase5Target`, `MeetsCitationAccuracyTarget`). No enhancement wired
in #3390 may regress the enhancement-free baseline below:

| Gate | Threshold |
| --- | --- |
| Recall@10 | ≥ 0.70 |
| Citation accuracy (`overlap_at_least_one`, page-level) | ≥ 0.80 |
| P95 latency | < 1500 ms |
| Web calls from the live path (rulebook domain) | **0** (permanent invariant) |

> The 0.70 / 0.80 thresholds are pragmatic, aligned with the in-code Phase-5 target; confirm against the
> first real baseline after the #3427 re-index. If the real baseline is lower, the eval-set still serves its
> purpose: **measure** and **prevent regression**.

## CRAG web-fallback is OFF on the closed rulebook domain

On the closed rulebook domain, a web "corrective" fallback **worsens** fidelity. The contract:

- The live path uses `RetrievalPolicy.LiveSession` (`EnhancementsEnabled = false`) → `activeEnhancements = None`
  → the CRAG block in `RagPromptAssemblyService` **does not run**. Guarded by
  `RagPromptAssemblyEnhancementsTests.WhenLiveSessionPolicy_CragEvaluatorNeverInvoked`.
- When CRAG *is* active (non-live path), the "corrective" requery stays **internal** to the corpus
  (`ExpandQueryAsync` + `TryHybridSearchAsync`); it never routes to a web plugin. The web-fallback plugins
  (`RetrievalWebPlugin`, `EvaluationCragPlugin`, which only *simulate* `web_search`) are **not** dependencies
  of the live prompt-assembly path. Guarded by
  `RagPromptAssemblyEnhancementsTests.RagPromptAssemblyService_HasNoWebRetrievalDependency` — if #3390 ever
  wires a web plugin here, that test goes RED, forcing an explicit new flag that keeps the rulebook domain OFF.
- **Runtime kill-switch**: feature flag `rag.enhancement.crag-evaluation` (`FeatureFlagSeeder`) disables all
  of CRAG without a redeploy (note: this also disables the harmless internal requery, not only a web fallback).

## Workflow & tooling

Admin-gated endpoints (CQRS via `IMediator`), plus Makefile wrappers (local ad-hoc smoke; a real staging run
is deferred until the #3427 re-index):

| Step | Endpoint | Makefile |
| --- | --- | --- |
| Run retrieval eval | `POST /api/v1/admin/eval/retrieval` | `make eval-retrieval DATASET=…` |
| AI-propose labels | `POST /api/v1/admin/eval/labeling-candidates` | — |
| Merge reviewed labels (persist) | `POST /api/v1/admin/eval/merge-labels` | `make eval-merge-labels DATASET=… REVIEW=…` |

Recall labeling (`relevant_chunk_ids`) and a committed baseline report under
`docs/for-developers/evaluation-reports/` are completed **after** the #3427 re-index (chunk ids are unstable
before it). Path sandboxing of `datasetPath` / `outputPath` is deferred to #3438.
