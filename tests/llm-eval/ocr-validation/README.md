# OCR Validation Harness

Sprint 0 artifact for **Task 0.1 — PR-2 OCR validation on 5 real gamebook manuals**.

## Purpose

Validate OCR confidence on real gamebook layouts BEFORE committing to Phase 1 ingestion code. Hard risk gate per plan v2 §"Phase 1 — G1 Photo-First Ingestion" / R-13 risk register.

## Prerequisites

1. **Manual procurement** (BLOCKERS.md B-1): purchase legal copies of:
   - Tainted Grail (English) — narrative-heavy, layout artistico
   - ISS Vanguard (English) — sci-fi, illustrazioni piene pagina
   - Stuffed Fables (English) — family, illustrations + text
   - Andor Chronicles (German) — chapter-based, no §
   - 7th Continent (French) — atypical layout

2. **Photo capture** (BLOCKERS.md B-1 step 2): for each manual, capture 10 representative pages in 3 conditions:
   - `good-light/`: reference daylight
   - `evening-light/`: living-room evening lighting (real scenario)
   - `angled/`: 15° angle (human hand)
   
   Total: 5 × 3 × 10 = 150 photos.
   
   Save as: `manuals/<game>/<condition>/page_NN.jpg`

3. **Smoldocling /preprocess endpoint** (Task 1.4a): must be deployed in dev environment at `http://localhost:8500/preprocess` accepting `image` file + `preprocessing_mode` form data, returning JSON with `confidence`, `extracted_text`, `warnings`, `is_blank`.

## Run

First-time setup:

```bash
python -m pip install requests
```

Then:

```bash
cd tests/llm-eval/ocr-validation
SMOLDOCLING_URL=http://localhost:8500 python run_validation.py
```

Output: `results.json` (per-page detailed results) + console summary per (game, condition).

## Decision gate

See `results.md` for the decision matrix and aggregate verdict criteria.

## Notes

- `manuals/` directory contents are **NOT committed** (copyright). Only `.gitkeep` is tracked.
- Script uses Python `requests` library — install via `pip install requests`.
- Plan v2 reference: `docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md` lines 291-435.
