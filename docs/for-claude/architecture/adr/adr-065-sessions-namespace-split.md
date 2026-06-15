# ADR-065 — `/sessions/[id]` vs `/sessions/live/[sessionId]`: namespace split documented, slug harmonisation deferred

**Date**: 2026-06-14
**Status**: Accepted
**Issue**: #2314 (spawned from #2152)

## Context

DS-17 Phase B audit Section C flagged what appeared to be a route-namespace
duplication:

- `/sessions/[id]/*` (8 pages, slug `[id]`)
- `/sessions/live/[sessionId]/*` (5 pages, slug `[sessionId]`)

with overlap on `/sessions/[id]/live` ↔ `/sessions/live/[sessionId]` and on
`*/players` (both branches).

Closer reading shows the two trees serve different features, not the same
feature accessed via two URLs:

| Tree | Feature | Origin |
|---|---|---|
| `/sessions/[id]/*` | **General session detail** with multi-state Live mode (Wave D, issue #746). Uses `useSearchParams()` for SSOT (?tab, ?mtab, ?fixture, ?state, ?dialog). Suspense-bounded. Hosts the 7 sub-routes (join / notes / play / players / scoreboard / live / agent-attached). | Wave D.2 Foundation #746 |
| `/sessions/live/[sessionId]/*` | **"Improvvisata" workflow** — Game Night Improvvisata Tasks 13–15. Renders `SessionCardParent` (desktop) + `PlayModeMobile` (4-tab mobile, <lg). Distinct UX cluster with its own SessionCardParent, photos sub-route, and `[sessionId]` semantic slug. | Game Night Improvvisata Phase 5 Task 4 |

The two clusters share a URL prefix but encode genuinely different
product surfaces with different rendering pipelines and different
component trees. Collapsing them into one would mask that distinction.

## Decision

1. **Keep both namespaces** — they document the product split.
2. **Defer slug harmonisation** (`[sessionId]` → `[id]`) to a focused
   follow-up PR. The semantic slug `[sessionId]` reads better in the
   Improvvisata tree (where the session is a first-class entity); the
   generic `[id]` reads better in the Wave D tree (where the slug is one
   of several entity params the route group accepts).
3. **Document the split** here (this ADR) + add a one-line comment in
   `apps/web/src/config/navigation.ts` near the `BOTTOM_TAB_NAV_IDS` /
   `TOP_BAR_NAV_IDS` declarations so the contract is discoverable from
   the navigation side too.
4. **Runtime linkers must pick deliberately**: anything wired to "the
   active live session" (e.g. the `LiveSessionPill` shipped in #2150)
   targets `/sessions/${sessionId}/live` (Wave D tree). Anything wired to
   the Improvvisata workflow targets `/sessions/live/${sessionId}/...`.

## Consequences

### Positive
- Two genuinely-different surfaces stay distinguishable via URL.
- Zero migration cost; existing tests + LiveSessionPill (#2150) keep working unchanged.
- Future readers find this decision via ADR + navigation.ts comment, not by re-discovering it through code archaeology.

### Negative / debt
- Slug inconsistency (`[id]` vs `[sessionId]`) survives. Mitigated by ADR; a focused harmonisation PR can flip `[sessionId]` → `[id]` once we confirm no internal `useParams<{sessionId: …}>()` consumer assumes the semantic slug.
- New contributors may parse the two trees as duplicates. This ADR is the canonical answer.

### Follow-up clean-up (NOT this ADR)
- Audit `useParams<{sessionId: string}>()` consumers — if all read the value generically and don't depend on the slug NAME, a future PR can rename `/sessions/live/[sessionId]/*` → `/sessions/live/[id]/*` for slug uniformity. Tracked separately if/when worth the churn.

## References

- DS-17 Phase B audit: `audits/2026-06-10-nav-chrome-bgg-naming-audit.md` § Section C.5a
- Wave D Foundation introduction: PR #746
- Improvvisata workflow introduction: Game Night Improvvisata Phase 5 Tasks 13–15
- Live session pill (uses `/sessions/[id]/live`): PR #2322 (#2150)
- Parent umbrella: #2152
