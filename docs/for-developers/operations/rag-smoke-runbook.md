# RAG Smoke Test Runbook (#2480)

Tier 3 D8 quality gate of [#2126](https://github.com/meepleAi-app/meepleai-monorepo/issues/2126). Catches **silent retrieval regressions** — embedding-model swaps, chunker changes, index drift — that the bake smoke (`seed-snapshot-bake-ci.yml`) does not cover.

## What it does

`infra/scripts/rag-smoke-assert.sh` runs the 5 canonical queries in `infra/fixtures/rag-canonical-queries.json` against `POST /api/v1/knowledge-base/ask/global` (SSE) and asserts the **top-3 retrieved chunks** per query match `infra/fixtures/rag-golden-baseline.json`.

It reads the **Citations SSE event (`type: 1`)**, which the vector search emits *before* the LLM streams tokens — so the assertion is independent of OpenRouter/LLM availability. Each chunk is keyed by `{source, page}`; `score` is advisory (not asserted, to tolerate minor embedding/search float drift).

## Capturing / updating the golden baseline

The baseline must be captured against a **fresh, compatible snapshot** (`snapshot-verify.sh` exit 0). Do this:

1. Ensure a fresh snapshot is the most recent in `data/snapshots/` (e.g. from `make seed-index`). `snapshot-fetch.sh` picks the newest `*.meta.json`.
2. Boot it:
   ```bash
   cd infra && make dev-from-snapshot
   bash scripts/wait-for-healthy.sh api 300
   ```
3. Capture:
   ```bash
   API_BASE_URL=http://localhost:8080 \
   SMOKE_EMAIL=<admin-email> SMOKE_PASSWORD=<password> \
   bash scripts/rag-smoke-assert.sh --update-baseline
   ```
   This writes `infra/fixtures/rag-golden-baseline.json` (`baseline`, `snapshot`, `embeddingModel`, `capturedAt`).
4. Review the diff and commit it.

**Regenerate the baseline after any intentional re-index**: embedding model change, chunker change, or a `seed-schema.version` bump (which forces a snapshot rebuild). An *unintentional* drift is exactly what this gate is meant to flag — investigate before regenerating.

## Asserting (CI / local)

```bash
cd infra
make rag-smoke          # or: bash scripts/rag-smoke-assert.sh
```
Exit 0 = all queries match. Exit 1 = a query drifted (prints expected vs got) or returned no citations.

## CI status — WEEKLY CRON (assert) + manual re-baseline

`.github/workflows/rag-smoke-dispatch.yml` runs **weekly on a schedule** (Monday 05:37 UTC) in **assert** mode against the committed golden baseline, and can still be dispatched manually to re-capture the baseline (`update_baseline=true`). R2 snapshot distribution works ([#2516](https://github.com/meepleAi-app/meepleai-monorepo/issues/2516)): the dedicated `meepleai-seed-snapshots` bucket + `SEED_BLOB_*` repo secrets are configured, and `dev-from-snapshot` fetches the published snapshot via the read creds synthesized in the workflow's "Configure snapshot bucket read credentials" step. On a `schedule` trigger `github.event.inputs.update_baseline` is empty → assert mode; a drift opens a tracking issue automatically.

The golden baseline was first committed for snapshot `meepleai_seed_20260628T211806Z_intfloat_multilingual-e5-base_7cee37d47` (#2480), after the RAG retrieval fixes in #2556 (cross-game DbContext concurrency) + #2559 (restored `text_chunks`/`pdf_documents.search_vector` columns).

**Re-baseline after an intentional re-bake** (EF head / embedding-model / chunker change):
1. re-bake + publish: `seed-snapshot-bake-full.yml -f publish=true`
2. dispatch this workflow with `update_baseline=true`
3. download the `rag-golden-baseline-<run_id>` artifact, commit `infra/fixtures/rag-golden-baseline.json`

## Canonical queries

| queryId | game | targets |
|---|---|---|
| `catan-setup` | Catan | board setup, initial settlements/roads |
| `wingspan-round-goals` | Wingspan | end-of-round goal scoring |
| `dominion-buy-phase` | Dominion | buy phase, coins |
| `ark-nova-conservation` | Ark Nova | conservation projects, reputation |
| `seven-wonders-military` | 7 Wonders | military conflict per age |

All pinned to `language: en` for deterministic ranking. All 5 games are indexed in the `dev.yml` seed manifest.
