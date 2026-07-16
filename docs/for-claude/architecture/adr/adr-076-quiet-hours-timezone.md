# ADR-076 — Quiet Hours Timezone Enforcement

**Status**: Accepted — implemented (server-side enforcement + preferences UI) in #2994/#2995
**Date**: 2026-06-15 (ratified/implemented 2026-07-16)
**Deciders**: @badsworm
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 4 — US-INT-5 (notifications & deep links)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · `NotificationPreferences` aggregate · Resend email provider (issue #1632)

> **Implementation note (2026-07-16, #2994/#2995).** Option C shipped: the server-side gate lives in
> `NotificationDispatcher.DispatchAsync` (`preferences.IsQuietHoursActive(TimeProvider.GetUtcNow())` suppresses
> the email + Slack DM enqueue; in-app is always created; the config-driven Slack **team** broadcast is not
> user-scoped so it is not gated). The window is set via `UpdateQuietHoursCommand` (`PUT /api/v1/notifications/preferences/quiet-hours`)
> and surfaced through `NotificationPreferencesDto` + the Preferences UI (`NotificationPreferences.tsx` "Ore di silenzio" section).
> MVP is **suppression-only** (not deferral), exactly as decided below. The client-side toast-suppression hook
> (Implementation Guidance §4) and the deferred-send queue remain future enhancements.

## Context

The `NotificationPreferences` aggregate (`Domain/Aggregates/NotificationPreferences.cs`) manages per-user notification channel preferences (email, push, Slack). As of 2026-06-15, it supports boolean flags per channel per notification type (`EmailOnDocumentReady`, `SlackOnGameNightInvitation`, etc.) but **has no quiet hours concept** — no `QuietHoursStart`, `QuietHoursEnd`, or `Timezone` fields.

The `NotificationDispatcher.DispatchAsync` (`Infrastructure/Services/NotificationDispatcher.cs`) loads `NotificationPreferences` and checks channel flags. Email delivery is synchronous to the handler (`NotificationQueueItem` enqueued for async processing), push delivery uses the Web Push API subscription, and Slack uses the `SlackNotificationProcessorJob`.

The email dispatch path uses Resend (memory: `resend-email-provider-setup.md`) — transactional emails are delivered immediately after `EmailQueueItem` is processed by the background job. There is currently no time-of-day gating in the `NotificationDispatcher` or the queue processor.

**Relevant channel characteristics**:
- **In-app notifications**: shown on next app open. Time of delivery is irrelevant — the user sees them when they check the app.
- **Email**: delivered to inbox immediately. A 02:00 UTC game night reminder email is disruptive for users in UTC+1 (03:00 local).
- **Push (Web Push API)**: delivered to device immediately if subscribed. Can wake a phone at night.
- **Slack DM**: delivered to the Slack client immediately, triggers a notification sound/badge.

"Quiet hours" means: suppress time-sensitive channels (email, push, Slack) during a user-defined window such as 22:00–08:00 local time. In-app notifications are never suppressed.

**Current `User` model**: no `TimeZone` field exists in the `Authentication` bounded context or the `Administration` bounded context user entity — confirmed by absence of `TimeZone`, `timezone`, `quiet_hours` in the codebase search.

## Problem

The specific architectural question: **where should quiet hours enforcement live — server-side using a `User.TimeZone` field for UTC-offset calculation, or client-side where the browser suppresses display of in-app notifications during the user's local quiet hours?**

Sub-decision: whether quiet hours should gate server-side channels (email, push, Slack) at the dispatch layer or the queue-processing layer.

## Options Considered

### Option A — Server-Side Enforcement Using `User.TimeZone`

Add a `TimeZone: string?` field to the `NotificationPreferences` aggregate (or the `User` entity in `Administration`) storing an IANA timezone string (e.g. `"Europe/Rome"`). Add `QuietHoursStart: TimeOnly?` and `QuietHoursEnd: TimeOnly?` to `NotificationPreferences`.

`NotificationDispatcher.DispatchAsync` checks, before enqueuing email/push/Slack: `IsInQuietHours(preferences, DateTimeOffset.UtcNow)`. If true, suppress email/push/Slack enqueue (in-app notification still created).

**IsInQuietHours** logic:
```csharp
var userTz = TimeZoneInfo.FindSystemTimeZoneById(preferences.TimeZone ?? "UTC");
var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, userTz);
var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);
return localTime >= preferences.QuietHoursStart && localTime < preferences.QuietHoursEnd;
// Handles midnight wrap-around: if Start > End (e.g. 22:00 - 08:00): 
//   localTime >= Start || localTime < End
```

**Pros**:
- Gates all push channels at the source: no Resend API call, no push notification, no Slack DM during quiet hours — user's phone does not wake.
- Server is authoritative: consistent behaviour regardless of whether the user's device is active.
- Works for email: even if the device is offline, the email is not sent.
- The `NotificationPreferences` aggregate is already the central opt-in/opt-out hub — adding quiet hours here is the natural extension.

**Cons**:
- Requires a `User.TimeZone` or `NotificationPreferences.TimeZone` field — new DB column + migration.
- IANA timezone handling in .NET requires `TimeZoneInfo.FindSystemTimeZoneById` which uses OS timezone data. On Linux containers, the `tzdata` package must be installed (standard on Debian/Ubuntu base images). Windows ↔ Linux timezone name differences must be handled (use `TimeZoneConverter` NuGet package for cross-platform IANA ↔ Windows compatibility).
- Suppressed notifications are lost: there is no "deferred queue" — the email that was suppressed during quiet hours is simply not sent. The user misses the notification until the next relevant event triggers a new dispatch. For a game night reminder, this means the 08:00 "wake-up" send never happens automatically.
- The user must configure their timezone in their profile settings — adds friction for users who do not bother configuring it. Default = UTC, which may mis-gate non-UTC users.

**Risks**: Moderate. Timezone handling edge cases (DST transitions, ambiguous local times at DST boundary) require careful implementation. Suppressed notifications are irreversibly lost without a deferral queue.

**Impact**: ~2.5 days. New `NotificationPreferences` fields + migration + quiet hours check + timezone converter + unit tests for DST edge cases.

---

### Option B — Client-Side Suppression (in-app only)

The server dispatches all notifications on schedule (no server-side quiet hours gating). The frontend reads `NotificationPreferences.QuietHoursStart` / `QuietHoursEnd` from the API and suppresses:
- **In-app notification badge/popup**: during quiet hours, do not show notification toasts; show a "quiet hours active" indicator instead.
- **Push notifications**: the Service Worker registration can use `Notification.requestPermission()` with a custom suppression check, but the Web Push payload is still delivered by the push service — the browser can show/hide the notification based on local DND settings, but the app cannot reliably suppress delivery after the payload is dispatched from the server.

**Pros**:
- Zero server-side infrastructure change: no timezone field, no DB migration.
- The user's device already knows the local time — no timezone string required.
- In-app suppression is accurate to the millisecond (device clock, no UTC offset calculation).

**Cons**:
- Does not suppress email or Slack DM. A 02:00 game night invitation email is still sent to the user's inbox — quiet hours does not protect email recipients.
- Does not suppress push notifications: the Web Push payload is delivered to the push service (FCM/Apple APNs) which delivers it immediately — the browser can suppress display only if the tab is active, but a push notification to a closed browser is delivered regardless.
- Client-side suppression requires the client to be running (the web app must be open to intercept the notification toast). A PWA Service Worker can handle push suppression, but this requires careful SW lifecycle management.
- Breaks for email: the primary user complaint for quiet hours features is unwanted emails at night — this option does not solve that.
- Multiple clients (web, mobile): each client must independently implement suppression.

**Risks**: High for email. Email is the most disruptive quiet-hours violation, and this option provides zero email protection.

**Impact**: ~1 day (frontend only). Does not address the core use case.

---

### Option C — Hybrid: Server-Side for Email/Slack, Client-Side for In-App/Push (recommended)

**Server-side** (in `NotificationDispatcher`): gate email and Slack DM dispatch during quiet hours using the `User.TimeZone` + `QuietHoursStart`/`QuietHoursEnd` fields on `NotificationPreferences`. If the user has quiet hours configured and the current server time falls within the window (in user's local timezone), suppress email and Slack enqueue. Log the suppression at `Debug` level.

**Client-side** (in-app + push): the frontend checks quiet hours locally for in-app notification toasts — suppress the toast popup during quiet hours, but do not suppress the notification record (it remains in the `notifications` table as unread). Push notification suppression is delegated to the device DND / Focus mode — the app does not attempt to suppress push delivery, which is outside app control.

**In-app notifications**: always created in the DB regardless of quiet hours. The user sees them when they open the app. The `IsRead` flag and `CreatedAt` timestamp are preserved. Quiet hours does not apply to persistent in-app notification records.

**Deferred send (optional future enhancement)**: email suppressed during quiet hours could be enqueued to a `QuietHours` queue and released at `QuietHoursEnd`. This is explicitly deferred — the MVP ships suppression-only (email not sent, not deferred).

**Pros**:
- Email and Slack DM are the most disruptive channels — both gated server-side.
- In-app notifications remain fully functional (no suppression of the notification record).
- The client-side check for toast popups uses the device clock — accurate, no server round-trip.
- Push is acknowledged as out of app control — documented expectation, not a bug.

**Cons**:
- Requires `NotificationPreferences.TimeZone: string?` + `QuietHoursStart: TimeOnly?` + `QuietHoursEnd: TimeOnly?` — new fields + migration.
- Server-side check adds latency to `NotificationDispatcher.DispatchAsync` (one extra preference check — already loaded, so no extra DB round-trip).
- Default `TimeZone == null` → defaults to UTC → may suppress emails for non-UTC users at wrong hours if they don't configure their timezone. **Mitigation**: when `TimeZone` is null, disable server-side quiet hours check entirely (treat as "no quiet hours configured"). Only apply when the user has explicitly configured both timezone and quiet hours window.
- MVP: suppressed emails are lost (not deferred). The 08:00 "quiet hours end" automatic send is a follow-up feature (requires a Hangfire job to re-dispatch suppressed items).

**Risks**: Low. The timezone calculation is a standard `.NET TimeZoneInfo` operation. DST is handled by `TimeZoneInfo.ConvertTime`. The null-timezone guard eliminates the mis-gate risk for unconfigured users.

**Impact**: ~2 days. `NotificationPreferences` new fields + migration + server-side quiet hours check in `NotificationDispatcher` + frontend toast suppression hook.

## Decision

**Adopt Option C**: hybrid quiet hours — server-side for email and Slack, client-side toast suppression for in-app notifications. Push suppression is delegated to device DND.

**Rationale**: Option B alone is insufficient for email gating (the primary driver). Option A alone adds unnecessary server complexity for in-app notifications (which are persistent in the DB and not disruptive when delivered late at night). Option C provides the right protection at the right layer: server controls the channels it controls (email, Slack), client controls the channels it renders (in-app toasts), and device OS controls the channel it owns (push DND).

## Consequences

**Positive**:
- Users in European timezones (the primary MeepleAI market) will not receive 02:00 game night invitation emails if they configure their quiet hours.
- `NotificationPreferences` already handles all channel opt-in/opt-out — quiet hours is a natural extension to the same aggregate.
- The `QuietHoursStart`/`QuietHoursEnd` null check means unconfigured users are unaffected by this change — zero regression risk on existing users.

**Negative**:
- MVP ships suppression-only: emails suppressed during quiet hours are not deferred to `QuietHoursEnd`. The organiser of a late-night game night invitation may not trigger a notification until the next game night action — acceptable for MVP.
- `TimeZoneConverter` NuGet package dependency may be needed for cross-platform IANA timezone compatibility (Linux containers use IANA names; Windows timezone IDs differ).

**Trade-offs**:
- The deferred-send queue (`email sent at QuietHoursEnd`) is a significant additional feature. It requires a Hangfire job that queries `notification_queue_items WHERE suppressed_until IS NOT NULL AND suppressed_until <= NOW()`. Deferred to a post-MVP issue.
- Push suppression at the server level (not dispatching the push payload when quiet hours are active) is technically feasible but would require the app to re-dispatch the push when quiet hours end — same complexity as deferred email. Deferred.

## Implementation Guidance

1. **`NotificationPreferences` domain changes**:
   - Add `TimeZone: string?` (IANA identifier, e.g. `"Europe/Rome"`).
   - Add `QuietHoursStart: TimeOnly?` and `QuietHoursEnd: TimeOnly?`.
   - Add domain method `bool IsInQuietHours(DateTimeOffset utcNow)`:
     ```csharp
     if (TimeZone is null || QuietHoursStart is null || QuietHoursEnd is null) return false;
     var tz = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
     var local = TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTime(utcNow, tz).TimeOfDay);
     return QuietHoursEnd > QuietHoursStart
         ? local >= QuietHoursStart && local < QuietHoursEnd          // same-day window
         : local >= QuietHoursStart || local < QuietHoursEnd;         // midnight wrap
     ```

2. **Migration**: `dotnet ef migrations add AddNotificationPreferencesQuietHours` — adds nullable `timezone`, `quiet_hours_start` (`time` type), `quiet_hours_end` (`time` type) to `notification_preferences`.

3. **`NotificationDispatcher` guard**: after loading `preferences`, before building `queueItems`:
   ```csharp
   var inQuietHours = preferences?.IsInQuietHours(DateTimeOffset.UtcNow) ?? false;
   // In email channel check (line ~103):
   if (!inQuietHours && (preferences == null || IsEmailEnabledForType(preferences, message.Type)))
   // In Slack channel check (line ~130):
   if (!inQuietHours && preferences is { SlackEnabled: true } && ...)
   ```
   Push is not server-gated in MVP.

4. **Frontend toast suppression**: in the `useNotificationsCounter` hook (Asse B, `apps/web/src/`), add a local `isInQuietHours()` utility using `new Date().getHours()` vs the user's `quietHoursStart`/`quietHoursEnd` from the preferences API response. If in quiet hours, suppress the notification toast popup but do not suppress the badge count or the notification list.

5. **Profile settings UI**: add a "Quiet Hours" section in `/profile?tab=settings&section=notifications` with: timezone selector (IANA timezone picker, defaulting to `Intl.DateTimeFormat().resolvedOptions().timeZone` on first load) + time range picker for start/end.

## Rollback / Reversibility

New `NotificationPreferences` fields are nullable — existing users without quiet hours configured are unaffected. Removing the `IsInQuietHours` guard from `NotificationDispatcher` reverts to always-dispatch behaviour. Rolling back the migration drops the three nullable columns (data-safe). The frontend toast suppression is a purely additive check that can be removed independently.

## References

- `NotificationPreferences` aggregate — `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationPreferences.cs`
- `NotificationDispatcher.DispatchAsync` — `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs`
- `NotificationPreferencesEntity` — `apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationPreferencesEntity.cs`
- Resend email provider (memory: `resend-email-provider-setup.md` — issue #1632)
- Mailpit dev email (memory: `dev-email-uses-mailpit.md`)
- `SlackNotificationProcessorJob` — `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`
