# ADR-074 — Voting Closure Mechanism (GameNight RSVP)

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 4 — US-INT-3 (GameNight invitations & voting flow)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · ADR-068 (RSVP delivery pipeline) · domain model spec `2026-06-04-gamenight-session-domain-model.md`

## Context

The `GameNightEvent` aggregate (`Domain/Entities/GameNightEvent/GameNightEvent.cs`) tracks the full lifecycle of a game night: `Draft → Published → (InProgress | Completed | Cancelled)`. The `GameNightRsvp` entity (`Domain/Entities/GameNightEvent/GameNightRsvp.cs`) captures per-invitee responses (`Pending | Accepted | Declined | Maybe`).

**Current state**: the `GameNightEvent` aggregate has `ScheduledAt: DateTimeOffset` (the event date/time) and status flags for reminders (`Reminder24hSentAt`, `Reminder1hSentAt`). There is **no `RsvpDeadline` field** on `GameNightEvent` in the current domain model — the aggregate and its EF entity do not yet have a concept of "voting closed" or an RSVP cutoff time. Searching for `RsvpDeadline`, `VotingClosed`, `ClosesAt`, and `VotingDeadline` across `BoundedContexts/GameManagement/Domain` returns no matches.

**What US-INT-3 requires** (per the domain model spec §Tagging vs RSVP): the RSVP phase has a cutoff — after the organiser publishes and sends invitations, there is an expected window during which invitees respond. The spec defines 5 phases (tag silente → "Invia inviti" esplicito → pending → confermato). The "voting closed" concept is the transition from the pending/response window to a finalised attendee list.

The voting closure mechanism must answer: **when can an organiser no longer accept new RSVPs, and how is that enforced?**

**Scheduling context**: the project currently uses Hangfire for lightweight scheduled jobs (`CooldownEndReminderJob`, `StaleShareRequestWarningJob`). A Hangfire scheduler context is therefore available. The reminder jobs (`Reminder24hSentAt`, `Reminder1hSentAt`) are time-based — they fire when `ScheduledAt - now < 24h` (or 1h), checking via scheduled polling.

## Problem

The specific architectural question: **should RSVP voting closure be enforced by a scheduled job that transitions the aggregate state explicitly, or by a lazy read-time check that derives "closed" status on query projection?**

Sub-decision: whether a new `RsvpDeadline: DateTimeOffset?` field is added to `GameNightEvent` or whether closure is derived implicitly from `ScheduledAt` (e.g. "RSVP closes 2h before the event").

## Options Considered

### Option A — Scheduled Job (Hangfire cron, explicit closure event)

A Hangfire recurring job polls every 5 minutes for `GameNightEvent` records where `Status == Published`, `RsvpDeadline <= now`, and no `RsvpClosedAt` timestamp. For matching records, the job calls a `CloseRsvpCommand` which transitions the aggregate to a "RSVP closed" sub-state and raises a `GameNightRsvpClosedEvent` domain event. Notifications are sent to the organiser ("RSVP period ended; N players confirmed").

**Pros**:
- Explicit domain event: `GameNightRsvpClosedEvent` is raised, enabling downstream reactions (send organiser summary email, lock new RSVPs at the aggregate level, update the UI state).
- Aggregate state reflects closure: `RsvpClosedAt` timestamp is set on the entity — readable in any query without recalculating.
- Consistent with the existing reminder job pattern (`Reminder24hSentAt` / `Reminder1hSentAt` set by similar cron-triggered jobs).
- Deterministic: organiser can see exactly when RSVP closed, and the timeline is auditable.

**Cons**:
- Polling granularity: a 5-minute tick means closure can lag up to 5 minutes past `RsvpDeadline`. For a 18:00 event with a 16:00 deadline, a new RSVP submitted at 15:58 is accepted; the aggregate closes at 16:01-16:05 depending on tick timing. Acceptable for a social game app.
- Requires adding `RsvpDeadline: DateTimeOffset?` and `RsvpClosedAt: DateTimeOffset?` to `GameNightEvent` + migration.
- Adds a new Hangfire job to the scheduler — minor operational complexity.
- `CloseRsvpCommand` + handler + validator: ~150 LOC new code.

**Risks**: Moderate. Hangfire tick failure (if the scheduler is down) could delay closure. `ConflictException` if `CloseRsvpCommand` is replayed for an already-closed event (must be idempotent — check `RsvpClosedAt != null` and short-circuit).

**Impact**: ~3 days. New domain fields + migration + Hangfire job + command/handler/notification.

---

### Option B — Lazy Check on Read (projection-time derivation)

