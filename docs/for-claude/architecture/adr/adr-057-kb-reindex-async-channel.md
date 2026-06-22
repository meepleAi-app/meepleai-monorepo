# ADR-057 — KB Reindex Async via In-Process Channel + Persistent Status

**Status**: Accepted
**Date**: 2026-05-13
**Deciders**: @badsworm
**Tracking**: Issue [#941](https://github.com/meepleAi-app/meepleai-monorepo/issues/941) (EPIC #906 follow-up)
**Supersedes**: —

## Context

`POST /games/{gameId}/kb/reindex` (Issue #903 / PR #928) ships today as a **synchronous-pretending-async** endpoint: it iterates every `Ready`/`Failed` PDF for the game, dispatches `IndexPdfCommand` per document in a `foreach`, and returns `KbJobResponse { JobId = Guid.NewGuid(), Status = "completed" }` immediately. The `JobId` is cosmetic; `Status` is never anything other than `"completed"`. For games with many PDFs (50+), the request thread blocks for minutes — HTTP timeouts and degraded UX follow.

The issue body recommends Hangfire. The spec-panel review of #941 (Fowler · Hightower · Newman · Hohpe, 2026-05-13) rejected that recommendation after surfacing **three async-job primitives already in the codebase**:

| Existing primitive | Location | Notes |
|--------------------|----------|-------|
| `ProcessingJob` aggregate | `DocumentProcessing` BC | Most mature; full state machine with Steps + LogEntries |
| `Channel<T>` + `BackgroundService` | `Authentication.GameSuggestionChannel` + `Infrastructure.BackgroundServices.GameSuggestionProcessorService` | Lightweight, in-process, with retry-with-backoff |
| `MechanicRecalcJobEntity` | `SharedGameCatalog` BC | Batch job entity for mechanic recalculation |

Adding Hangfire would introduce a **fourth pattern** plus a third-party Postgres storage dep (`Hangfire.PostgreSql`), plus a `/hangfire` admin surface to auth-gate. For a user-triggered job where the user can re-trigger after a process restart, this overhead is not warranted.

## Decision

The reindex flow uses the **`Channel<T>` + `BackgroundService`** pattern (matching `GameSuggestionChannel` from the Authentication BC) for the work queue, with a **thin persistent `KbReindexJob` entity** for status tracking that the polling endpoint reads from.

**Work queue**: in-memory, unbounded `Channel<KbReindexJobRequest>` singleton.
**Status row**: persistent `KbReindexJob` row keyed by job ID, with state machine (`queued → running → completed | failed`).
**Polling**: `GET /games/{gameId}/kb/reindex/{jobId}/status` reads the persistent row.

## Alternatives considered

### A. Hangfire (issue body recommendation)
**Rejected**. Introduces fourth pattern; new third-party Postgres storage; new admin auth surface; killer features (retries, dashboard, multi-instance) aren't needed at current scale.

### B. Reuse `ProcessingJob` aggregate
**Considered**, deferred. `ProcessingJob` is designed for PDF-extraction job-level orchestration with Steps + LogEntries — overkill for a coarse "reindex these N PDFs" job. The reindex itself fans out into N child `IndexPdfCommand`s that each already produce their own `ProcessingJob` rows. Adding a parent `ProcessingJob` layer would couple two BCs around a generic abstraction without immediate value. If a future "complex multi-step orchestration" use case emerges, revisit.

### C. MediatR Behavior + in-memory queue
**Rejected**. MediatR Behaviors are designed for cross-cutting pipeline concerns (logging, validation, transactions), not for fire-and-forget work dispatch. Mismatch in pattern intent. Also: in-memory only, no status persistence.

### D. **Channel<T> + persistent status row (chosen)**
Zero new deps. Mirrors the established `GameSuggestionChannel` pattern (1 BC over, same Infrastructure folder). Status persistence is a small EF entity. Polling endpoint reads the row directly — no inter-service messaging.

## Consequences

### Positive

- Zero new third-party dependencies.
- Reuses an established codebase pattern (`Channel<T>` + `BackgroundService`); single mental model for two async flows.
- Status persistence is decoupled from work queue, so a process restart loses **in-flight work** but **not** status visibility. Operator can see "stuck running" jobs and decide.
- Cancellation, retry, partial-failure semantics all live in the consumer code we control — no library opinionatedness.
- Easy migration path **to** Hangfire later if scale demands: the entity model and endpoint surface stay; only the queue swaps.

### Negative

- **In-memory work queue**: pending requests are lost on process restart. Status rows for those become permanently `queued` until cleanup. The reindex is user-triggered → user can retry. Document this in the runbook.
- **No multi-instance support**: if MeepleAI ever scales to >1 API instance, the channel becomes per-instance and jobs route to whichever instance accepted the HTTP request. Currently single-instance — not a near-term concern.
- **DIY retry/cancellation**: code we maintain instead of library code. Mitigated by reusing the `GameSuggestionProcessorService` retry pattern (3 attempts, exponential backoff).

### Neutral

- The reindex job is the second `Channel<T>` consumer in the codebase. If a third lands, consider extracting a generic `ChannelBackgroundService<T>` base class — out of scope for this ADR.

## Concurrency invariants

1. **At most one *running* reindex job per `(GameId, UserId)`**. A second `POST` while a previous job is `queued` or `running` returns HTTP 409 with the existing `jobId` (lets caller poll the right one).
2. **Idempotency at the PDF level** is already preserved by `IndexPdfCommand` (existing contract); a process restart mid-job leaves the system consistent — re-running the failed PDFs is safe.
3. **Job-row lifecycle**: created `queued` in the same transaction as the POST handler (under existing UoW pattern from ADR-056). State transitions written via `KbReindexJob.MarkRunning / MarkCompleted / MarkFailed`.

## Migration path to Hangfire (if ever needed)

The swap surface is intentionally small:

1. Replace `KbReindexChannel` writer with `IBackgroundJobClient.Enqueue(...)`.
2. Replace `KbReindexBackgroundService` with a Hangfire-decorated handler method.
3. Status row + polling endpoint **unchanged** — Hangfire updates the same `KbReindexJob` row.

This means the persistent-status decision is the durable architectural commitment; the in-memory queue is a starting point.

## Refs

- Issue #941 (implementation tracking)
- PR #928 (the synchronous-pretending-async original — SG2 closes #903)
- Pattern reference: `Api.BoundedContexts.Authentication.Infrastructure.Services.GameSuggestionChannel`
- Background service reference: `Api.Infrastructure.BackgroundServices.GameSuggestionProcessorService`
- ADR-056 (UoW uniformity — the status row write follows the same pattern)
- EPIC #906 (parent, closed)
