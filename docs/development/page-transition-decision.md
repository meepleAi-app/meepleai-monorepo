# Decision Doc: PageTransition Integration (Gate G0.3 — #293)

**Date:** 2026-04-09
**Decision Owner:** @user
**Status:** 🟡 DRAFT — awaiting user decision
**GitHub Issue:** meepleAi-app/meepleai-monorepo#293
**Component:** `apps/web/src/components/ui/animations/PageTransition.tsx` (82 lines, Framer Motion)

## Context

`PageTransition` was built in Issue #2965 Wave 8 as a wrapper for smooth route transitions (fade/slide/scale variants). It has never been wired into any layout. Next.js App Router uses streaming + Suspense, which can interact badly with naive client-side wrapping.

## Options

### Option A — Pilot in `(chat)/layout.tsx` only
**Pros:**
- Smallest blast radius — only chat routes affected
- Chat thread navigation benefits most from visual continuity (context switches feel jarring today)
- `(chat)/layout.tsx` is already a client component; no streaming interaction issue

**Cons:**
- Users who never use chat won't see the improvement
- Creates inconsistency: chat routes animate, library/admin don't

**Effort:** ~3h

### Option B — Pilot in `(authenticated)/layout.tsx`
**Pros:**
- Covers the majority of logged-in user navigation
- More visible improvement
- Good balance of scope vs risk

**Cons:**
- Medium blast radius (library, agents, games, sessions all affected)
- Possible interaction with Suspense boundaries in nested routes
- Requires more QA

**Effort:** ~5h (includes staging validation)

### Option C — Both `(chat)` + `(authenticated)`
**Pros:**
- Full coverage of user-facing app
- Consistent feel

**Cons:**
- Higher risk
- Testing burden doubles

**Effort:** ~8h

### Option D — Decline (close #293)
**Pros:**
- Zero risk
- Removes an orphan from the backlog

**Cons:**
- Loses a UX improvement opportunity
- PageTransition component wasted effort

**Effort:** 0h (just close the issue)

## Recommendation

**Option A — Pilot in `(chat)/layout.tsx` only** — with feature flag.

**Rationale:**
- Chat navigation is where users feel the jarring context switch most (switching threads, jumping between game chats)
- `(chat)` layouts are already client components (no streaming risk)
- Feature flag allows A/B testing without blocking a release
- Low effort, low risk, high learning

**Guardrails:**
1. Add `NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS` env var (default `false`)
2. Wrap `children` in `<PageTransition variant="fade">` conditionally on the flag
3. Staging validation: 10 chat thread switches, verify no flicker/hydration mismatch
4. Lighthouse delta ≤ 5%
5. If GO in staging, enable in prod; expand to `(authenticated)` in a follow-up ticket

## Decision

**Choice:** [x] A  [ ] B  [ ] C  [ ] D
**Signed:** @user (autonomous execution delegation)
**Date:** 2026-04-09
**Rationale:** Follow spec-panel recommendation — pilot in `(chat)/layout.tsx` with feature flag `NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS`. Narrow scope, chat navigation benefits most, no streaming interaction risk.

## Go/No-Go Criteria (post-pilot)

- [ ] No visual flicker on 10 consecutive navigations
- [ ] No hydration mismatch warnings in console
- [ ] Lighthouse performance ≥ current - 5%
- [ ] No regression in E2E test suite
- [ ] 2+ team members approve visual feel

## Out of Scope

- Global `app/layout.tsx` wrapping (too high blast radius; server component)
- Custom variants beyond fade (save for follow-up)
- Per-route custom transitions (YAGNI)

## If Decision Is D (Decline)

Actions:
1. Close GitHub issue #293 with rationale comment linking to this doc
2. Mark `PageTransition.tsx` for deletion in a follow-up cleanup ticket
3. Update `orphan-components-integration-plan.md` to remove #293 from backlog
