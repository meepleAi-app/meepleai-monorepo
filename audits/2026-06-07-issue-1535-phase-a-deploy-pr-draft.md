# Issue #1535 — Phase A Hybrid Deploy: PR Description Draft

> **Purpose**: this document is the canonical PR description template for the
> Phase A merge (Hybrid mode). It also doubles as the operator runbook for the
> 24h staging soak that gates the merge to `main-dev`. Reuse the content
> verbatim in the GitHub PR body — the Definition-of-Done checklist at the end
> is the gate Reviewer #1 will tick.

---

## Summary

This PR ships the **post-commit domain-event outbox** (issue #1535) defaulted to
`DomainEventOutbox:Mode = "Hybrid"` in both `appsettings.Production.json` and
`appsettings.Staging.json`. Net behaviour change for production = **ZERO**:
both the outbox row INSERT AND the inline `MediatR.Publish` fire on every
`SaveChangesAsync`. Consumers see 2× dispatch during the soak window — this is
intentional, it stresses the idempotency contract documented in
[`audits/2026-06-06-issue-1535-consumer-idempotency-audit.md`](2026-06-06-issue-1535-consumer-idempotency-audit.md).

**Phase B (a separate PR, after 24h staging soak)** flips the single
`Mode` field to `OutboxOnly`. That cutover is documented in plan §T9.

---

## What ships

| Layer | Component | Status |
|---|---|---|
| Schema | `domain_event_outbox` table + 2 partial indexes | T1 ✅ |
| Domain | `DomainEventOutboxEntity` (Enqueue/MarkSent/MarkRetry/MarkFailed/RearmFromFailed) | T1, T6 ✅ |
| Application | `DomainEventTypeResolver` + alias-then-FullName lookup | T2 ✅ |
| Infrastructure | `MeepleAiDbContext.SaveChangesAsync` Hybrid routing | T3 ✅ |
| Background | `DomainEventOutboxProcessor` (poll + drain + retry budget) | T4, T5 ✅ |
| Observability | 4 counters + 3 ObservableGauges + 3 alert rules | T6 ✅ |
| Admin | `/api/v1/admin/event-outbox/{stats,failed,pending,{id}/retry}` | T6 ✅ |
| Acceptance | 4 integration tests + 1 SKIP (concurrency hardening tracker) | T7 ✅ |

**Config flip in this PR**: `appsettings.Production.json` +
`appsettings.Staging.json` add `DomainEventOutbox:Mode = "Hybrid"` explicitly.
This was the default at the binding level — the explicit override exists so
the Phase A → Phase B transition is a one-line, blame-traceable diff.

---

## What the 24h staging soak verifies (DoD gates)

The soak is the gate for merging this PR. **Do not merge until all six
checkboxes flip green in the staging Grafana board.**

### Gate 1 — Arrival rate ≈ dispatch rate

```promql
rate(meepleai_domain_event_outbox_enqueued_total[5m])
  ≈
rate(meepleai_domain_event_outbox_dispatched_total[5m])
```

Within the retry window, the two rates must track each other. A widening gap
(enqueue ahead of dispatch by > 100 rows sustained) indicates the processor
cannot keep up with the inline-publish path — backlog will grow, leading to
Gate 5 alert.

### Gate 2 — Zero terminal failures

```promql
rate(meepleai_domain_event_outbox_failed_terminal_total[1h]) == 0
```

Hybrid mode is the safe-by-default rollout. The inline publish path SHOULD
absorb every legitimate dispatch, leaving the outbox processor with at most
retries (transient errors). Any terminal failure during Phase A is a real bug
— **diagnose before merging**.

### Gate 3 — Consumer behaviour unchanged

Manual smoke check via the admin dashboards + product flows:

- [ ] No duplicate `cache.RemoveByTag()` log lines per request (would indicate
      the inline path AND the outbox path both fired for the same event AND
      the consumer is not naturally idempotent).
- [ ] No double-email reports in `mailtrap` (staging email sink).
- [ ] No double-row inserts in `notification_queue` for the same `event_id`.
- [ ] No `SignalR.OnConnected` deduplication errors in the FE console for the
      tester sessions.

### Gate 4 — Latency p95 < 10s (DoD-9)

```promql
histogram_quantile(0.95,
  rate(meepleai_domain_event_outbox_dispatch_latency_seconds_bucket[5m]))
```

> ⚠ **Note**: the dispatch-latency histogram is not yet wired in T6 — only the
> 4 counters + 3 gauges are. The dispatch-latency check below uses the proxy
> metric `oldest_pending_age_seconds < 10`. The full histogram is tracked as
> a T8 follow-up before the production cutover (T9).

```promql
meepleai_domain_event_outbox_pending_oldest_age_seconds < 10
```

If the oldest Pending row sits longer than 10s in steady-state traffic, the
processor's poll interval (5s) or batch size (100) is undersized — **tune
DomainEventOutbox config before merging**.

### Gate 5 — Three Prometheus alerts silent

Throughout the 24h window:

- [ ] `domain_event_outbox_backlog_high` — NOT firing
- [ ] `domain_event_outbox_stale_pending` — NOT firing
- [ ] `domain_event_outbox_failed_spike` — NOT firing

A single firing during the soak fails the gate. Investigate via the
`/admin/event-outbox/{failed,pending}` endpoints and the LastError messages.

### Gate 6 — Admin surface smoke

- [ ] `GET /api/v1/admin/event-outbox/stats` returns 200 with the expected
      shape and non-stale data (PendingCount changes over the polling window).
- [ ] `GET /api/v1/admin/event-outbox/failed?limit=10` returns 200 with an
      empty list (Gate 2 cross-check).
- [ ] `GET /api/v1/admin/event-outbox/pending?limit=10` returns 200, oldest
      row's EnqueuedAt < now − pollIntervalSeconds.
- [ ] `POST /api/v1/admin/event-outbox/{id}/retry` on a known Failed row
      (manufactured for the test) returns 204; subsequent GET shows Pending.

---

## Rollback path

If any gate fails during the soak OR after merge to production, **immediately**
flip:

```diff
- "Mode": "Hybrid"
+ "Mode": "InlineOnly"
```

in `appsettings.Production.json` and redeploy. `InlineOnly` reverts to the
exact pre-#1535 behaviour — the outbox rows continue to accumulate but the
processor's batch will skip them at deserialization time (resolver returns a
type; mediator publishes; row → Sent). The processor will catch up after the
flip is reverted; no manual cleanup needed for ≤ 24h of accumulated rows.

