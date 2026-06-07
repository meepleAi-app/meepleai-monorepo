# n8n Idempotency Contract for Domain-Event-Driven Webhooks

**Status:** active
**Owner:** WorkflowIntegration BC + DevOps (n8n workflows)
**Issue:** [#1942](https://github.com/meepleAi-app/meepleai-monorepo/issues/1942) (iso-3) — follow-up of [#1535](https://github.com/meepleAi-app/meepleai-monorepo/issues/1535) consumer idempotency audit

## Why this contract exists

The MeepleAI backend dispatches `IDomainEvent` instances via MediatR. The dispatch is currently inline-in-transaction and will move post-commit in #1535. In both modes a handler can be re-invoked for the same logical event:

- **Pre-#1535**: MediatR transient retry; double-dispatch on a request-replay race.
- **Post-#1535** (outbox): crash between `MediatR.Publish` and `MarkSent` re-fires the handler on the next processor tick.

Handlers that call `IN8nWebhookClient.TriggerWorkflowAsync(...)` forward the event to an n8n workflow. n8n workflows often have observable side effects (send email, create calendar entry, post Slack message, ping external systems). **A duplicate workflow run produces a duplicate side effect**, since n8n has no built-in dedup against MeepleAI's dispatch lifecycle.

To close that gap without changing n8n's transport semantics, both sides cooperate on a single contract:

> **The BE-side caller writes `domainEventId` into the payload. The n8n-side workflow dedups on it.**

## The contract

### BE side — every domain-event-driven `TriggerWorkflowAsync` call

The first-level payload object passed to `IN8nWebhookClient.TriggerWorkflowAsync` MUST contain a top-level property named `domainEventId` whose value is the originating `IDomainEvent.EventId` (a UUID).

```csharp
await _n8nClient.TriggerWorkflowAsync("game-night-published", new
{
    // Issue #1942 / iso-3: dedup key for n8n side workflows.
    domainEventId = notification.EventId,
    eventId = notification.GameNightEventId,
    organizerId = notification.OrganizerId,
    // ... other domain-specific fields ...
}, cancellationToken).ConfigureAwait(false);
```

Hand-triggered admin webhooks (e.g. a manual "Test webhook" button) that do NOT originate from an `IDomainEvent` are exempt — they MAY omit `domainEventId`, and the n8n workflow MUST treat its absence as "no dedup possible, process normally".

Verification is by code review. There is no compile-time enforcement (the `IN8nWebhookClient.TriggerWorkflowAsync` signature accepts `object`); the doc-comment carries the formal requirement and the audit checklist in `audits/2026-06-06-issue-1535-consumer-idempotency-audit.md` enumerates every caller that must comply.

### n8n side — every workflow consuming a MeepleAI domain event

The first node after the Webhook trigger MUST inspect the incoming payload's `domainEventId` and short-circuit when the same value has already been processed.

Reference dedup pattern (n8n 1.x):

```
Webhook (path: /game-night-published)
  ↓
Set node                   // extract { domainEventId } as a fixed variable
  ↓
IF node                    // condition: domainEventId is empty → bypass dedup, continue
  ├─ true  → Postgres SELECT 1 FROM n8n_processed_events WHERE event_id = $domainEventId
  │           ├─ found → Respond 200 OK (no-op) and stop the workflow
  │           └─ not found → Postgres INSERT INTO n8n_processed_events (event_id, workflow, received_at)
  │                            then continue to the real workflow body
  └─ false (no domainEventId) → continue to the real workflow body (legacy / hand-triggered)
```

The `n8n_processed_events` table is owned by n8n's own Postgres (NOT the MeepleAI app DB):

```sql
CREATE TABLE n8n_processed_events (
  event_id UUID PRIMARY KEY,
  workflow VARCHAR(128) NOT NULL,
  received_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Optional cleanup: retain 30 days for audit, then drop.
CREATE INDEX idx_n8n_processed_events_received_at ON n8n_processed_events(received_at);
```

The `PRIMARY KEY` on `event_id` provides race-free dedup: two simultaneous workflow runs racing on the same `domainEventId` will see one `INSERT` succeed and the sibling fail with a unique-violation, which the workflow MUST translate into the "found → no-op" branch.

## Workflows currently in scope

As of 2026-06-07 the following 3 workflows MUST be wired with the dedup node:

| Webhook path | Payload field carrying domainEventId | Owning caller |
|---|---|---|
| `game-night-published` | `domainEventId` | `GameNightPublishedN8nHandler` |
| `game-night-cancelled` | `domainEventId` | `GameNightCancelledN8nHandler` |
| `game-night-rsvp-changed` | `domainEventId` | `GameNightRsvpN8nHandler` |

When new domain-event-driven webhooks are added, this table MUST be updated in the same PR that adds the BE caller. The PR description MUST also reference the n8n workflow change (or a follow-up DevOps issue) so the cross-system pair is tracked.

## Backward compatibility

n8n ignores unknown JSON fields by default. Workflows that have NOT yet been updated will continue to receive `domainEventId` and process the payload normally — they just won't dedup. The BE-side change is therefore **always safe to ship before** the n8n-side change. The reverse (n8n updated first, BE not propagating yet) is also safe because the IF node bypasses dedup when `domainEventId` is absent.

## Rollout plan

1. **BE ship** (this PR): caller-side `domainEventId` propagation merges to `main-dev`. Three workflows above start receiving the field.
2. **n8n staging audit** (DevOps): for each of the 3 workflows on the n8n staging instance, add the dedup nodes per the reference pattern. Test by replaying a recent webhook invocation twice — verify the second run short-circuits.
3. **n8n prod cutover** (DevOps): copy the staged workflows to prod. Verify by tailing `n8n_processed_events.received_at` for a known game-night-published event during a deploy window.

## References

- Audit doc: [`audits/2026-06-06-issue-1535-consumer-idempotency-audit.md`](../../../audits/2026-06-06-issue-1535-consumer-idempotency-audit.md) § "n8n webhook handlers"
- Source: `IN8nWebhookClient` doc-comment in `apps/api/src/Api/BoundedContexts/WorkflowIntegration/Application/Services/IN8nWebhookClient.cs`
- Caller examples: `GameNightN8nEventHandlers.cs` in `apps/api/src/Api/BoundedContexts/WorkflowIntegration/Application/EventHandlers/`
