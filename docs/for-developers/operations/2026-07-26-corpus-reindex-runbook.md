# Corpus Big-Bang Re-Index Runbook (SP3 #3269 / D4)

Operational runbook for the **SP3 heading-aware big-bang re-index** — the gate that
activates the heading-aware RAG pipeline (SP1 #3264, SP2 #3275/#3282) **on the existing
corpus**. Sub-project 3/4 of epic [#3266](https://github.com/meepleAi-app/meepleai-monorepo/issues/3266).

- **Slice 1** (merged): `POST /api/v1/admin/queue/reindex-ready` — enqueues Ready PDFs whose
  `IndexerVersion != target`, capped by the processing queue.
- **Slice 2** (merged): EN+IT golden baseline via `rag-smoke-dispatch.yml`.
- **Slice 3 (this runbook)**: `make reindex-corpus ENV=…` — loops the Slice 1 endpoint until
  the corpus is drained + ADR [adr-086](../../for-claude/architecture/adr/adr-086-corpus-reindex-orchestration.md).

## Why a big-bang is needed

The heading-aware pipeline is **latent**: it only runs on fresh ingests. The already-indexed
corpus has `PdfDocumentEntity.StructuredElementsJson = NULL`, flat `text_chunks`
(`Level=1`, `Heading=NULL`, `role_tags=0`) on **both** sinks (`text_chunks` +
`pgvector_embeddings`). Re-indexing every Ready PDF to `v1.1` re-runs extract → chunk →
embed → index, repopulating structured elements + role tags on both sinks.

## How the orchestration works

`reindex-ready` enqueues at most `MaxQueueSize - <currently queued>` PDFs per call, so one
call cannot drain a large corpus. `reindex-corpus.sh` logs in as admin (cookie auth), then
loops: `POST reindex-ready` → parse `EnqueuedCount` → if `> 0`, `sleep PACING_SECONDS` (lets
the Quartz rail process the batch) and repeat → if `0`, the corpus is drained and it exits 0.
A `MAX_ITERATIONS` cap fails loudly if the queue is stuck.

**Idempotent / resumable**: the selector skips already-`v1.1` docs, so a crash mid-run leaves
some PDFs re-indexed and some not — **safe to re-run**; already-migrated PDFs are not touched.

---

## Per-env procedure (run staging first, then prod)

### 0. Per-env config (one-time)

```bash
cd infra
cp scripts/reindex-corpus/.env.example scripts/reindex-corpus/.env.staging
# edit .env.staging: ENV_NAME, API_BASE_URL, ADMIN_EMAIL, ADMIN_PASSWORD
# (never commit the filled file — it is gitignored)
```

### 1. 🔴 Precondition — the `unstructured` container must be SP1-rebuilt

A **stale** `unstructured` container returns 0 elements → the re-index silently produces flat
chunks (`role_tags=0`, `Heading=NULL`) — a no-op that wastes the whole run. Verify the running
container carries the SP1 code **before** re-indexing:

```bash
# On the target host (staging/prod):
pwsh -c "docker inspect meepleai-unstructured --format '{{.Image}} {{.Created}}'"
# The image must post-date the SP1 merge (#3264). If it predates it, rebuild:
cd infra && docker compose build unstructured && docker compose up -d unstructured
```

Confirm it actually emits elements (should be a non-empty `elements[]`):

```bash
pwsh -c "docker logs meepleai-unstructured --tail=50"   # look for element extraction, not '0 elements'
```

### 2. 🔴 Capture the PRE-SP3 golden EN+IT baseline (Slice 2) — BEFORE the big-bang

The big-bang **changes retrieval ranking**, so a baseline captured *after* it would silently
absorb any pre-existing IT regression. Freeze the known-good EN+IT behavior first:

1. Dispatch `.github/workflows/rag-smoke-dispatch.yml` with `update_baseline=true` (runs against
   the current published snapshot).
2. Download the `rag-golden-baseline-<run_id>` artifact and commit
   `infra/fixtures/rag-golden-baseline.json`.

See [rag-smoke-runbook.md § SP3 critical note](./rag-smoke-runbook.md) for the full sequence.
**Skipping this step forfeits the epic #3266 non-regression signal.**

### 3. Dry-run, then the real run (staging)

```bash
cd infra
make reindex-corpus ENV=staging EXTRA=--dry-run   # verifies admin auth + prints the plan, NO enqueue
make reindex-corpus ENV=staging                   # real run: loops reindex-ready until drained
```

Expected real-run log (one line per iteration):

```
[INFO]  iteration 1: enqueued=100 skipped=0 errors=0
[INFO]  Pacing 5s to let the Quartz rail drain the queue before re-enqueuing the rest...
[INFO]  iteration 2: enqueued=100 skipped=100 errors=0
...
[OK]    Corpus drained: EnqueuedCount==0 after N iteration(s). Total enqueued this run: <T>.
```

> `skipped` rising while `enqueued` falls is normal: already-`v1.1` docs (and PDFs still in the
> queue) are skipped until the rail catches up. If `EnqueuedCount` never reaches 0 and the script
> exits with `MAX_ITERATIONS exceeded`, the queue is stuck — re-check step 1 and that the Quartz
> rail is running / not paused before raising `MAX_ITERATIONS`.

The script returns when PDFs are **enqueued**, not when they finish. Wait for the queue to drain
(async) before asserting:

```bash
# poll queue status until Queued + Processing == 0
curl -s -b <cookie> https://staging.meepleai.app/api/v1/admin/queue/status | jq '{queued,processing}'
```

### 4. Post-reindex assert on a sample IT rulebook

Pick one IT rulebook's `pdf_document_id` and confirm the structured fields landed on **both**
sinks. Parent chunks are `Level=0`, children `Level=2`, children carry a `ParentChunkId`, and
`role_tags != 0` on **both** `text_chunks` **and** `pgvector_embeddings`:

```sql
-- Parent + child chunk shape (text_chunks)
SELECT level, (heading IS NOT NULL) AS has_heading,
       (parent_chunk_id IS NOT NULL) AS has_parent,
       (role_tags <> 0) AS has_role_tags,
       COUNT(*)
FROM text_chunks
WHERE pdf_document_id = '<SAMPLE_IT_PDF_ID>'
GROUP BY 1,2,3,4
ORDER BY level;
-- Expect: rows only at level 0 (parents, has_parent=false) and level 2 (children,
-- has_parent=true); has_heading=true and has_role_tags=true on both.

-- role_tags must ALSO be non-zero on the vector sink (the two sinks must agree)
SELECT (role_tags <> 0) AS has_role_tags, COUNT(*)
FROM pgvector_embeddings
WHERE pdf_document_id = '<SAMPLE_IT_PDF_ID>'
GROUP BY 1;
-- Expect: has_role_tags=true for all rows.

-- Version stamp landed
SELECT indexer_version, structured_elements_json IS NOT NULL AS has_elements
FROM pdf_documents WHERE id = '<SAMPLE_IT_PDF_ID>';
-- Expect: indexer_version='v1.1', has_elements=true.
```

> If `role_tags=0` / `heading IS NULL` after a "successful" run, the `unstructured` container was
> stale (step 1). Rebuild it and re-run `make reindex-corpus` — the selector will pick the
> flat-but-`v1.1`-stamped docs back up only if you re-stamp them; if they were stamped `v1.1`
> while flat, re-index them explicitly via `reindex-failed` / per-PDF reindex after fixing the
> container. Prevention (step 1) is far cheaper than remediation.

### 5. Re-capture the POST-SP3 golden baseline

After the queue drains and the assert passes, re-capture the EN+IT baseline on the new corpus
and commit it (same `rag-smoke-dispatch.yml update_baseline=true` flow as step 2), then diff it
against the pre-SP3 baseline to review the **intentional** ranking change.

### 6. Gate → promote to prod

- **Gate**: `rag-smoke` green on staging (weekly cron or manual `make rag-smoke`).
- **Promote the code**: `main-dev → main-staging → main` (standard release train).
- **Prod re-index** (dry-run first, `IMEANIT` required):

```bash
cd infra
cp scripts/reindex-corpus/.env.example scripts/reindex-corpus/.env.prod   # fill prod values
make reindex-corpus ENV=prod EXTRA=--dry-run IMEANIT=--i-mean-it          # preview
make reindex-corpus ENV=prod IMEANIT=--i-mean-it                          # execute
```

Then repeat steps 4–5 against prod (post-reindex assert + baseline re-capture).

---

## Rollback thinking

The re-index **deletes and recreates** each PDF's chunks (per `ReindexDocumentCommand`:
delete `TextChunks` → reset to Pending → re-run the pipeline). Consequences for a mid-run
crash / abort:

- **Partial state is expected and safe.** Some PDFs are re-indexed (`v1.1`, structured), others
  are still flat (`v0`/`v1.0`/`NULL`). The selector (`IndexerVersion == null || != target`)
  **skips already-`v1.1` docs**, so simply **re-run `make reindex-corpus ENV=…`** to finish —
  it resumes, it does not double-process.
- **No manual DB rollback is normally needed.** The forward path (finish the re-index) is the
  rollback: a corpus with mixed `v0`/`v1.1` chunks still serves retrieval; the only downside is
  degraded ranking on the not-yet-migrated PDFs until the re-run completes.
- **If you must revert to the pre-SP3 corpus**: restore from the pre-SP3 DB snapshot / published
  seed snapshot (the RAG data lives in `text_chunks` + `pgvector_embeddings`) and re-deploy the
  pre-SP3 code. This is a heavier operation than re-running forward and is only warranted if the
  new pipeline is producing bad chunks corpus-wide (e.g. the `unstructured` container was stale
  the whole run — which step 1 exists to prevent).
- **The `reindex-ready` endpoint never aborts a batch on a per-PDF failure** — failures are
  counted in `Errors[]`/`skipped` and the loop continues. A nonzero `errors` count in the log is
  a signal to inspect the affected job IDs in the API logs, not a reason to roll back.

## References

- Endpoint: `AdminQueueEndpoints.cs` `HandleBulkReindexReady` → `BulkReindexReadyCommand`
- Script: `infra/scripts/reindex-corpus/reindex-corpus.sh`
- ADR: [adr-086 — Corpus Re-Index Orchestration](../../for-claude/architecture/adr/adr-086-corpus-reindex-orchestration.md) (extends [adr-057](../../for-claude/architecture/adr/adr-057-kb-reindex-async-channel.md))
- RAG smoke: [rag-smoke-runbook.md](./rag-smoke-runbook.md)
- Plan: `docs/superpowers/plans/2026-07-26-issue-3269-sp3-bulk-reindex.md`
- Epic [#3266](https://github.com/meepleAi-app/meepleai-monorepo/issues/3266) · Issue [#3269](https://github.com/meepleAi-app/meepleai-monorepo/issues/3269)
