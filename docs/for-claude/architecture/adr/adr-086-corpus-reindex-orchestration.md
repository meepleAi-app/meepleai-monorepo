# ADR-086 — Corpus-Wide Big-Bang Re-Index Orchestrated Out-of-Band via Admin-Endpoint Loop

**Status**: Accepted
**Date**: 2026-07-26
**Deciders**: @badsworm
**Tracking**: Issue [#3269](https://github.com/meepleAi-app/meepleai-monorepo/issues/3269) (SP3, epic [#3266](https://github.com/meepleAi-app/meepleai-monorepo/issues/3266))
**Extends**: [ADR-057](./adr-057-kb-reindex-async-channel.md) (KB reindex async via in-process channel)
**Supersedes**: —

## Context

The heading-aware RAG pipeline (SP1 [#3264](https://github.com/meepleAi-app/meepleai-monorepo/issues/3264),
SP2 [#3275](https://github.com/meepleAi-app/meepleai-monorepo/issues/3275)/[#3282](https://github.com/meepleAi-app/meepleai-monorepo/issues/3282))
is merged to `main-dev` but **latent**: it only acts on fresh ingests. The already-indexed
corpus has `PdfDocumentEntity.StructuredElementsJson = NULL` and flat `text_chunks`
(`Level=1`, `Heading=NULL`, `role_tags=0`) on both the `text_chunks` and `pgvector_embeddings`
sinks. SP3 is the **gate** that activates the epic on real data via a one-shot, corpus-wide
re-index.

SP3 Slice 1 (merged) added `POST /api/v1/admin/queue/reindex-ready` — a
`BulkReindexReadyCommand` handler that selects Ready PDFs whose `IndexerVersion` differs from
the target (heading-aware `v1.1`) and fans them out to `ReindexDocumentCommand` on the existing
async Quartz processing rail. Per ADR-057, that rail is an in-process `Channel<T>` +
`BackgroundService`; the reindex-ready selector paces itself against the queue capacity
(`MaxQueueSize`), so **a single call enqueues at most one queue's worth** and returns
`{ EnqueuedCount, SkippedCount, Errors[] }`. A large corpus therefore needs **repeated calls**
to fully drain.

The open question for Slice 3 (D4): **how do we drive those repeated calls per environment
(staging → prod) for a one-time big-bang migration?**

## Decision

The corpus-wide big-bang re-index is orchestrated **out-of-band** by an operator-run shell loop
over the existing admin endpoint — `infra/scripts/reindex-corpus/reindex-corpus.sh`, exposed as
`make reindex-corpus ENV=<env>` — **not** by adding a new Quartz job and **not** by making the
endpoint synchronous.

The script:

1. Loads a per-env `.env.<ENV>` (`API_BASE_URL`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, optional
   `TARGET_VERSION`/`PACING_SECONDS`/`MAX_ITERATIONS`), copied from a committed `.env.example`;
   filled files are gitignored.
2. Refuses to run against `prod` without `--i-mean-it` (mirrors the `game-reset` prod guard).
3. Logs in as admin (cookie auth) and, on `--dry-run`, prints the plan without enqueuing
   (enqueuing is a real side effect; the endpoint has no read-only mode).
4. On a real run, **loops**: `POST reindex-ready` → parse `EnqueuedCount` with `jq` → if `> 0`,
   `sleep PACING_SECONDS` (lets the Quartz rail drain the queue) and repeat → if `0`, the corpus
   is drained and it exits 0. A `MAX_ITERATIONS` safety cap fails loudly on a stuck queue.

Idempotency / resumability comes for free from the Slice 1 selector
(`IndexerVersion == null || IndexerVersion != target`): already-`v1.1` docs are skipped, so a
crash mid-run is safe to re-run.

## Alternatives considered

### A. New Quartz job (`CorpusReindexJob`) that self-drains
**Rejected.** A big-bang re-index is a **one-shot, per-env, operator-gated** migration, not a
recurring background concern. A permanent scheduled job would need pause/resume, env-targeting,
and a prod safety gate baked into application code — carrying operational surface forever for a
handful of invocations. It also inverts control: an operator running a migration wants to watch
it, pace it, and stop it, not dispatch-and-hope. The existing per-PDF Quartz rail (ADR-057)
already does the actual work; a second job orchestrating the first is redundant.

### B. Make `reindex-ready` synchronous / self-looping server-side
**Rejected.** Would re-introduce exactly the "synchronous-pretending-async" long-request
problem ADR-057 removed — a corpus-wide loop inside one HTTP request blocks for the entire
migration and times out. Server-side looping also removes the operator's ability to pace/observe
and couples the endpoint to a one-time concern.

### C. `BackfillPdfCoversJob`-style bounded background job
**Considered, rejected for this case.** `BackfillPdfCoversJob` is the right pattern for a gentle,
bounded, *ongoing* backfill of a derived field. SP3 is a *migration event* with a clear start,
a promotion gate (rag-smoke green on staging before prod), and a manual prod approval — an
out-of-band script models that lifecycle better than an always-registered job. We reuse its
**pacing ethos** (bounded batch, sleep between batches) in the loop, not its job scaffolding.

### D. **Out-of-band admin-endpoint loop (chosen)**
Zero new application code or dependencies — reuses the Slice 1 endpoint and the ADR-057 rail.
Mirrors the established `infra/scripts/game-reset/` operator-script pattern (ENV-parametrized,
`.env.<ENV>` config, `--i-mean-it` prod guard, `make` target + `-help` target). Pacing,
observability (per-iteration log line), and stop-control live where the operator is.

## Consequences

### Positive

- **No new application code, jobs, dependencies, or admin surface** — the endpoint and rail
  already exist; the script is infra/ops only.
- **Idempotent & resumable** via the IndexerVersion selector — re-running after a crash finishes
  the job without double-processing.
- **Operator-paced & observable** — one log line per iteration (`enqueued/skipped/errors`), a
  dry-run that verifies auth and prints the plan, and a hard prod guard.
- **Respects the async rail** (ADR-057): pacing between iterations lets the queue drain, so the
  loop never saturates `MaxQueueSize` and the enqueue-swallow-on-full failure mode is avoided.
- **Consistent with `game-reset`** — one mental model for per-env operator migrations.

### Negative

- **Manual invocation** — a human runs it per env. Acceptable: this is a rare, gated migration,
  not an automated pipeline step.
- **Enqueue ≠ done** — the script returns when PDFs are *enqueued*; completion is asynchronous.
  The runbook adds a queue-drain wait + post-reindex SQL assert before promotion.
- **Silent no-op if `unstructured` is stale** — a pre-SP1 container emits 0 elements → flat
  chunks stamped `v1.1`. Not a property of the orchestration; mitigated by the runbook's
  step-1 precondition check.
- **Admin credentials in a local `.env.<ENV>`** — gitignored, same posture as `game-reset`.

### Neutral

- `IndexerVersion` remains a **provenance label**, not a dispatch key (per SP3 non-goals). The
  script passes the target version **explicitly** every call to avoid stored-label drift
  (`ReindexDocumentCommand` resolves `explicit ?? stored ?? Current`).
- If corpus-wide re-indexes become frequent (e.g. every chunker change), revisit promoting this
  to a first-class gated job — the endpoint and selector stay; only the driver changes.

## References

- Issue #3269 (SP3, D4 = this slice) · Epic #3266
- ADR-057 (async reindex rail this orchestration drives)
- Slice 1 endpoint: `Api.Routing.AdminQueueEndpoints.HandleBulkReindexReady` → `BulkReindexReadyCommand`
- Per-PDF fan-out: `ReindexDocumentCommand` (delete chunks → reset Pending → enqueue on Quartz rail)
- Pacing-ethos reference: `BackfillPdfCoversJob` (DocumentProcessing BC)
- Operator-script pattern reference: `infra/scripts/game-reset/`
- Script: `infra/scripts/reindex-corpus/reindex-corpus.sh`
- Runbook: `docs/for-developers/operations/2026-07-26-corpus-reindex-runbook.md`
- Plan: `docs/superpowers/plans/2026-07-26-issue-3269-sp3-bulk-reindex.md`
