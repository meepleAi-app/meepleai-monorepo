# ADR-068 — RSVP Delivery Pipeline

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 2 — sub-issue US-INT-3 (GameNight invitations & voting flow)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · [ADR-060](adr-060-live-session-persistence.md) · issue #1632 (Resend provider)

## Context

The GameManagement bounded context already has a `GameNightRsvp` entity (`Domain/Entities/GameNightEvent/GameNightRsvp.cs`) with statuses `Pending | Accepted | Declined | Maybe`, and a `GameNightRsvpReceivedEvent` domain event that carries `GameNightEventId`, `UserId`, `RsvpStatus`, and `OrganizerId`.

The email stack is fully built: `IGameNightEmailService` / `GameNightEmailService` (`GameManagement/Infrastructure/Services/`) renders four HTML templates (invitation, 24h reminder, cancelled, RSVP confirmation) and delegates to `IEmailService.SendRawEmailAsync`. That service routes to Resend via `ResendEmailSender` (`Api/Services/Email/ResendEmailSender.cs`) in staging/production, and to Mailpit via `SmtpEmailSender` in dev (memory: `dev-email-uses-mailpit.md`). Resend is live for `meepleai.app` since issue #1632.

The in-app notification stack is also built: `Notification` aggregate (`UserNotifications/Domain/Aggregates/Notification.cs`) with a `SourceEventId: Guid?` field introduced specifically for dispatcher-level deduplication (comment: "issue #1937 / CF-1"). The `EnqueueEmailCommand` / `EnqueueEmailCommandHandler` (`UserNotifications/Application/Commands/`) provides subject-based deduplication within a 1-hour window plus a per-user rate limit of 10 emails/hour.

What is **not yet implemented** for RSVP is the event handler that wires `GameNightRsvpReceivedEvent` to the notification and email dispatch paths. The existing `GameManagement/Application/EventHandlers/` directory has handlers for session lifecycle events (Created, Started, Paused, Resumed, Completed, Terminated, Abandoned) and game CRUD events, but no handler for RSVP responses.

US-INT-3 requires that when an invitee submits an RSVP (Accept/Decline/Maybe), the **organizer** receives: (a) an in-app notification, and (b) a transactional email summary. The invitee optionally receives a confirmation email (per `SendGameNightRsvpConfirmationEmailAsync`, already in `IGameNightEmailService`). The trigger fires from `GameNightEvent.RecordRsvp()` which raises `GameNightRsvpReceivedEvent`.

**Constraints observed in codebase**:
- ADR-060: every command handler that mutates EF entities **must** call `await _unitOfWork.SaveChangesAsync(ct)`. Domain events dispatch post-SaveChanges only (via MediatR `INotificationHandler`).
- `Notification.SourceEventId` exists for dedup — it must be populated from the domain event's ID to prevent double-notification on MediatR retry.
- `EnqueueEmailCommandHandler` performs subject-based dedup within 1 hour; no additional application-level dedup for email is required, but the `CorrelationId` field links the email queue item to the originating domain event for audit.
- `GameNightEmailService` calls `IEmailService.SendRawEmailAsync` directly (synchronous to the handler). This is the current pattern for all GameManagement emails — no queue intermediary is used for these notification emails.

## Problem

The specific architectural question: **how should the RSVP notification delivery be wired — synchronous in-handler dispatch or event-driven via `INotificationHandler`; and where does email deduplication live for the RSVP confirmation flow?**

Two concrete sub-decisions:
1. **In-app notification**: created synchronously in the `RecordRsvpCommandHandler` (same transaction), or raised via `GameNightRsvpReceivedEvent` and consumed by a separate `INotificationHandler`?
2. **Email dispatch**: called synchronously (block the handler, throw on Resend failure) or routed through the existing `EnqueueEmailCommand` queue (async, with retry policy)?

The decision affects the failure surface: a Resend API timeout currently propagates to the HTTP response caller. For an RSVP operation, the user action (submitting their RSVP) should not fail because of a downstream notification failure.

## Options Considered

### Option A — Fully synchronous: in-handler notification + direct email

The `RecordRsvpCommandHandler` calls `INotificationRepository.AddAsync(...)` and `IGameNightEmailService.SendRsvpConfirmationAsync(...)` directly within the same handler, before `SaveChangesAsync`. Both succeed or the whole command fails.

**Pros**:
- Simplest code path; one transaction boundary.
- Consistent with how `GameNightEmailService` is used elsewhere (called directly from handlers or services).
- Transactional guarantee: if the DB commit fails, the email is never sent (no ghost notifications).

