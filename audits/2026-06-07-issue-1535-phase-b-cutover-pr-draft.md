# Issue #1535 — Phase B Cutover: PR Description Draft

> **Purpose**: canonical PR description template for the cutover from
> `Mode = "Hybrid"` to `Mode = "OutboxOnly"`. Also the operator runbook for the
> staging + production soak windows that gate the merge.

---

## Summary

Single-line config diff: `DomainEventOutbox:Mode` flips from `"Hybrid"` to
`"OutboxOnly"` in both `appsettings.Production.json` and
`appsettings.Staging.json`. **This PR closes the original #1535 bug**: the inline
`MediatR.Publish` inside `SaveChangesAsync` is OFF — the
`DomainEventOutboxProcessor` BackgroundService becomes the SOLE dispatcher of
domain events.

Behaviour change vs Phase A:
- Inline `MediatR.Publish` no longer fires within `SaveChangesAsync`.
- All domain events flow through the outbox row + post-commit drain.
- Consumers must be idempotent (verified in the
  [`consumer idempotency audit`](2026-06-06-issue-1535-consumer-idempotency-audit.md)).
- A rolled-back outer transaction now NEVER causes a downstream side-effect to
  escape — the original race that motivated #1535 is closed.

**Prerequisites checked**:
- ✅ Phase A merged to `main-dev` (commit `23dc88727`)
- ✅ 24h staging soak in Hybrid mode passed all 6 DoD gates (see Phase A runbook)
- ✅ T6 code review's 15 findings all closed (commit `bbba404bc`)
- ✅ T7 acceptance tests confirm OutboxOnly behaves as designed (4 PASS + 1 SKIP)

---

## DoD gates for the Phase B soak

### Staging soak (24h)

Same 6 gates as Phase A (see
[`Phase A runbook`](2026-06-07-issue-1535-phase-a-deploy-pr-draft.md))
plus a Phase-B-specific cross-check:

#### Gate B1 — Single-source dispatch verified

```promql
rate(meepleai_domain_event_outbox_dispatched_total[5m])
  ≈
rate(meepleai_domain_event_outbox_enqueued_total[5m])
```

In Phase A this ratio was approximately 2× because inline + outbox dispatch both
fired. In Phase B it MUST be ≈ 1× (every enqueue results in exactly one
dispatch). The transition itself: on the deploy timestamp `T0`, dispatched_total
rate should drop from 2× to 1× within one poll interval. A ratio sustained at
1.5× or anything > 1.05× indicates the inline path is still firing for some
reason — investigate.

#### Gate B2 — Zero `domain_event_log.dispatch_failures_total` increment

```promql
rate(meepleai_domain_event_log_dispatch_failures_total[1h]) == 0
```

The inline path was the source of these counters. Post-cutover they should stop
incrementing entirely. A non-zero rate here means an inline path is still alive
somewhere (likely a code path that calls `_mediator.Publish` directly).

### Production soak (7 days)

After staging passes Gate B1+B2 for 24h, deploy to production. Monitor for 7
days with the same 6 gates from Phase A PLUS B1+B2. Specific items to watch in
the first 72h:

- [ ] Consumer error rate unchanged vs Phase A baseline (no idempotency bugs
      missed by the audit).
- [ ] Mean dispatch latency from `EnqueuedAt` to `DispatchedAt` < 10s p95.
- [ ] Backlog never exceeds 100 rows in steady state.
- [ ] No new `domain_event_outbox_failed_terminal_total` increments.

---

## Rollback path

**Reverting Phase B is a single-line config diff back to Hybrid + redeploy.**

```diff
   "DomainEventOutbox": {
-    "Mode": "OutboxOnly"
+    "Mode": "Hybrid"
   }
```

After the redeploy:
- Inline `MediatR.Publish` resumes immediately for new SaveChangesAsync calls.
- Outbox rows in Pending continue to be drained by the processor (double
  dispatch resumes for backlogged rows).
- Consumers may see 2× dispatch during the rollback window — same as Phase A.

No data loss; no migration; no manual cleanup of `domain_event_outbox`.

---

## Soak procedure (operator runbook)

```bash
# 1. Merge this PR to main-dev. CI deploys to staging.
gh run watch --branch main-dev --workflow deploy-staging.yml

# 2. Confirm staging picked up the new Mode.
TOKEN=$(./scripts/get-staging-admin-token.sh)
curl -H "Cookie: meepleai_session=$TOKEN" \
  https://staging.meepleai.app/api/v1/admin/event-outbox/stats
# Stats should show Pending growing modestly + Sent rising at ~1× enqueue rate.

# 3. Watch Grafana panel for 24h.
open https://grafana.staging.meepleai.app/d/issue-1535-domain-event-outbox

# 4. T+24h: confirm all 6 Phase A gates + B1 + B2. Open the production deploy.

# 5. Deploy to production via the normal release workflow.

# 6. Production soak: 7 days monitoring. At T+7d, this PR can be considered
#    fully stabilised — Phase 5 T10 cleanup (remove InlineOnly enum, restore
#    [AtomicAudit] on RotateProviderKeyCommand) is unblocked.
```

---

## Definition of Done — Reviewer #1 checklist

- [ ] Phase A merged + 24h staging soak completed cleanly.
- [ ] T6 code review's 15 findings all merged (`bbba404bc`).
- [ ] T7 acceptance tests pass on reviewer's branch (4 PASS + 1 SKIP).
- [ ] Staging soak in Phase B has elapsed without incident (24h).
- [ ] All 6 Phase A gates + B1 + B2 are green.
- [ ] No `domain_event_outbox_failed_terminal_total` increment over the 24h.
- [ ] Admin surface smoke passes (same as Phase A Gate 6).
- [ ] Phase 5 T10 cleanup PR is staged as a draft.

---

## Phase 5 T10 preview (separate PR, after 7-day prod soak)

T10 removes `DomainEventDispatchMode.InlineOnly` and restores
`[AtomicAudit]` on `RotateProviderKeyCommand`. See plan §T10. T10 is gated
on this PR's 7-day production soak — DO NOT merge T10 until that elapses.
