# Image-Table Golden Set (#3435 §10)

**Status**: spec (ground-truth authoring pending) · **Epic**: [#3435](https://github.com/meepleAi-app/meepleai-monorepo/issues/3435) (image-table region grounding) · **Gates**: SP6 quality (table citation opens page + highlights region + answers from table content)

The image-table golden set is the **quality gate** for the VLM table-extraction pipeline. Unlike the
[RAG golden eval set](./rag-golden-eval-set.md) (page-level citation accuracy over narrative text), this
set measures whether **tables that live as images** are (1) detected, (2) transcribed faithfully, and
(3) grounded to the correct on-page region. The VLM can hallucinate cell values, so every acceptance
criterion here needs **human-authored ground truth** — it cannot be self-graded from the model output.

Activation of the pipeline this validates: [image-table VLM activation runbook](../operations/2026-08-04-image-table-vlm-activation-runbook.md).

## Why a separate set

The narrative golden set grades page-level citation accuracy; it says nothing about whether an
image-only table was transcribed correctly. A table extracted with a swapped column or a hallucinated
value would still "cite the right page" and pass the narrative gate while giving a **wrong answer**.
This set closes that gap: it grades transcription fidelity (cell values) + region fidelity (bbox) +
answer correctness (a Q whose answer is only in the table).

## The corpus: 5–10 known image-tables

Draw from 3–4 rulebooks that contain **real image-tables** (scoring grids, action/cost tables rendered
as graphics, resource-conversion charts). Candidate sources (confirm each table is image-only, not
selectable text, before authoring):

| Game | Example table (image) | Why it qualifies |
| --- | --- | --- |
| Agricola | End-game scoring table (fields/pastures/grain → points) | Dense grid, image-rendered, answer-bearing |
| Ark Nova | Action-card cost / association-reward table | Multi-column, graphical |
| Wingspan | Bonus-card / end-of-round goal scoring grid | Scoring matrix as image |
| 7 Wonders | Military / science scoring reference | Compact reference table |

> **Selection rule**: a table qualifies only if it is **not** already covered by the text extractor
> (i.e. it is an image, so `<otsl>`-gated extraction is the *only* way its content enters the corpus).
> Verify with the SP2 candidate list + a manual crop check before authoring ground truth.

## Ground-truth schema

One record per known image-table. Authored by a human from the rulebook; the VLM output is **graded
against** it, never sourced from it.

```jsonc
{
  "id": "agricola-scoring-endgame",
  "game": "Agricola",
  "pdf_hint": "agricola-rulebook",          // resolves to a PdfDocument at eval time
  "region": {
    "page": 11,                              // 1-based page in the PDF
    "bbox": [0.08, 0.42, 0.90, 0.71],        // [x, y, x2, y2] normalized top-left; approx (IoU-matched)
    "bbox_tolerance_iou": 0.5                 // min IoU vs the persisted region bbox to count as "same region"
  },
  "expected_content": {
    "must_contain_cells": ["Fields", "0", "-1", "5+"],   // key cells that MUST appear in the markdown
    "shape": { "min_rows": 4, "min_cols": 3 }            // structural floor (row/col count sanity)
  },
  "qa": [
    { "q": "How many points is 5 or more grain worth at end game?", "a_contains": ["4"] },
    { "q": "What is the penalty for an unused field space?", "a_contains": ["-1"] }
  ]
}
```

Store under `tests/llm-eval/golden-set/image-tables/` (JSON per game or one combined file), alongside
the existing golden-set schema doc. Ground-truth authoring is a manual, one-time task per table.

## Acceptance criteria

Run after activation (runbook §5–6), against a corpus where the candidate PDFs have been through the
table-extraction pass.

| AC | What it checks | Source of truth | Pass condition |
| --- | --- | --- | --- |
| **AC1 — detection** | The image-table was recognized as a table | `pdf_table_extractions.status='extracted'` for the region matching `region.bbox` (IoU ≥ tolerance) | every golden table detected |
| **AC2 — transcription fidelity** | The markdown contains the right cells | `text_chunks.content` (Table chunk) vs `expected_content.must_contain_cells` | all `must_contain_cells` present; shape ≥ floor |
| **AC3 — region fidelity** | The citation highlights the right on-page area | persisted chunk `bounding_boxes_json` vs `region.bbox` | IoU ≥ `bbox_tolerance_iou` |
| **AC4 — answer grounding** | A table-only question is answered correctly + cites the table | grounded answer (RAG) for each `qa.q` | answer contains `a_contains`; a citation resolves to the table chunk's page + region |

AC2 uses **substring/cell-membership**, not exact-string equality — VLM whitespace/ordering varies, and
demanding an exact table render is brittle. AC4 reuses the narrative citation matcher
(`InlineCitationMatcherService`) plus a region-presence check (`CitationDto.Regions` non-empty for the
cited table chunk).

## Validation procedure

1. **Activate** the pipeline for the golden PDFs (runbook §3–5), or trigger a targeted batch.
2. **Detection + fidelity (AC1–AC3)** — for each golden record, query `pdf_table_extractions` +
   `text_chunks` (SQL in runbook §6), match the region by IoU, assert cells + bbox.
3. **Answer grounding (AC4)** — ask each `qa.q` against the game's agent; assert `a_contains` ⊆ answer
   and that a returned citation resolves to the table chunk (page + non-empty region).
4. **Report** — pass/fail per AC per table. A regression on any AC is a release blocker for the feature
   flag, not the whole corpus.

## Known limitations

- **VLM non-determinism**: identical crops can yield slightly different whitespace/row order. Grade by
  cell membership + structural floor, never exact string.
- **Approximate bboxes**: SP1 region bboxes are detector output, not pixel-perfect; `bbox_tolerance_iou`
  (default 0.5) absorbs the drift. Tighten per-table only if a table sits adjacent to another region.
- **Not a recall gate**: this set validates *known* tables end-to-end; it does not measure how many of a
  rulebook's tables were missed. Missed-table recall is a separate, later effort (needs exhaustive
  per-page table labeling).
- **Ground-truth cost**: authoring is manual per table. Keep the set small (5–10) and high-signal; grow
  only when a real extraction bug escapes it.

---

**Pipeline**: [design §10](../../superpowers/specs/2026-08-01-image-table-region-grounding-design.md) ·
**Activation**: [runbook](../operations/2026-08-04-image-table-vlm-activation-runbook.md) ·
**Narrative counterpart**: [rag-golden-eval-set.md](./rag-golden-eval-set.md).