**Cons**:
- Resend API latency (typically 50–200 ms) adds to the RSVP command response time. For a 429 or network timeout, the user's RSVP operation fails even though the data mutation succeeded.
- Email and notification creation are in-scope for RSVP command — violates single-responsibility; the handler does data mutation AND side effects.
- No retry path if Resend is transiently unavailable.

**Risks**: Resend outage = RSVP submissions return 500. Confirmed pattern from existing `GameNightEmailService` callers — risk accepted in other flows but escalates with RSVP volume.

**Impact**: ~1 day. No new infrastructure. Just a handler extension.

---

### Option B — Event-driven: `INotificationHandler<GameNightRsvpReceivedEvent>` for both notification + email

The `RecordRsvpCommandHandler` only mutates the aggregate and calls `SaveChangesAsync`. MediatR `Publish(new GameNightRsvpReceivedEvent(...))` fires post-commit (per ADR-060 pattern). A `GameNightRsvpNotificationHandler : INotificationHandler<GameNightRsvpReceivedEvent>` handles in-app notification creation + email dispatch.

**Pros**:
- Clean CQRS separation: the command handler is pure mutation; side effects live in the event handler.
- MediatR dispatches handlers sequentially in process — same request pipeline, no infrastructure overhead.
- `Notification.SourceEventId` can be set from the domain event ID, enabling dispatcher-level dedup on retry.
- Matches the pattern used by `GameSessionCompletedEventHandler`, `GameSessionStartedEventHandler`, etc. in `GameManagement/Application/EventHandlers/`.

**Cons**:
- Email dispatch is still synchronous inside the `INotificationHandler` — the Resend call blocks the MediatR pipeline. A Resend timeout still propagates upward and can fail the HTTP response.
- Does not solve the fundamental "email failure = request failure" problem without also adding async queuing.

**Risks**: Moderate. If the notification handler throws, MediatR propagates the exception to the command caller. Resend transient failures become user-facing 500s.

**Impact**: ~1.5 days. New event handler class. Minimal infrastructure change.

---

### Option C — Event-driven handler + async email via `EnqueueEmailCommand` (recommended)

The `RecordRsvpCommandHandler` mutates the aggregate and calls `SaveChangesAsync`. Post-commit, MediatR publishes `GameNightRsvpReceivedEvent`. A `GameNightRsvpNotificationHandler` handles:
1. **In-app notification** (synchronous, in-process): `IMediator.Send(CreateNotificationCommand(...))` with `SourceEventId` set from the domain event ID.
2. **Email** (async): `IMediator.Send(new EnqueueEmailCommand(..., CorrelationId: domainEvent.Id))`.

`EnqueueEmailCommandHandler` already performs subject-based dedup (`ExistsSimilarRecentAsync`) and enqueues to the `email_queue` table. The background job processor picks it up and calls Resend with its own retry policy (1 min, 5 min, 30 min exponential backoff — per `EmailQueueItem.MarkAsFailed` domain method).

**Pros**:
- RSVP submission cannot fail due to Resend API unavailability — the HTTP response returns once the DB mutation + email enqueue succeed.
- `EnqueueEmailCommandHandler.ExistsSimilarRecentAsync` provides 1-hour subject dedup, preventing duplicate RSVP confirmation emails on retry or double-submit.
- `CorrelationId = domainEvent.Id` enables cross-system audit (notification + email queue linked to same domain event).
- `Notification.SourceEventId` enables dispatcher-level dedup for in-app notifications.
- Consistent with how PDF notification emails are dispatched (via `EnqueueEmailCommand` from `PdfNotificationEventHandler`).

**Cons**:
- Email delivery is eventually consistent: the RSVP confirmation email arrives seconds to minutes after the RSVP submission, not sub-second.
- The organizer's in-app notification is still synchronous (in-process MediatR) — this is acceptable because notification creation is a fast DB write, not an external API call.
- Requires two `IMediator.Send` calls inside the event handler — test fixtures must mock both.

**Risks**: Low. `EnqueueEmailCommand` is already tested and in production for PDF notifications. The subject-dedup window of 1 hour covers double-submit scenarios. The `CorrelationId` field is already on `EnqueueEmailCommand`.

**Impact**: ~2 days. New event handler class + two new email templates (`rsvp_organizer_notification`, `rsvp_invitee_confirmation`) registered in `EnqueueEmailCommandHandler`'s template switch.

---

### Option D — Outbox pattern (MediatR + EF transactional outbox)

Persist domain events in an `outbox_events` table within the same DB transaction. A background processor reads from outbox and dispatches. Guarantees at-least-once delivery even if the app crashes between commit and MediatR publish.

**Pros**: True transactional guarantee — notification cannot be lost.

