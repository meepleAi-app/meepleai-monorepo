# Golden Test Set — Libro Game MVP Phase 1

> **Status**: B-3 partial — golden set expanded to 100 Q&A + 50 translations via AI-assisted draft (2026-05-05). Citation pages pending B-1 procurement (manuali fisici Aaron). Aaron quality review pending.

## Purpose

Ground truth dataset for LLM evaluation:
- **Q&A hallucination CI gate** (Phase 2 Task 2.7): hallucination rate ≤ 3% on golden set
- **Translation MOS validation** (pre v2 launch): minimum 4/5 human score

## Draft Status

- **Citation pages**: All entries use `"primary_pages": []` as placeholder. Will be filled post-B-1 (procurement of physical manuals by Aaron).
- **Match policy**: `overlap_at_least_one` used as relaxed policy during draft; hard edge-case entries use `exact` (empty expected = "I don't know" path test).
- **Italian content**: AI-generated (Claude Sonnet 4.6) — native IT speaker review by Aaron pending post-B-1.
- **Game accuracy**: Plausible based on general training knowledge of each game's setting and mechanics. Specific rule citations (page numbers, exact wording) require manual verification against physical rulebooks post-B-1.
- **Source languages**: ~30 EN, ~10 DE, ~5 FR, ~3 ES/NL mixed (Andor German original + others where plausible).

## Files

| File | Purpose |
|------|---------|
| `schema.md` | Formal JSONL schemas + match policy enum |
| `qa-questions.jsonl` | 100 entries — 5 games × 20 Q&A, difficulty 60/30/10 (B-3 AI draft) |
| `translation-paragraphs.jsonl` | 50 entries — 5 games × ~10 paragraphs, types 20/15/10/5 (B-3 AI draft) |
| `csv_to_jsonl.py` | CSV → JSONL converter for contractor workflow |
| `requirements.txt` | Python deps (none — uses stdlib only) |

## Contractor workflow (Sprint 1+ when contractor onboarded)

1. Contractor receives Google Sheet template (1 sheet per game)
2. Contractor populates 100 Q&A + 50 translations entries
3. Export Sheet → CSV (one per category)
4. Run converter:
   ```bash
   python csv_to_jsonl.py qa.csv qa-questions.jsonl --schema qa
   python csv_to_jsonl.py translations.csv translation-paragraphs.jsonl --schema translation
   ```
5. ML engineer validates schema + 100% review pass
6. Commit JSONL files

## Coverage requirements

See `schema.md` for full distribution (Q&A 60/30/10 difficulty + per-game category quotas; Translations 20/15/10/5 type quotas).

## Plan reference

`docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md` lines 437-578 (Task 0.2).
