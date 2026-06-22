# ADR-072 — Notification Deduplication Strategy

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 3 — US-INT-5 (notifications & deep links)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · ADR-068 (RSVP delivery pipeline) · issue #1937 (CF-1 dedup contract)

## Context

The `UserNotifications` bounded context already exposes a `SourceEventId: Guid?` field on the `Notification` aggregate (`Domain/Aggregates/Notification.cs:30`) and on the `NotificationQueueItem` entity. ADR-068 flagged that "the `INotificationRepository.SourceEventId` dedup contract was NOT FOUND" at the time of drafting — meaning the **enforcement mechanism** for the CF-1 contract had not yet been decided. This ADR closes that gap.

The existing codebase has now shipped the following dedup infrastructure as part of issue #1937:

- **DB partial unique index** `UX_notifications_user_source_event_id` on `(user_id, source_event_id) WHERE source_event_id IS NOT NULL` — configured in `NotificationEntityConfiguration.cs:45-48`.
- **Application-level check**: `NotificationDispatcher.DispatchAsync` calls `INotificationRepository.ExistsBySourceEventIdAsync(userId, sourceEventId)` before inserting, short-circuiting on `true` (`NotificationDispatcher.cs:51-59`).
- **Repository method**: `NotificationRepository.ExistsBySourceEventIdAsync` executes `AnyAsync(n => n.UserId == userId && n.SourceEventId == sourceEventId)` (`NotificationRepository.cs:136-140`).
- **Parallel queue dedup**: `UX_notification_queue_items_channel_recipient_source_event` partial unique on `(channel_type, recipient_user_id, source_event_id)` (`NotificationQueueEntityConfiguration.cs:57-59`).

Multiple existing event handlers already propagate `SourceEventId` from the originating domain event: `PdfNotificationEventHandler`, `VectorDocumentReadyNotificationHandler`, `GameNightPublishedNotificationHandler`, `ShareRequestApprovedNotificationHandler`, and others.

ADR-068's RSVP handler pattern (recommended Option C) relies on this dedup mechanism to prevent double-notification on MediatR retry. The design decision in this ADR is therefore **which layer is the authoritative dedup enforcement point**, and whether the current dual-layer (application check + DB constraint) approach should be the canonical pattern going forward.

**What is NOT yet settled**: new event handlers are not consistently setting `SourceEventId`. Several handlers that create notifications through `INotificationDispatcher` omit the field, falling back to "legacy behavior (no dedup)" per the `NotificationDispatcher` inline comment (`NotificationDispatcher.cs:49`). The dedup strategy must be clearly specified so future handlers follow the same pattern.

## Problem

The specific architectural question: **what is the authoritative layer for `(userId, sourceEventId)` dedup enforcement — application-level check, DB unique constraint, or both — and what is the mandatory contract for new event handlers?**

Sub-decisions:
1. **In-memory cache** (`IMemoryCache` keyed by `(userId, sourceEventId)`) — fast, per-process only.
2. **Redis TTL cache** — works across pods, requires Redis dependency on the notification path.
3. **DB unique constraint** (already in place as `UX_notifications_user_source_event_id`) — strongest guarantee, catches races.
4. **Application-level pre-check** (already in `NotificationDispatcher`) — eliminates unnecessary DB write attempts, reduces constraint-violation exceptions in logs.

## Options Considered

### Option A — In-Memory Cache (IMemoryCache)

Key `(userId, sourceEventId)` with a sliding TTL (e.g. 30 minutes) in `IMemoryCache`. `NotificationDispatcher` checks the cache before any DB access.

**Pros**:
- Sub-millisecond cache hit — no DB round-trip for the common case (handler retry within the same process lifetime).
- Already registered in DI (`Microsoft.Extensions.Caching.Memory` is present via the hosting infrastructure).