**Cons**: Significant infrastructure investment (outbox table + polling processor) not present in codebase. ADR-060's `SaveChangesAsync`-then-publish pattern is the current standard and is not being replaced as part of US-INT-3. Deferred to a potential future infrastructure ADR.

**Impact**: ~5 days. Out of scope for US-INT-3.

## Decision

**Adopt Option C**: event-driven `INotificationHandler<GameNightRsvpReceivedEvent>` with synchronous in-app notification creation and async email dispatch via `EnqueueEmailCommand`.

**Rationale**: Option C is the only option that decouples RSVP submission latency from Resend API availability without introducing new infrastructure (Option D). It reuses the existing `EnqueueEmailCommand` queue, already proven for PDF notifications. The `Notification.SourceEventId` field exists precisely for this dedup pattern. Option A's synchronous email is a risk multiplier as RSVP volume grows. Option B improves code organisation but leaves the Resend-failure-propagation problem unsolved.

## Consequences

**Positive**:
- RSVP submissions are resilient to transient Resend outages.
- In-app notification and email delivery are both idempotent (SourceEventId dedup + subject dedup).
- Consistent with existing GameManagement event handler patterns and UserNotifications email queue usage.

**Negative**:
- RSVP confirmation email is eventually consistent — not instantaneous. Acceptable for a game-night invitation context.
- Two templates (`rsvp_organizer_notification`, `rsvp_invitee_confirmation`) must be added to `EnqueueEmailCommandHandler`'s template switch or refactored to a more extensible dispatch map.

**Trade-offs**:
- If the `EnqueueEmailCommand` handler fails to enqueue (rare — DB write failure), the email is silently lost and the organizer must rely on the in-app notification only. This is acceptable given the 3-retry exponential backoff once the email is enqueued.

## Implementation Guidance

1. **New event handler**: `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/GameNightRsvpNotificationHandler.cs`.
   - Implements `INotificationHandler<GameNightRsvpReceivedEvent>`.
   - Step 1: create organizer in-app notification via `IMediator.Send(new CreateNotificationCommand(..., SourceEventId: domainEvent.EventId))`.
   - Step 2: enqueue organizer email via `IMediator.Send(new EnqueueEmailCommand(UserId: domainEvent.OrganizerId, ..., TemplateName: "rsvp_organizer_notification", CorrelationId: domainEvent.EventId))`.
   - Step 3 (if `RsvpStatus == Accepted`): enqueue invitee confirmation email via second `EnqueueEmailCommand` with `TemplateName: "rsvp_invitee_confirmation"`.

2. **Template registration**: extend the `switch` in `EnqueueEmailCommandHandler.Handle()` with `"rsvp_organizer_notification"` and `"rsvp_invitee_confirmation"` cases. Render via `IEmailTemplateService` if a new method is added, or inline HTML per existing `GameNightEmailService` pattern.

3. **Dedup**: `EnqueueEmailCommandHandler.ExistsSimilarRecentAsync` uses `command.Subject` as the dedup key. The subject for the organizer email should include the invitee's name and GameNight ID (e.g., `"RSVP: Marco responded to Game Night #abc123"`). This naturally prevents duplicates within the 1-hour window.

4. **Notification dedup**: set `SourceEventId = domainEvent.EventId` on the `Notification` aggregate. The `INotificationRepository` implementation must check for existing notifications with the same `SourceEventId` before inserting (see CF-1 contract on `Notification.SourceEventId`).

5. **Test**: unit test the handler with mocked `IMediator`. Integration test: verify `email_queue` row exists after `RecordRsvpCommand` completes.

## Rollback / Reversibility

The `INotificationHandler` approach is additive — removing the handler class reverts to no-notification behaviour. The `EnqueueEmailCommand` enqueue is also additive to the queue table. Rollback = delete the handler class and remove the two template cases from `EnqueueEmailCommandHandler`. No schema migration needed.

## References

- `GameNightRsvpReceivedEvent` — `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/GameNightRsvpReceivedEvent.cs`
- `GameNightRsvp` entity — `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightRsvp.cs`
- `IGameNightEmailService` — `apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/IGameNightEmailService.cs`
- `EnqueueEmailCommandHandler` — `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/EnqueueEmailCommandHandler.cs`
- `EmailQueueItem` (retry policy) — `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/EmailQueueItem.cs`
- `Notification.SourceEventId` — `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/Notification.cs:30`
- `ResendEmailSender` — `apps/api/src/Api/Services/Email/ResendEmailSender.cs` (issue #1632)
- ADR-060: `docs/for-claude/architecture/adr/adr-060-live-session-persistence.md` (SaveChanges-then-publish contract)
- Memory: `resend-email-provider-setup.md`, `dev-email-uses-mailpit.md`
