# RAG Smoke Test Runbook (#2480)

Tier 3 D8 quality gate of [#2126](https://github.com/meepleAi-app/meepleai-monorepo/issues/2126). Catches **silent retrieval regressions** — embedding-model swaps, chunker changes, index drift — that the bake smoke (`seed-snapshot-bake-ci.yml`) does not cover.

## What it does

`infra/scripts/rag-smoke-assert.sh` runs the canonical queries in `infra/fixtures/rag-canonical-queries.json` against `POST /api/v1/knowledge-base/ask/global` (SSE) and asserts the **top-3 retrieved chunks** per query match `infra/fixtures/rag-golden-baseline.json`.

The suite covers **EN + IT** (10 queries: 5 EN + 5 IT, added for [#3269](https://github.com/meepleAi-app/meepleai-monorepo/issues/3269)). The corpus is English rulebooks; `multilingual-e5-base` does **cross-lingual retrieval**, so each `-it` query pins the IT→EN retrieval behavior. This is the concrete implementation of the epic [#3266](https://github.com/meepleAi-app/meepleai-monorepo/issues/3266) LOCKED safety-net: *"EN+IT non-regression suite on staging before prod"*. Motivating case: `catan-setup-it` ("Setup per N giocatori" style IT query) must still retrieve the right EN chunks.

It reads the **Citations SSE event (`type: 1`)**, which the vector search emits *before* the LLM streams tokens — so the assertion is independent of OpenRouter/LLM availability. Each chunk is keyed by `{source, page}`; `score` is advisory (not asserted, to tolerate minor embedding/search float drift).

### Per-query `language`

The fixture top-level `language` (`"en"`) is the **default**. Each query MAY set a per-query `"language"` override — the IT queries carry `"language": "it"`, the 5 EN queries omit it and inherit the default. The harness resolves `(.language) // <top-level default>` per query and sends it as the request-body `language`, so retrieval ranking is deterministic per query.

### SKIP for un-baselined queries

A query with **no golden-baseline entry** reports `SKIP` (via a `::notice::`), not `FAIL`, and does **not** fail the gate. This lets new queries (e.g. the IT set) land *before* the ops `--update-baseline` capture without redding the weekly cron. Real drift and no-citations still `FAIL`. The summary reports `N passed, N failed, N skipped (pending baseline)`; exit is non-zero only on a real `FAIL`.

### Baseline scaduta ≠ retrieval regredito (exit 3, #3645)

Prima di eseguire le query l'harness confronta il campo `snapshot` della baseline con lo snapshot caricato (`$SEED_INDEX_OUT_DIR/.latest`). Se differiscono **esce 3 senza eseguire alcuna query**:

```
::error:: baseline scaduta — non è una regressione del retrieval
  baseline catturata su: meepleai_seed_20260729T070620Z_..._9101176e9
  snapshot in esecuzione: meepleai_seed_20260809T060634Z_..._dc83e1a4e
```

**Perché esiste**: la baseline fissa i chunk `{source,page}` di un corpus preciso. Su un corpus diverso *ogni* query risulta "drifted" — dal 2026-07-20 al 2026-08-10 il gate ha riportato `0 passed, 11 failed` per tre settimane, aprendo una issue intitolata «retrieval drift» che descriveva un guasto mai avvenuto. Un rosso che significa sempre la stessa cosa smette di essere letto, e in quelle tre settimane una regressione autentica sarebbe passata inosservata.

**Cosa fare**: rigenerare la baseline (§ *Capturing the EN + IT baseline via CI dispatch*). Non è un bug da indagare — è la conseguenza attesa di un re-bake.

| Exit | Significato | Azione |
|---|---|---|
| `0` | tutte le query combaciano (o baseline aggiornata) | — |
| `1` | drift reale **a parità di snapshot**, o nessuna citation | indagare |
| `3` | baseline catturata su un altro snapshot | rigenerare la baseline |

La guardia confronta solo quando **entrambi** i lati sono noti: senza `.latest` (esecuzione contro un'API remota) o con una baseline priva del campo `snapshot`, non blocca. Uno stato non conoscibile non va trasformato in un fallimento.

⚠️ L'auto-opener deduplica sulla label `rag-smoke-failure`: **finché una issue resta aperta non ne viene emessa un'altra**. Una issue di baseline scaduta lasciata aperta silenzia gli alert successivi, inclusi quelli di un drift vero.

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

### Capturing the EN + IT baseline via CI dispatch (preferred)

The IT queries ship with **no baseline entries** — they `SKIP` until captured. To capture the full EN + IT baseline against the current published snapshot without a local boot:

1. Dispatch `.github/workflows/rag-smoke-dispatch.yml` with `update_baseline=true` (runs against the published snapshot the workflow fetches).
2. Download the `rag-golden-baseline-<run_id>` artifact from that run.
3. Commit the artifact's `infra/fixtures/rag-golden-baseline.json` (now containing all 10 EN + IT entries).

After the commit, the weekly assert run covers EN + IT with zero `SKIP`s.

### 🔴 SP3 #3269 critical note — capture the EN + IT baseline BEFORE the big-bang re-index

The SP3 big-bang re-index (**Slice 3**) changes retrieval ranking, so a baseline captured *after* it would silently absorb any pre-existing IT regression. Sequence:

1. **Pre-SP3**: capture the EN + IT baseline on the *current* published snapshot (via the CI dispatch above) and commit it. This freezes the known-good IT→EN behavior.
2. Run the Slice 3 big-bang re-index + publish the new snapshot.
3. **Post-SP3**: re-capture the EN + IT baseline on the new snapshot and commit it — then diff against the pre-SP3 baseline to review the intentional ranking change.

Skipping step 1 forfeits the non-regression signal the epic #3266 safety-net exists to provide.

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

| queryId | game | language | targets |
|---|---|---|---|
| `catan-setup` | Catan | en (default) | board setup, initial settlements/roads |
| `wingspan-round-goals` | Wingspan | en (default) | end-of-round goal scoring |
| `dominion-buy-phase` | Dominion | en (default) | buy phase, coins |
| `ark-nova-conservation` | Ark Nova | en (default) | conservation projects, reputation |
| `seven-wonders-military` | 7 Wonders | en (default) | military conflict per age |
| `catan-setup-it` | Catan | it | board setup, initial settlements/roads (IT→EN cross-lingual) |
| `wingspan-round-goals-it` | Wingspan | it | end-of-round goal scoring (IT→EN cross-lingual) |
| `dominion-buy-phase-it` | Dominion | it | buy phase, coins (IT→EN cross-lingual) |
| `ark-nova-conservation-it` | Ark Nova | it | conservation projects, reputation (IT→EN cross-lingual) |
| `seven-wonders-military-it` | 7 Wonders | it | military conflict per age (IT→EN cross-lingual) |

The 5 EN queries omit `language` and inherit the top-level default (`en`); the 5 IT queries (#3269) set `"language": "it"`. All 5 games are indexed in the `dev.yml` seed manifest. The IT queries report `SKIP` until their baseline is captured (see *Capturing the EN + IT baseline via CI dispatch* above).