No new domain field or scheduled job. `RsvpDeadline` is derived: `RsvpClosedAt = ScheduledAt - configured_offset` (e.g. 2h before the event). Every read-path projection (DTOs, query handlers) checks `if (now > event.ScheduledAt - deadlineOffset) => IsRsvpClosed = true`. The `RecordRsvpCommand` validator checks the same condition and returns a `ConflictException("RSVP period has closed")` if closure is detected.

**Pros**:
- Zero new infrastructure: no Hangfire job, no new migration columns.
- Always "live": the closure condition reflects the current server time at the moment of the request — no polling lag.
- Implementation is a pure expression in the validator and projection: `event.ScheduledAt.AddHours(-2) < DateTimeOffset.UtcNow`.

**Cons**:
- No explicit domain event: there is no `GameNightRsvpClosedEvent` to trigger organiser notifications, UI state updates, or audit entries. The organiser must discover closure by observing the UI change, not by receiving a notification.
- The `deadlineOffset` (2h) is a hardcoded convention not surfaced in the domain model — not configurable per event.
- Timeline is not auditable: no timestamp records when the RSVP period actually ended for a given event.
- Inconsistent with the reminder job pattern: the existing `Reminder24hSentAt` / `Reminder1hSentAt` pattern uses explicit timestamps set when the domain transition occurs, not derived projections.

