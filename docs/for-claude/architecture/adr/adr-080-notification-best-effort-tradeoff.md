# ADR-080 — Notification Side-Effect Best-Effort Tradeoff for Fan-Out Jobs

**Status**: Proposed
**Date**: 2026-06-16
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2407](https://github.com/meepleAi-app/meepleai-monorepo/issues/2407) — `feat(notifications): #2392 follow-up — outbox/retry for AchievementEvaluationJob notification atomicity`
**Related**: [#2392](https://github.com/meepleAi-app/meepleai-monorepo/issues/2392) (closed, PR #2402) · [#2383](https://github.com/meepleAi-app/meepleai-monorepo/issues/2383) (umbrella) · [#1535](https://github.com/meepleAi-app/meepleai-monorepo/issues/1535) (domain-event outbox reference pattern)

## Context

PR #2402 (closed #2392) migrated 9 of 10 `INotificationRepository.AddAsync` callers to either `AddAndCommitAsync` (single-row) or the new `AddBatchAndCommitAsync` (fan-out batch). Both new methods fire metric + SSE broadcast **after** SaveChanges, eliminating the phantom-broadcast risk where a failed save left a dangling metric/SSE frame for a notification that was never durably stored.

The change introduced a **secondary failure mode** in callers that previously batched aggregate state + notifications in the same Unit of Work. The clearest example is `AchievementEvaluationJob` (Gamification bounded context):

```csharp
// BEFORE (single transaction)
foreach (var userId in userIds) {
    EvaluateUserAchievementsAsync(userId, ...)  // tracks UserAchievement + Notification
}
await _unitOfWork.SaveChangesAsync(ct);  // commits BOTH atomically

// AFTER (split transactions, #2402)
foreach (var userId in userIds) {
    EvaluateUserAchievementsAsync(userId, pendingNotifications, ...);  // tracks UserAchievement only
}
await _unitOfWork.SaveChangesAsync(ct);                           // Step 1: commits UserAchievement.UnlockedAt
await _notificationRepository.AddBatchAndCommitAsync(pending, ct); // Step 2: commits Notifications + side-effects POST-save
```

### Atomicity gap

If Step 2 throws (deadlock, transient connection drop, DB hiccup), the outer `try/catch` at `AchievementEvaluationJob.Execute:112` swallows + logs. At the next job run:

```csharp
if (existing.IsUnlocked) continue;  // line 142
```

→ the user is skipped → **the notification is never retried**.

Pre-#2402 behaviour: the whole `_unitOfWork.SaveChangesAsync` would have rolled back, so the achievement stayed locked AND the notification would have been re-emitted on the next run. Trade-off: **phantom-broadcast (transient, FE-visible) → silent notification loss (rare, daily-job-bounded)**.

### Why this matters now

PR #2402 made the same split in 5 other loop callers (`CircuitBreakerStateChangedEventHandler`, `ModelDeprecatedAutoFallbackHandler`, `ModelDeprecatedNotificationHandler`, `CooldownEndReminderJob`, `StaleShareRequestWarningJob`). Each one trades the same way. The code-review subagent flagged this as MEDIUM (PR #2402 finding #1). Issue #2407 enumerated three design options:

- **Option A** — Outbox pattern: persist pending notifications to a `pending_notification_outbox` table in the SAME transaction as the aggregate state, dispatch asynchronously via a `BackgroundService` modelled on `DomainEventOutboxProcessor` (issue #1535). Restores full atomicity. ~6-8h.
- **Option B** — Sentinel column: add `UserAchievement.NotificationDispatched: bool` (or equivalent per caller) so the daily job re-evaluation retries the notification step on a partial failure. ~3-4h per caller, 6 callers ⇒ ~24h aggregated.
- **Option C** — Document the tradeoff: accept that fan-out notifications are best-effort. Surface monitoring so a partial-failure pattern is detectable. ~30min.

## Decision

**Option C** (best-effort + monitoring) for the current implementation. The notification loss mode is bounded and detectable:

| Caller | N per run | Frequency | Failure-mode user impact | Re-fire trigger? |
|---|---|---|---|---|
| `AchievementEvaluationJob` | ≤ ~10 unlocks/day | Daily | Missed "achievement unlocked" badge popup. Badge still visible on `/achievements` page on next render. | No (next run skips on `IsUnlocked`). |
| `CircuitBreakerStateChangedEventHandler` | ≤ N admins | Per state change | Admin doesn't get SSE/email for a single circuit-breaker transition. Next transition re-notifies. | Implicit (next event). |
| `ModelDeprecatedAutoFallbackHandler` | ≤ N admins | Per deprecation | Admin misses one auto-fallback alert. Strategy mapping update still succeeded. | No. |
| `ModelDeprecatedNotificationHandler` | ≤ N admins | Per deprecation event | Same as above. | Implicit on next ModelDeprecatedEvent. |
| `CooldownEndReminderJob` | ≤ N users | Hourly | User doesn't see "cooldown ended" cheer. Cooldown end is still computable from the rate limiter. | No (user's `CooldownEndsAt` already past). |
| `StaleShareRequestWarningJob` | ≤ N admins | Per stale window | Admin misses one of N daily digests. Next day's run re-emits the same warning (request still stale). | Yes (idempotent re-emit). |

**Common pattern**: every caller is either daily/per-event with a self-correcting next-firing, OR the notification is a courtesy signal whose underlying state is independently visible to the user. Silent loss of a single notification does not cause a state divergence between client and server — the achievement is still unlocked, the cooldown is still ended, the circuit breaker state is still queryable. Only the *notification frame* is missed.

### Monitoring strategy (this ADR's deliverable)

- **Existing metric**: `meepleai_notification_created_total` (Prometheus counter, recorded by `MeepleAiMetrics.RecordNotificationCreated`).
- **Existing instrumentation**: `_logger.LogError` inside each caller's outer `catch (Exception ex)` block surfaces a stack trace on partial failure.
- **No new code required**: the existing log + metric pair is the detection signal. A Grafana alert on `delta(meepleai_notification_created_total[15m]) == 0 AND log_count{level="error",job=~".*Job"} > 0` flags the failure pattern.
- **Operator runbook delta**: see `docs/for-developers/operations/operations-manual.md` § Notifications (post-merge update).

### Trigger for revisiting

Promote to **Option A (outbox)** if any of the following becomes true:

1. A real Grafana alert fires for the pattern above and post-incident review shows >1 day of missed notifications.
2. Achievement unlock cadence increases to > ~100/day (where missing 1 stops being negligible).
3. A new caller is added that violates the "self-correcting next-firing" property (e.g. a notification that fires exactly once per user-lifetime).
4. Product surfaces a user complaint where the missing notification frame caused real confusion.

Promote to **Option B (sentinel)** for a specific caller if its self-correcting property is contested but a full outbox is over-engineered for that one site.

## Consequences

### Positive

- Zero new code shipped — eliminates the ~6-8h outbox effort + ~24h sentinel-per-caller effort.
- Existing metric + log infrastructure already detects the failure pattern.
- Preserves the phantom-broadcast fix (PR #2402) without trading it for atomicity that the callers don't actually require.
- Documents the tradeoff in one place so the next dev reading `AchievementEvaluationJob.cs:85-95` doesn't have to reconstruct the reasoning from PR history.

### Negative

- Silent loss of a single notification is possible on transient DB failure. Mitigated by the per-caller self-correcting properties enumerated above.
- The tradeoff is invisible from the code — `_notificationRepository.AddBatchAndCommitAsync(...)` reads as if it's atomic with the upstream `_unitOfWork.SaveChangesAsync(...)`. The xmldoc on `AddBatchAndCommitAsync` (`INotificationRepository.cs`) already notes "any `DbUpdateException` rolls back the entire batch" but does NOT mention that an external prior commit is NOT part of the rollback. A follow-up xmldoc tightening could surface this.

### Neutral

- This ADR is reversible: Option A's outbox pattern is additive (new table + new BackgroundService) and would not require touching the 6 callers' existing call shape. The decision can be revisited per the trigger conditions above.

## Related work

- PR #2402 introduced `AddBatchAndCommitAsync` and migrated 9 of 10 callers.
- Issue #1535 + ADR (Phase B) shipped the `DomainEventOutboxProcessor` pattern that an Option A implementation would mirror.
- CLAUDE.md § "ADR-060" documents an analogous pre/post-save split for the LiveSession concurrency model.

Closes #2407.
