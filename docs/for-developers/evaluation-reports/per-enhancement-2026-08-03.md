# Per-enhancement RAG eval on the live grounded seam (#3390 Slice 4)

**Date**: 2026-08-03
**Harness**: `POST /api/v1/admin/eval/retrieval` with `enhancements: [...]` (#3390 Step 1 grounded seam — the eval now exercises `AssemblePromptAsync`, the SAME retrieval path the in-session live agent uses, so enhancements actually affect the numbers).
**Corpus**: staging, image `staging-20260803-caadcf6`. Golden set `meepleai-en-seed.json`, UUID-resolved (5 games: catan/wingspan/dominion/ark-nova/7-wonders), **34 samples / 28 citation-graded**, `bypassCache` implicit per run.
**Spec**: `2026-08-02-rag-enhancement-live-path-validation.md` (§6 statistics, R4 gate).

## Results

| Config | citation_accuracy | structural_validity | answer_correctness | cited | P95 |
|---|---|---|---|---|---|
| **baseline-grounded run 1** | 0.5357 | 0.088 | 0.825 | 28/34 | 12447 ms |
| **baseline-grounded run 2** | 0.4643 | 0.059 | 0.809 | 28/34 | 12209 ms |
| adaptive-routing | 0.1071 | 0.588 | 0.850 | 28/34 | 13848 ms |
| crag-evaluation | 0.2857 | 0.382 | 0.811 | 28/34 | 16154 ms |
| raptor-retrieval | 0.3571 | 0.178 | 0.848 | 28/34 | 12698 ms |
| rag-fusion-queries | 0.4643 | 0.029 | 0.810 | 28/34 | 12466 ms |
| graph-traversal | 0.5000 | 0.088 | 0.787 | 28/34 | 11907 ms |

## Noise floor (test-retest, spec §6)

Two identical baseline-grounded runs gave citation_accuracy **0.5357** and **0.4643** → **test-retest spread = 0.071** (mean ≈ **0.500**). At n=28 graded this confirms the spec's weak-power caveat: only **large** effects (Δ ≳ 0.07) are distinguishable from run-to-run noise. Answer-correctness is stable (0.81–0.85), so the variance is in citation matching, not answer text.

## Verdict — R4 gate: NO enhancement passes

R4 requires an enhancement to **improve** citation_accuracy (significantly, no regression below the floor). Against the same-path grounded baseline (mean 0.500 ± ~0.036):

| Enhancement | Δ citation_accuracy vs baseline | Beyond noise? | Verdict |
|---|---|---|---|
| adaptive-routing | **−0.393** | yes (far) | **REGRESSION** |
| crag-evaluation | **−0.214** | yes | **REGRESSION** |
| raptor-retrieval | **−0.143** | yes | **REGRESSION** |
| rag-fusion-queries | −0.036 | ≈ noise | neutral / inconclusive |
| graph-traversal | 0.000 | within noise | neutral |

**None improves grounding; three regress it clearly.** On the closed rulebook domain with this golden set, the enhancements do not help citation grounding — they mostly hurt it.

**Recommendation: keep every `rag.enhancement.*` flag OFF on the in-session live path.** The `RetrievalPolicy.LiveSession` default (enhancements off, #3389/#3390) is the right production setting; this eval provides the evidence Slice 4 required to confirm it rather than assume it.

Mechanism notes:
- **adaptive-routing** has the highest structural_validity (0.588) but the lowest citation_accuracy (0.107): it skips retrieval for queries it classifies "simple", returning few/no answer-relevant citations — good structure, wrong content.
- **CRAG** adds the most latency (16.2 s, +4 s) for a −0.21 citation_accuracy — worst cost/quality. (CRAG's corrective requery on this domain stays corpus-internal — no web calls — per the `RagPromptAssemblyService_HasNoWebRetrievalDependency` guard.)

## Two findings beyond the enhancement verdict

1. **The real grounded baseline (~0.50) is much higher than the legacy-measured #3477 baseline (0.286).** #3477 measured `AskWithHybridSearchAsync` (the legacy hybrid path), which is NOT the live path — exactly the #3475-class gap Step 1 fixes. On a 5-sample spot check the legacy path also scored much lower on answer_correctness (0.27 vs 0.87 grounded). **The live in-session path is already substantially better than the old baseline suggested.** The #3477 baseline should be treated as a legacy-path floor, not the live-path baseline.
2. **structural_validity was LOW on the grounded path (0.03–0.59) vs 1.00 on the legacy path** — ✅ **fixed and VERIFIED on staging** (PR #3517, merged to `main-dev`). The grounded vector arm (`RagPromptAssemblyService.cs:301`) mapped the citation's source document id from `Embedding.VectorDocumentId` (`vector_documents.Id`) instead of `Embedding.PdfDocumentId` (`pdf_documents.Id`). `CitationValidationService` keys structural validity on `pdf_documents.Id`, so nearly every baseline citation (the vector arm dominates at MinScore 0.55 — FTS-only RRF caps below threshold, #2705) failed with `DocumentNotFound`, driving baseline structural_validity to ~0.07. The legacy hybrid path (`HybridSearchService.cs:287`) already keyed on `PdfDocumentId` — the fix restores parity. Same one-line swap also corrects two other live-path defects that consumed the wrong id: the usage_count increment (`ChatWithSessionAgentCommandHandler.BuildChunkUsageLocators`, which resolves `TextChunk` by `(pdf_documents.Id, chunkIndex)`) and the sentence-window adjacent-chunk expansion (which queried `GetAdjacentChunksAsync` with the vector-store id). This is independent of citation_accuracy (inline `[Page N]` → expected pages), so it does not change the enhancement verdict.

   **Post-deploy verification (2026-08-03, image `staging-20260803-244d286`, `RagPromptAssemblyService.cs:307` confirmed = `PdfDocumentId.ToString()`):** two baseline-grounded re-runs (`enhancements:[]`, 34 samples / 28 graded) both scored **citation_structural_validity = 1.00** (was 0.088 / 0.059 pre-fix) — a full recovery from ~0.07 → 1.00. citation_accuracy held at 0.536 / 0.571 (vs ~0.50 pre-fix, i.e. unchanged as predicted, confirming structural_validity is orthogonal), answer_correctness 0.764 / 0.763, cited 28/34.

## Caveats
- **Statistical rigor**: this is aggregate citation_accuracy per run + a test-retest noise floor, not the full per-sample McNemar/Holm protocol (the endpoint returns aggregates, not per-sample paired citation-correctness). The clear regressions (adaptive/crag/raptor) are well beyond the noise floor; fusion/graph are within it. A rigorous paired test would need the endpoint to emit per-sample results (follow-up).
- **Recall N/A**: recall/nDCG/MRR = 0 (doc-level snippets, no chunk-id labels) — unchanged from #3477; the R8 compensating manual control was not run here.
- **Latency**: the grounded path P95 is ~12 s (vs 1.4 s legacy hybrid) — inherent to the full assembly pipeline; already far above the 1500 ms target. This reinforces keeping enhancements off (they only add latency).

## Follow-ups
- ✅ ~~Investigate the grounded-path structural_validity discrepancy~~ — done: fixed (PR #3517) and verified on staging (0.07 → 1.00). The dead-code twin of the same defect on the `/search` path (`SearchResult.ToCitation()`) was also removed (PR #3520).
- Emit per-sample results from the eval endpoint to enable the full McNemar/Holm protocol.
- Re-baseline the live grounded path formally (this run supersedes #3477's legacy 0.286 as the live-path reference: ~0.50).
