# Golden Test Set — Libro Game MVP Phase 1

> **Status**: Sprint 0 schema + converter artifact. Full data creation blocked on contractor (BLOCKERS.md B-3).

## Purpose

Ground truth dataset for LLM evaluation:
- **Q&A hallucination CI gate** (Phase 2 Task 2.7): hallucination rate ≤ 3% on golden set
- **Translation MOS validation** (pre v2 launch): minimum 4/5 human score

## Files

| File | Purpose |
|------|---------|
| `schema.md` | Formal JSONL schemas + match policy enum |
| `qa-questions.jsonl` | Sample 3 entries (full set: 100, blocked on B-3) |
| `translation-paragraphs.jsonl` | Sample 2 entries (full set: 50, blocked on B-3) |
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
