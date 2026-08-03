# Paired McNemar + Holm — per-enhancement grounding test (#3390 Slice 4, R4 gate)

**Date**: 2026-08-03
**Method**: paired McNemar (exact two-sided) + Holm-Bonferroni, via `tests/llm-eval/mcnemar_holm.py` (PR #3525), over per-config eval JSONs whose `samples` array is emitted by `POST /api/v1/admin/eval/retrieval` (PR #3523).
**Corpus**: staging, image `staging-20260803-b9912f1` (main-staging after release #3528; carries #3523). Golden set `meepleai-en-seed.json`, UUID-resolved, **34 samples / 28 citation-graded**.
**Baseline**: grounded live path, `enhancements: []`. Each enhancement is its single flag.
**Supersedes** the aggregate-only run in [`per-enhancement-2026-08-03.md`](./per-enhancement-2026-08-03.md) with the per-sample paired protocol that report's follow-up called for.

## Per-config aggregates (this run)

| config | citation_accuracy | structural_validity | graded |
|---|---|---|---|
| **baseline** | 0.6071 | 1.000 | 28/34 |
| adaptive-routing | 0.1429 | 1.000 | 28/34 |
| crag-evaluation | 0.3571 | 1.000 | 28/34 |
| raptor-retrieval | 0.5000 | 0.837 | 28/34 |
| rag-fusion-queries | 0.4643 | 1.000 | 28/34 |
| graph-traversal | 0.5000 | 1.000 | 28/34 |

(Reconstruct-vs-emitted `citation_accuracy` consistency check passed [OK] for every config, so the per-sample data is faithful to the aggregate. Baseline is 0.607 here vs ~0.50 in the aggregate run — a fresh run on the re-deployed binary; the paired comparisons are all on this same binary/run, so they are internally valid.)

## Citation grounding — `citation_matched` (McNemar exact + Holm, paired n=28)

`b` = baseline correct / enhancement wrong; `c` = baseline wrong / enhancement correct (discordant pairs; only these drive McNemar). `p_holm` = Holm-Bonferroni adjusted across the 5 comparisons.

| enhancement | b | c | Δacc | p_raw (exact) | **p_holm** | significant (α=0.05) | direction |
|---|---|---|---|---|---|---|---|
| **adaptive-routing** | 13 | 0 | −0.4643 | 0.00024 | **0.00122** | ✅ **yes** | regression |
| crag-evaluation | 7 | 0 | −0.2500 | 0.01562 | 0.06250 | no (borderline) | regression |
| raptor-retrieval | 4 | 1 | −0.1071 | 0.37500 | 0.65625 | no | regression |
| rag-fusion-queries | 5 | 1 | −0.1429 | 0.21875 | 0.65625 | no | regression |
| graph-traversal | 3 | 0 | −0.1071 | 0.25000 | 0.65625 | no | regression |

## Structural validity — binarized at 1.0 (McNemar exact + Holm, n=34)

| enhancement | b | c | p_holm | significant | direction |
|---|---|---|---|---|---|
| **raptor-retrieval** | 32 | 0 | ~0.000 | ✅ **yes** | regression |
| adaptive-routing / crag-evaluation / rag-fusion-queries / graph-traversal | 0 | 0 | 1.000 | no | neutral |

## Findings

- **adaptive-routing significantly regresses citation grounding** — 13 samples lost, **0 gained**; exact p = 0.00024, and it **survives Holm correction** (p_holm = 0.0012). A robust defect, not run-to-run noise. (It classifies queries "simple" and skips retrieval, returning few answer-relevant citations.)
- **raptor-retrieval significantly regresses structural validity** — 32/34 citations become malformed (p_holm ≈ 0). RAPTOR tree-summary nodes cite aggregated content that does not map to a valid `PDF:{doc}` + in-range page, so `CitationValidationService` rejects them. Its citation *accuracy* regression is not significant.
- **crag-evaluation is a borderline citation regression** — raw-significant (p = 0.016) but **does not survive Holm** (p_holm = 0.0625).
- **rag-fusion-queries and graph-traversal are within noise** (not significant).
- **No enhancement produces a net improvement**: every `c ≤ 1` and every `b ≥ 3` — there is not a single enhancement that recovers citations the baseline misses more than once.

## Verdict — R4 gate

The paired per-sample protocol **confirms and sharpens** the aggregate run: **keep every `rag.enhancement.*` flag OFF on the in-session live path** (`RetrievalPolicy.LiveSession` default). Statistically-significant harms after multiple-comparison correction: **adaptive-routing** (grounding) and **raptor-retrieval** (structural validity). No enhancement improves grounding. This is the rigorous evidence Slice 4's R4 gate required — a paired McNemar per sample with Holm correction, not aggregate deltas plus a noise floor.

## Reproduce

```
# 1. Deploy #3523 to staging (release main-dev → main-staging triggers deploy-staging.yml).
# 2. Copy the UUID-resolved golden set into the api container as /tmp/golden-resolved.json.
# 3. Run the endpoint once per config, saving each JSON (enhancements:[] = baseline):
for cfg in '[]' '["adaptive-routing"]' '["crag-evaluation"]' '["raptor-retrieval"]' \
           '["rag-fusion-queries"]' '["graph-traversal"]'; do ... > eval_<label>.json ; done
# 4. Analyze:
python3 tests/llm-eval/mcnemar_holm.py --baseline eval_baseline.json \
  adaptive=eval_adaptive.json crag=eval_crag.json raptor=eval_raptor.json \
  fusion=eval_fusion.json graph=eval_graph.json
#    Second metric: add --metric structural_validity.
```
