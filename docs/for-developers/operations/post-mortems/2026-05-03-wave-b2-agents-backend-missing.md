# Post-mortem — Wave B.2 `/agents` route shipped without working backend (2026-05-03)

> Illustrative example using a real incident, lightly sanitised. See [post-mortem-template.md](../post-mortem-template.md) for the canonical template.

## Header

| Field | Value |
|---|---|
| **Incident ID** | `INC-2026-001` |
| **Date detected** | 2026-05-03 09:14 UTC |
| **Date resolved** | 2026-05-04 02:47 UTC (autonomous overnight recovery) |
| **Duration** | 17h 33m |
| **Severity** | P2 (core feature degraded — `/agents` page rendered with stub data, no functionality) |
| **Environment** | staging + main-dev (no prod impact — pre-prod-deploy) |
| **Author** | @degrassiaaron |
| **Reviewers** | @meepleAi-app, @claude-bot |
| **Status** | published |

## Summary

Wave B.2 PR #637 (`feat(agents): v2 brownfield migration`) merged 2026-04-30 with green CI and visual-regression baselines, but the `/agents` route surfaced a UI shell wired to backend endpoints that did not yet exist in production code. The visual-test fixture pattern masked the gap because all 5 FSM states could be rendered via `?state=` URL override without hitting the API. Detected when @degrassiaaron loaded the route on staging post-merge and noted the absent network calls. Recovered autonomously overnight via 6 follow-up PRs (#643, #644, #646, #662, #645, #664) that implemented the missing AgentsAggregate query handler + 3 cache layers.

## Impact

| Dimension | Detail |
|---|---|
| **Users affected** | 0 (pre-prod) |
| **Functionality impacted** | `/agents` route on `main-dev` and `main-staging`: UI rendered but no live data; filters non-functional |
| **Data integrity** | None |
| **Revenue impact** | None |
| **Reputation impact** | Internal-only (caught by author before invited-user testing) |
| **SLO breach** | None |

## Timeline

```
2026-04-30 11:21  ACTION   — PR #637 merged to main-dev (CI green, A11y E2E green, visual baselines green)
2026-05-03 09:14  DETECT   — Author loads /agents on staging; Network tab shows zero API calls
2026-05-03 09:22  DECIDE   — Confirm gap is structural (backend missing) vs config (env var)
2026-05-03 10:05  DECIDE   — Spec-panel review identifies AgentsAggregate query handler not yet implemented; 3 cache layers (popular, recent, by-game) referenced in tests but not in code
2026-05-03 14:30  ACTION   — Dispatch 7-phase autonomous overnight execution plan via codex-coordinator
2026-05-04 02:47  RESOLVE  — All 6 follow-up PRs merged, /agents fully functional on staging; smoke nightly workflow now active
```

## Root cause

1. **Why did PR #637 ship without working backend?**  The CI pipeline did not detect that frontend hooks referenced backend endpoints that did not yet exist in the deployed API.
2. **Why didn't CI detect the missing endpoints?**  Visual regression tests run against the `?state=` URL override fixture, which short-circuits TanStack Query before any network call. Unit tests mock the hook layer.
3. **Why does the fixture pattern allow this?**  It was designed to make pre-backend frontend work parallelisable (test UI states without waiting on API delivery). This is a deliberate trade-off — speed of frontend iteration vs end-to-end contract verification.
4. **Why was there no compensating endpoint-binding gate?**  No CI step verifies that every hook used in a v2 component has a corresponding route registered in `apps/api/src/Api/Routing/`.
5. **Why was the gate not in place at the time of merge?**  The need for it was discovered during this incident. Pattern P19 (backend-95%-pre-complete) emerged later (PR #811/#812) and would have flagged this gap.  ← **root cause**

### Unknowns

- Whether other Wave B PRs (B.1 #635, B.3 #638) carry similar latent gaps. Action item to audit.

## Resolution

- **Rollback scenario**: None — fix-forward chosen because frontend was already merged and rollback would have removed UI shell that other in-flight PRs depended on.
- **Recovery PRs**: #643 (AgentsAggregate query handler), #644 (popular agents cache layer), #646 (recent agents), #662 (by-game agents), #645 (cache eviction tags), #664 (smoke nightly workflow activation)
- **Manual intervention**: `make staging-restart` after each backend PR merge to invalidate Redis cache without restart of cache layer

## What went well

- Spec-panel review (10:05) correctly identified all 3 missing cache layers + handler in a single pass, enabling parallel autonomous overnight execution
- 6 PRs merged in 17h via codex-coordinator dispatch without human intervention
- Visual baselines + a11y gates did not regress during recovery (frontend untouched)

## What went poorly

- Visual-test fixture pattern, designed for parallelisation, allowed shipping of UI dependent on non-existent backend. The trade-off was implicit and not documented in CLAUDE.md "Known Pitfalls".
- The smoke nightly workflow (post-merge integration test on staging) was missing for `/agents` route. It was the obvious compensating control.
- No PR template checkbox asks "Are all referenced API endpoints implemented and registered?"

## Action items

| # | Owner | Due date | Priority | Status | Action | Tracking issue |
|---|---|---|---|---|---|---|
| 1 | @degrassiaaron | 2026-05-10 | P1 | done | Audit Wave B.1 #635 + B.3 #638 for same latent gap | #647 |
| 2 | @meepleAi-app | 2026-05-15 | P2 | done | Add `Endpoint binding` CI check: grep hooks, verify route registered | #649 |
| 3 | @degrassiaaron | 2026-05-12 | P2 | done | Document fixture pattern trade-off in CLAUDE.md "Known Pitfalls" | #651 |
| 4 | @meepleAi-app | 2026-05-30 | P3 | open | Generalise smoke nightly workflow to all v2 routes (currently per-route) | #659 |

## Lessons learned

The visual-test fixture pattern that enabled frontend/backend parallel work also masked a structural CI gap: there is no automated verification that hooks reference endpoints which exist in deployed code. Future Wave migrations must include either (a) integration test that exercises every hook against a deployed stub, or (b) static-analysis CI check mapping hook → route. The "feature flag for incomplete backend" pattern (PR #819 Phase 4b stub returning 501) is preferred over silent rendering of empty data, because it surfaces the gap to the user instead of hiding it behind a green CI.

## Cross-links

- Parent incident issue: (none — fix-forward without separate incident tracking issue)
- Related runbook section: [rollback-runbook §6 — Three rollback scenarios](../rollback-runbook.md#6-three-rollback-scenarios) (consulted but not used; fix-forward chosen)
- Related PRs: #637 (incident trigger), #643/#644/#646/#662/#645/#664 (recovery cluster)
- Spec maturation: see session memory `project_wave-b2-agents-backend-missing.md`