**Cons**:
- Per-process only: in a multi-pod deployment, `NotificationDispatcher` on Pod A cannot see that Pod B already created a notification for `(userId, sourceEventId)`. Cache miss → duplicate write attempt → caught only by DB constraint (race window exists within the constraint's atomicity, but Postgres serializes concurrent inserts on the unique index).
- Cache entry eviction on process restart loses dedup state for in-flight retries longer than the TTL.
- Does not replace the DB constraint: a cache miss always triggers a DB write, so the constraint still needs to exist for correctness. Adds a cache layer without removing DB dependency.

**Risks**: Duplicate notifications in multi-pod scenarios between cache miss and DB insert (Postgres constraint prevents the insert but a logged `UniqueKeyViolationException` appears in the error logs — misleading for on-call ops).

**Impact**: ~1 day. New cache lookup + eviction logic in `NotificationDispatcher`.

---

### Option B — Redis TTL Cache

Replace the application-level `ExistsBySourceEventIdAsync` check with a Redis `SET NX EX <ttl>` operation. Cache key: `notif-dedup:{userId}:{sourceEventId}`. TTL = 24h (covers retry windows up to one day).

**Pros**:
- Shared across all pods — no cross-pod race window.
- Atomic `SET NX EX` eliminates the check-then-act race that exists in Option C's `ExistsBySourceEventId → AddAsync` sequence.
- Redis is already used in the infrastructure (`StackExchange.Redis` is in `apps/api/` — `docker-compose.yml` includes Redis service).

**Cons**:
- Adds Redis as a hard dependency on the notification write path. If Redis is down, the dispatcher must decide: fail the notification dispatch, or fall back to DB-only (re-introducing duplicates). Neither is clean.
- The DB unique constraint **must** be retained regardless: Redis TTL expiry (or a Redis flush) could allow duplicate inserts after the TTL window. Two layers required.
- 24h TTL is arbitrary: legitimate re-notification after a full day of inactivity (e.g., a second invite to a rescheduled game night) would be incorrectly suppressed if the same domain event ID is reused (which it should not be, but human error in testing can cause it).
- Introduces a new pattern not currently used on any notification path in the codebase.

**Risks**: Redis unavailability = notification dispatch degrades. Operational complexity (cache TTL tuning, Redis monitoring, flush-on-deploy guard).

**Impact**: ~2.5 days. New Redis client usage in `UserNotifications`, fallback logic, monitoring.

---

### Option C — DB Unique Constraint as Primary + Application Pre-Check (recommended)

Retain the existing architecture: the DB partial unique constraint `UX_notifications_user_source_event_id` is the **authoritative enforcement** mechanism. The application-level `ExistsBySourceEventIdAsync` pre-check in `NotificationDispatcher` is a **best-effort optimistic guard** that reduces unnecessary write attempts and cleans up log noise — not the primary correctness guarantee.

This is the architecture **already shipped** as issue #1937. This ADR formalises it as the canonical pattern and mandates the `SourceEventId` contract for all new event handlers.

**Canonical contract for new handlers**:
1. Every `INotificationHandler<TDomainEvent>` that dispatches via `INotificationDispatcher` **must** set `SourceEventId = domainEvent.EventId` (or equivalent domain event identifier) on the `NotificationMessage`.
2. Handlers that use `IMediator.Send(new CreateNotificationCommand(...))` directly must also pass `sourceEventId: domainEvent.EventId`.
3. Admin-triggered notifications without an originating domain event (e.g. `SendManualNotificationCommand`) may omit `SourceEventId` (null = no dedup, as documented in `NotificationDispatcher.cs:49`).

**Pros**:
- Zero additional infrastructure dependency. DB is always available on the notification path.
- Partial unique constraint is tight: per `(user_id, source_event_id)` pair; fan-out events (one domain event → N users) are correctly allowed (each `(user_id, event_id)` pair is distinct).
- `ExistsBySourceEventIdAsync` eliminates most write attempts before they reach the constraint — reduces noisy `UniqueKeyViolationException` entries in Postgres logs.
- Pattern is already in production and tested (`UX_notifications_user_source_event_id` is in the EF Core snapshot and applied migrations).
- No TTL ambiguity: the dedup window is permanent (notification row exists forever unless deleted).

**Cons**:
- `ExistsBySourceEventIdAsync` adds one `SELECT` round-trip before each notification insert when `SourceEventId` is set. Under high notification volume, this doubles DB round-trips. Mitigated by the partial index efficiency.
- The pre-check + insert sequence has a theoretical race window: between `ExistsBySourceEventIdAsync` returning `false` and `AddAsync` completing, a parallel handler replica could insert the same `(user_id, source_event_id)`. Postgres catches this with a `UniqueKeyViolationException` at the DB layer. `NotificationDispatcher` must catch this exception and swallow it (log as `Information`, not `Error`).

**Risks**: Low. The existing infrastructure is correct by construction. The main gap is handler compliance — some handlers do not set `SourceEventId`, leaving them without dedup protection.

**Impact**: ~0.5 days. Add `UniqueKeyViolationException` catch + swallow in `NotificationDispatcher.AddAsync` path; document the handler contract; audit existing handlers for compliance.

---

### Option D — Event Sourcing / Idempotency Key Table

A dedicated `notification_idempotency_keys(id, user_id, source_event_id, created_at)` table, inserted atomically alongside the notification in the same transaction. The unique index is on this table rather than on `notifications`.

**Pros**: Clean separation of dedup concern from notification data.

**Cons**: Two-table write in every transaction. The existing `UX_notifications_user_source_event_id` constraint already serves this purpose with zero extra table. Option D would be a pure overhead increase with no benefit for the current scale.

**Impact**: ~3 days. Out of scope.

## Decision

**Adopt Option C**: DB partial unique constraint `UX_notifications_user_source_event_id` is the authoritative dedup enforcement mechanism. The application-level `ExistsBySourceEventIdAsync` pre-check is retained as an optimistic guard. `SourceEventId` propagation from domain event handlers is **mandatory** for all new handlers.

**Rationale**: The architecture is already in place and correct. The decision resolves the ambiguity flagged in ADR-068 by formally naming the DB constraint as the canonical enforcement layer. Option A (in-memory) would fail across pods. Option B (Redis) adds a hard runtime dependency without removing the DB constraint requirement. Option D is pure overhead.

## Consequences

**Positive**:
- All future event handlers have a clear, enforceable contract for `SourceEventId` propagation.
- The partial unique index is lightweight (Postgres `WHERE source_event_id IS NOT NULL` means null-`SourceEventId` admin notifications incur no index overhead).
- Multi-pod safety: the DB constraint is the single point of truth, visible to all instances.
- Existing handlers (`PdfNotificationEventHandler`, `GameNightPublishedNotificationHandler`, etc.) are already compliant; only new handlers need onboarding.

**Negative**:
- One `SELECT` per notification dispatch when `SourceEventId` is set (the `ExistsBySourceEventIdAsync` round-trip). Acceptable at current scale; revisit if notification volume exceeds ~10k/min.
- `UniqueKeyViolationException` from the DB constraint (when the pre-check race window is lost) must be explicitly caught and suppressed in `NotificationDispatcher` — currently this is a gap that causes an unhandled DB exception to propagate to the calling event handler.

**Trade-offs**:
- Permanent dedup window (no TTL expiry) means a re-triggered notification for an event that legitimately repeats (e.g. a retried workflow with a new domain event ID) will work correctly only if the retry generates a new `EventId`. Handlers must never reuse `EventId` across retry attempts — this is consistent with MediatR domain event semantics (each `AddDomainEvent` call creates a new event object with a new `EventId`).

## Implementation Guidance

1. **Catch `UniqueKeyViolationException` in `NotificationDispatcher.DispatchAsync`**: after `_notificationRepository.AddAsync(notification, ct)`, wrap the call in a try/catch for `PostgresException` with `SqlState == "23505"`. Log at `Information` level: "Duplicate notification insert caught by DB constraint: user={UserId}, sourceEventId={SourceEventId} — idempotent skip". This prevents a spurious `Error`-level log from alerting on-call for an expected idempotency scenario.

2. **Handler contract documentation**: add XML doc to `INotificationDispatcher.DispatchAsync`:
   > **Required**: set `NotificationMessage.SourceEventId = domainEvent.EventId` for all event-driven dispatches. Omit only for manually-triggered admin notifications without a domain event origin.

3. **Compliance audit**: grep for `INotificationDispatcher.DispatchAsync` callers and verify `SourceEventId` is set. As of 2026-06-15, compliant handlers include `PdfNotificationEventHandler`, `VectorDocumentReadyNotificationHandler`, `GameNightPublishedNotificationHandler`, `GameNightCancelledNotificationHandler`, `ShareRequestApprovedNotificationHandler`, `NewShareRequestAdminAlertHandler`, `SharedGameIndexingAdminNotificationHandler`, `ProcessingJobNotificationEventHandler`. Non-compliant handlers (if any) should be patched in the same PR that introduces the new handler.

4. **New handler template**: `GameNightRsvpNotificationHandler` (per ADR-068 Option C) is the reference implementation — set `SourceEventId = domainEvent.EventId` on both the organizer and invitee `NotificationMessage` objects.

5. **No schema migration needed**: `UX_notifications_user_source_event_id` is already applied (EF Core snapshot confirmed at `Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs:1005`).

## Rollback / Reversibility

The DB constraint and application-level check are independently reversible. Removing `ExistsBySourceEventIdAsync` from `NotificationDispatcher` reverts to DB-constraint-only enforcement (silent `UniqueKeyViolationException` on retry). Dropping the DB index would remove dedup entirely — this is a schema migration that requires a new EF Core migration. No rollback of the dedup mechanism is anticipated; this is a permanent correctness guarantee.

## References

- `Notification.SourceEventId` — `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/Notification.cs:30`
- `NotificationEntityConfiguration` (partial unique index) — `apps/api/src/Api/Infrastructure/EntityConfigurations/UserNotifications/NotificationEntityConfiguration.cs:45-48`
- `NotificationDispatcher.DispatchAsync` (pre-check) — `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs:51-59`
- `INotificationRepository.ExistsBySourceEventIdAsync` — `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/INotificationRepository.cs:44`
- `NotificationRepository.ExistsBySourceEventIdAsync` — `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationRepository.cs:136-140`
- ADR-068 — RSVP delivery pipeline (flagged the dedup gap)
- Issue #1937 — CF-1 dedup contract (original implementation)