For a longer rollback window (> 24h), set `Mode = "InlineOnly"` AND truncate
the table:

```sql
TRUNCATE TABLE domain_event_outbox;
```

(safe because in `InlineOnly` mode the rows are never the source of dispatch —
they are passive duplicates of `MediatR.Publish` calls).

---

## Soak procedure (operator runbook)

```bash
# 1. Merge this PR to main-dev (after Reviewer #1 approval — they tick the
#    DoD checkbox below based on this draft).

# 2. CI auto-deploys to staging (workflow: deploy-staging.yml). Confirm:
gh run watch --branch main-dev --workflow deploy-staging.yml

# 3. Smoke the admin surface (Gate 6).
TOKEN=$(./scripts/get-staging-admin-token.sh)
curl -H "Cookie: meepleai_session=$TOKEN" https://staging.meepleai.app/api/v1/admin/event-outbox/stats

# 4. Open the Grafana board.
open https://grafana.staging.meepleai.app/d/issue-1535-domain-event-outbox

# 5. Set a 24h reminder (T+24h from step 2 timestamp). Re-check Gates 1–5.

# 6. On clean exit (all 6 gates green at T+24h):
#    a. Open the Phase B PR (single-line diff: Hybrid → OutboxOnly).
#    b. Reference this audit doc as the "Phase A soak evidence".

# 7. On gate failure: revert via the Rollback path above. Triage. Re-open
#    a fresh Phase A PR with the diagnosis + fix.
```

---

## Definition of Done — checklist for Reviewer #1

- [ ] Code review of the Phase 1–3 deliverables (T1–T6) passes.
- [ ] T7 acceptance tests (4/4 + 1 SKIP) PASS locally on the reviewer's branch.
- [ ] The 24h staging soak window has elapsed without incident.
- [ ] All six gates above are green in the staging Grafana board.
- [ ] No `domain_event_outbox_failed_terminal_total` increment over the 24h.
- [ ] Admin surface smoke (Gate 6) passes on the reviewer's last attempt.
- [ ] The Phase B follow-up PR is staged as a draft, ready to flip the single
      `Mode` field after this PR merges.

---

## Phase B preview (not in this PR)

```diff
# appsettings.Production.json (Phase B PR — separate)
   "DomainEventOutbox": {
-    "Mode": "Hybrid"
+    "Mode": "OutboxOnly"
   }
```

Plan §T9 (Phase B cutover) opens after Phase A merges + 7 production days
stable. T10 (cleanup — remove `InlineOnly`, restore `[AtomicAudit]` on
`RotateProviderKeyCommand`, ship consumer contract doc) follows after T9
+ 7 days stable.

---

**Related files** (read before reviewing):

- [`docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md`](../docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md) — full plan
- [`docs/superpowers/specs/2026-06-06-issue-1535-event-outbox-design.md`](../docs/superpowers/specs/2026-06-06-issue-1535-event-outbox-design.md) — design spec
- [`audits/2026-06-06-issue-1535-event-outbox-kickoff.md`](2026-06-06-issue-1535-event-outbox-kickoff.md) — three-amigos Q1–Q5 lockdown
- [`audits/2026-06-06-issue-1535-consumer-idempotency-audit.md`](2026-06-06-issue-1535-consumer-idempotency-audit.md) — consumer-contract audit