**Risks**: Low implementation risk; but missing notification on closure is a UX gap — the organiser receives no closure summary email (violates the intent of US-INT-3's organiser notification requirement).

**Impact**: ~0.5 days. Validator guard + DTO projection field.

---

### Option C — Lazy Check + Nightly Cleanup Batch (recommended)

Hybrid approach: the read-path uses a lazy `IsRsvpClosed` derived property (`ScheduledAt <= DateTimeOffset.UtcNow || (RsvpDeadline.HasValue && RsvpDeadline.Value <= DateTimeOffset.UtcNow)`) for immediate closure enforcement in the `RecordRsvpCommand` validator. Separately, a lightweight Hangfire job runs once nightly (or every hour) to find `GameNightEvent` records whose implicit or explicit closure condition has passed and updates their `RsvpClosedAt` timestamp + dispatches the organiser summary notification.

Add a **lightweight `RsvpDeadline: DateTimeOffset?`** field to `GameNightEvent`: optional, organiser-configurable. Defaults to `null` (meaning: RSVP closes when the event starts, i.e. `ScheduledAt`).

**Closure rule**:
- If `RsvpDeadline != null`: closed when `RsvpDeadline <= now`.
- If `RsvpDeadline == null`: closed when `ScheduledAt <= now` (event has started — no point accepting RSVPs).

**Pros**:
- Immediate enforcement: the `RecordRsvpCommand` validator checks the derived closure condition — no polling lag for blocking new RSVPs.
- Explicit audit trail: the Hangfire job sets `RsvpClosedAt` once, after the closure condition is met, giving an auditable timestamp.
- Organiser notification: the Hangfire job dispatches the closure summary notification (`EnqueueEmailCommand` for "RSVP period ended: N confirmed, M declined, K no-response").
- Flexible: organiser can set an early `RsvpDeadline` (e.g. 48h before the event for catering planning) or leave it null for the default "closes when event starts" behaviour.
- Minimal new fields: only `RsvpDeadline: DateTimeOffset?` and `RsvpClosedAt: DateTimeOffset?` — single migration.

**Cons**:
- Two-layer approach (lazy check + scheduled cleanup) introduces duplication of the closure condition logic (validator and job both evaluate it). Extract to a domain method `GameNightEvent.IsRsvpClosed(DateTimeOffset now)` to avoid divergence.
- Hangfire job still has polling lag for setting `RsvpClosedAt` (up to 1h if job runs hourly). The organiser may receive the closure notification up to 1h after the actual closure. Acceptable for a social app.
- One new Hangfire job added to the scheduler.

**Risks**: Low. The domain method `IsRsvpClosed` is a pure function that both the validator and the job delegate to. Divergence risk is eliminated if the domain method is the single source of truth.

**Impact**: ~2 days. `RsvpDeadline` + `RsvpClosedAt` fields + migration + domain method + Hangfire job + closure notification.

---

### Option D — Event-Sourced Closure via ScheduledAt Trigger Only

Force closure exclusively through the `GameNightEvent.Publish()` → organiser sets `ScheduledAt` → no RSVPs after `ScheduledAt`. No new fields. No job. The `RecordRsvpCommand` validator simply checks `event.ScheduledAt <= DateTimeOffset.UtcNow`.

**Pros**: Zero-overhead simplicity.

**Cons**: No per-event configurable deadline. No organiser closure notification. Inconsistent with the 5-phase RSVP model (organiser may want RSVPs to close days before the event, not at start time). Subset of Option B with less flexibility.

**Impact**: Lowest. Out of scope for the RSVP closure feature.

## Decision

**Adopt Option C**: lazy validation-time closure check + optional `RsvpDeadline` field + Hangfire hourly cleanup job for `RsvpClosedAt` timestamp and organiser notification.

**Rationale**: Option B alone provides no organiser notification and no audit trail. Option A adds a 5-minute-tick job with full implementation cost but no improvement over Option C for the user-facing experience (the validator catches new RSVPs immediately in both). Option C captures the best outcome: immediate enforcement (no RSVP accepted after closure), auditable timestamp, and organiser notification — with a minimal new domain model change (two nullable fields). Option D is too restrictive for the MVP use case.

## Consequences

**Positive**:
- `RecordRsvpCommand` validation immediately rejects RSVPs past the closure point — no polling lag for the user-facing action.
- Organiser receives a "RSVP period closed: N confirmed" notification through the existing `EnqueueEmailCommand` path (per ADR-068 Option C).
- The `RsvpDeadline` field is organiser-configurable — supports catering/planning workflows that need an early cutoff.
- Domain method `GameNightEvent.IsRsvpClosed(DateTimeOffset now)` is the single source of truth for closure state — validator, job, and DTOs all delegate to it.

**Negative**:
- One new migration required (`RsvpDeadline: DateTimeOffset?`, `RsvpClosedAt: DateTimeOffset?`, added to `game_night_events` table).
- Hangfire job adds an operational dependency. If the job stalls, `RsvpClosedAt` is never set (organiser does not receive closure notification), but new RSVPs are still rejected by the validator. The functional impact is notification delay only.

**Trade-offs**:
- The Hangfire job runs hourly — organiser closure notification may be delayed up to 1 hour past actual closure. Acceptable for a social game-night app (organisers are not making time-sensitive catering calls based on this notification).
- The `RsvpDeadline == null` default (closure at `ScheduledAt`) is the least surprising behaviour for organisers who don't configure a deadline.

## Implementation Guidance

1. **Domain model changes** (`GameNightEvent.cs`):
   - Add `RsvpDeadline: DateTimeOffset?` — set via `SetRsvpDeadline(DateTimeOffset deadline)` domain method with guard (`deadline < ScheduledAt` required).
   - Add `RsvpClosedAt: DateTimeOffset?` — set by the Hangfire job via `MarkRsvpClosed(DateTimeOffset closedAt)` internal method.
   - Add `bool IsRsvpClosed(DateTimeOffset now) => RsvpClosedAt.HasValue || (RsvpDeadline.HasValue ? RsvpDeadline.Value <= now : ScheduledAt <= now)`.

2. **Migration**: `dotnet ef migrations add AddGameNightRsvpClosureFields` — adds nullable `rsvp_deadline` and `rsvp_closed_at` columns to `game_night_events`.

3. **Validator guard** (`RecordRsvpCommandValidator`): add rule `RuleFor(cmd => cmd).Must(cmd => !gameNightEvent.IsRsvpClosed(DateTimeOffset.UtcNow)).WithMessage("RSVP period has closed for this event").WithErrorCode("RSVP_CLOSED")`.

4. **Hangfire job**: `GameNightRsvpClosureJob` at `UserNotifications/Infrastructure/Scheduling/` — runs hourly. Queries `game_night_events WHERE status = 'Published' AND rsvp_closed_at IS NULL AND (rsvp_deadline <= NOW() OR scheduled_at <= NOW())`. For each: calls `MarkRsvpClosed(now)`, saves via `IUnitOfWork.SaveChangesAsync`, dispatches closure notification via `INotificationDispatcher`.

5. **DTO projection**: `GameNightEventDto` adds `IsRsvpClosed: bool` derived from `event.IsRsvpClosed(DateTimeOffset.UtcNow)` in the query handler mapping.

## Rollback / Reversibility

The new domain fields (`RsvpDeadline`, `RsvpClosedAt`) are nullable — existing events without these fields behave as before. Removing the Hangfire job stops the closure notification but does not break existing RSVPs. The validator guard can be removed to re-allow post-deadline RSVPs. Rolling back the migration requires dropping the two nullable columns (data-safe since no values existed before the migration).

## References

- `GameNightEvent` aggregate — `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs`
- `GameNightRsvp` entity — `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightRsvp.cs`
- `CooldownEndReminderJob` (reference Hangfire job) — `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/CooldownEndReminderJob.cs`
- `StaleShareRequestWarningJob` (reference Hangfire pattern) — `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/StaleShareRequestWarningJob.cs`
- ADR-068 (RSVP delivery pipeline + `EnqueueEmailCommand` pattern)
- Domain model spec — `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md` § Tagging vs RSVP (5-phase model)
