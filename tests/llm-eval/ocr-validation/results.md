# OCR Validation Results — Libro Game MVP Phase 1

> **Status**: Awaiting manual procurement (BLOCKERS.md B-1) + smoldocling /preprocess endpoint deployment (Task 1.4a).
>
> Run via: `cd tests/llm-eval/ocr-validation && SMOLDOCLING_URL=http://localhost:8500 python run_validation.py`

## Decision Gate Matrix (per game)

| Decision | Criteria |
|----------|----------|
| ✅ PASS | avg good-light ≥ 0.85 AND avg angled ≥ 0.7 AND high_conf_pct(good) ≥ 90% |
| ⚠️ MARGINAL | avg good-light 0.7-0.85 — proceed with stronger UI confidence indicators (Task 1.6) |
| 🔴 FAIL | avg good-light < 0.7 OR > 30% pages low_conf in good-light → SCOPE REVIEW |

## Aggregate Decision

- ✅ All 5 PASS → green light Phase 1
- ⚠️ 3-4 PASS, 1-2 MARGINAL → green with mitigations
- 🔴 1+ FAIL OR 3+ MARGINAL → STOP — investigate Mistral OCR / Google Document AI / scope reduction

## Results Table

| Game | good-light avg | evening avg | angled avg | high_conf_pct (good) | low_conf_pct (any) | Decision |
|------|----------------|-------------|------------|----------------------|--------------------|--------:|
| tainted-grail | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ |
| iss-vanguard | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ |
| stuffed-fables | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ |
| andor | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ |
| 7th-continent | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ | _pending_ |

## Aggregate Verdict

_pending validation run_

## Findings & Mitigations

_to be populated after run_
