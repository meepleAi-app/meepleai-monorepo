# Domain Events — Post-Commit Dispatch Contract

> **Issue**: #1535
> **Effective**: 2026-06-07 (Phase B cutover)
> **Audience**: every contributor writing or reviewing an `INotificationHandler<TEvent>` where `TEvent : IDomainEvent`

## What changed

Pre-#1535:

```
[Aggregate] → IDomainEventCollector → MeepleAiDbContext.SaveChangesAsync:
  base.SaveChangesAsync (commits aggregate row)
  → foreach (event in collected): MediatR.Publish(event)   // INLINE, before outer Commit
  → return
```

If the outer caller's transaction rolls back (audit retry, transient connection
error, manual `tx.RollbackAsync()`), the aggregate row is rolled back BUT the
already-fired `MediatR.Publish` side-effects (Redis cache invalidation, SSE
broadcast, email enqueue) **cannot be undone**. That race was the original bug.

Post-#1535 (this PR):

```
[Aggregate] → IDomainEventCollector → MeepleAiDbContext.SaveChangesAsync:
  EnqueueOutboxRows(events)                  // inserts rows into domain_event_outbox
  base.SaveChangesAsync (commits aggregate + outbox rows TOGETHER)
  return                                     // no inline Publish

[Later, async, 5s poll]
DomainEventOutboxProcessor → for each Pending row:
  resolve CLR type → deserialize payload → MediatR.Publish(event)
  on success: MarkSent
  on failure: MarkRetry (exponential backoff) → MarkFailed (terminal)
```

The outer transaction now commits the outbox row atomically with the aggregate.
A rollback removes the row before the processor ever sees it. **No side-effect
ever leaves the system from a rolled-back transaction.**

## Consumer requirements

Every `INotificationHandler<TEvent>` where `TEvent : IDomainEvent` **MUST be
idempotent**. The same `EventId` may be delivered:

- once (steady state — most events)
- twice (Hybrid mode rollback path, processor crash between Publish and MarkSent commit, multi-instance race)
- many times in pathological cases (consumer throws + retry budget burns down)

"Idempotent" means: receiving event with `EventId = X` for the second time
produces the **same observable outcome** as receiving it the first time. No
double-counters, no duplicate emails, no double inserts.

## Patterns

### ✅ Naturally idempotent

- **Cache invalidation**: `cache.RemoveByTag(tag)` — second call is a no-op.
- **Idempotent UPSERT**: `INSERT … ON CONFLICT DO NOTHING` — second insert is no-op.
- **SSE broadcast with client-side dedup**: the FE keeps a Set of seen
  `EventId`s and ignores re-broadcasts.

### ⚠️ Requires explicit guard

- **Email enqueue**: check `WHERE EventId = @id` on `email_queue` BEFORE INSERT.
  See `audits/2026-06-06-issue-1535-consumer-idempotency-audit.md` § Resolved
  for the canonical pattern.
- **Webhook fire** (n8n, external integrations): include `EventId` in the
  payload AND ensure the remote endpoint dedupes. The webhook contract
  treats `EventId` as the idempotency key.
- **Counter increment**: use a `(EventId, MetricKey)` UNIQUE table. Each event
  inserts at most one row; subsequent attempts collide on the unique
  constraint and become no-ops.
- **Domain aggregate mutation**: the aggregate's invariant checks should
  reject re-application of the same transition (e.g., `Session.Finalize()`
  throws if Status is already Finalized).

### ❌ Anti-patterns

- "Append to a file" without dedup — every duplicate creates a duplicate line.
- "Increment in-memory counter without persistence" — duplicates inflate it.
- "Fire-and-forget HTTP without idempotency key" — duplicates send duplicates.
- "External API call that writes" without `Idempotency-Key` header.

## How idempotency was verified

The Phase B cutover is gated on the consumer-idempotency audit:
[`audits/2026-06-06-issue-1535-consumer-idempotency-audit.md`](../../../audits/2026-06-06-issue-1535-consumer-idempotency-audit.md).

That audit enumerated every `INotificationHandler<TEvent>` where `TEvent : IDomainEvent`
in the codebase as of 2026-06-06 and classified each into:

- ✅ Naturally idempotent (cache invalidation, etc.)
- ⚠️ Requires explicit guard (email queue, counter increment)
- ❌ Non-idempotent — required a fix BEFORE the cutover

All ❌ categories were resolved through dedicated PRs (#1937 CF-1, #1938 CF-2,
#1939 CF-3, #1940 iso-1, #1941 iso-2) before Phase B.

## How to write a new handler

1. Pick the event type that matches your trigger condition.
2. Decide where on the idempotency spectrum your side-effect lands.
3. If ✅ — write the handler normally.
4. If ⚠️ — add the dedupe guard (UNIQUE constraint, `EventId` filter,
   etc.) and write a test that fires the same event twice and asserts the
   end state is identical to firing it once.
5. If ❌ — STOP. Open a discussion with the team; the event likely should not
   be a domain event at all, or the side-effect needs to be redesigned to
   become idempotent.

## How to register a new event for outbox dispatch

Domain events flow through the outbox automatically when their CLR type
implements `IDomainEvent` AND lives in the `Api` assembly (the
`DomainEventTypeResolver` scans only that assembly — see `DomainEventTypeResolver.cs`
for the startup-time assertion that protects this invariant).

For stable persistence + dashboard JOINs, register the event in
`EventTypeRegistry.AliasByType`:

```csharp
[typeof(MyNewEvent)] = "my.bounded_context.event",
```

The alias is the durable identifier — it survives CLR-type renames. Without it
the outbox still works (the resolver falls back to `Type.FullName`), but dashboards
that JOIN on `event_type` will be more brittle.

## References

- Plan: [`docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md`](../../../docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md)
- Spec: [`docs/superpowers/specs/2026-06-06-issue-1535-event-outbox-design.md`](../../../docs/superpowers/specs/2026-06-06-issue-1535-event-outbox-design.md)
- Consumer audit: [`audits/2026-06-06-issue-1535-consumer-idempotency-audit.md`](../../../audits/2026-06-06-issue-1535-consumer-idempotency-audit.md)
- Phase A PR runbook: [`audits/2026-06-07-issue-1535-phase-a-deploy-pr-draft.md`](../../../audits/2026-06-07-issue-1535-phase-a-deploy-pr-draft.md)
- Phase B PR runbook: [`audits/2026-06-07-issue-1535-phase-b-cutover-pr-draft.md`](../../../audits/2026-06-07-issue-1535-phase-b-cutover-pr-draft.md)
