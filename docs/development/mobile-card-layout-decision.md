# Decision Doc: MobileCardLayout Adoption (Gate G0.3 — #295)

**Date:** 2026-04-09
**Decision Owner:** @user (product)
**Status:** 🟡 DRAFT — awaiting product decision
**GitHub Issue:** meepleAi-app/meepleai-monorepo#295
**Component:** `apps/web/src/components/ui/data-display/meeple-card/mobile/MobileCardLayout.tsx`

## Context

`MobileCardLayout` provides **tactile one-card-at-a-time browsing** on mobile: hand sidebar (vertical card list) + focused card (large center view) + swipe gesture. Companion `MobileDevicePreview` renders a phone frame for dev validation.

**Current mobile UX:** Mobile pages use `<MeepleCard variant="list" />` or `<MeepleCard variant="compact" />` with vertical scroll. **No tactile browsing anywhere in production.**

**Only consumer:** `app/(public)/dev/meeple-card/page.tsx` (demo route, not a real user page).

## The Product Question

**Is tactile one-card-at-a-time browsing worth adding UX complexity on mobile?**

This is NOT a technical question. The component works. The question is whether users will benefit from an alternative browsing mode.

## Options

### Option A — Adopt in library discovery (mobile)
**Target:** `apps/web/src/app/(authenticated)/library/page.tsx` on mobile viewport

**User value hypothesis:**
- Discovery mode: swiping through games one-at-a-time feels like browsing a card deck
- Focused attention on each game (cover, rating, metadata) vs scroll-scanning
- Mimics Tinder/Bumble pattern users are familiar with

**Pros:**
- Highest-value mobile page (most traffic)
- Discovery context fits tactile browsing

**Cons:**
- Large library (100+ games) = tedious one-at-a-time
- Users may prefer list scroll for scanning
- Requires layout toggle (list vs swipe) + user preference storage

**Effort:** ~12h (toggle + preference + integration + mobile E2E)

### Option B — Adopt in session resume
**Target:** `apps/web/src/app/(authenticated)/sessions/page.tsx` or similar

**User value hypothesis:**
- Active/paused sessions are few (<10 typically)
- Tactile review of each session helps decide which to resume
- Swipe to pick, tap to open

**Pros:**
- Natural fit (small dataset)
- Clear user goal: "pick a session to resume"
- No overwhelm issue

**Cons:**
- Lower traffic surface
- Sessions page may not exist or may be redesigned

**Effort:** ~8h

### Option C — Adopt in game discovery / onboarding
**Target:** first-time user flow, "swipe through games to build your library"

**Pros:**
- Onboarding benefits most from delight features
- One-time friction acceptable for learning

**Cons:**
- Requires onboarding flow redesign
- Out of scope for this ticket

**Effort:** ~20h

### Option D — Decline (close #295 + delete component)
**Pros:**
- Zero risk
- Removes orphan from backlog
- −900 LOC (approx, all mobile files in `meeple-card/mobile/`)

**Cons:**
- Loses a prepared feature
- MobileDevicePreview (phone frame) also becomes orphan

**Effort:** 0h (just close + delete)

## Recommendation

**Option D — Decline and delete**, unless product has strong user research signal for tactile mobile browsing.

**Rationale:**
1. **No forcing function:** no user request, no A/B data, no PM pitch in the backlog
2. **Risk of feature debt:** building an alternative mobile browsing mode that users don't discover = churn
3. **Opportunity cost:** 8-12h of mobile dev time is better spent on performance, accessibility, or offline support
4. **Preserve option:** if product later wants to revisit, the component can be restored from git history
5. **YAGNI principle:** current `MeepleCard variant="list"` works and is in production

**Alternative recommendation (if Option D feels too aggressive):** Choose **Option B** (session resume) as a low-risk pilot — small dataset, clear user goal, minimal scope.

## Decision

**Choice:** [ ] A  [ ] B  [ ] C  [ ] D
**Signed:** _____________________
**Date:** _____________________

## Product Questions to Answer Before Choosing

1. Do we have user research showing mobile users want alternative browsing modes? (Y/N)
2. What is the success metric for this feature? (session duration, games added, engagement?)
3. Is there a PM owner for mobile UX decisions? (name)
4. What is the cost of reverting if adoption fails? (link to component + feature flag strategy)
5. Are there analogous features in competitor apps (BGG, Ludopedia) that validate the pattern?

**If all 5 answers are weak or unknown → Option D is correct.**

## Go/No-Go Criteria (if A, B, or C chosen)

- [ ] Product research document linked
- [ ] A/B test plan documented
- [ ] Fallback/toggle UX designed
- [ ] Mobile E2E test suite (iOS Safari + Android Chrome)
- [ ] Usability test with ≥2 real users
- [ ] Lighthouse performance ≤ current - 5%

## If Decision Is D (Decline + Delete)

Actions:
1. Close GitHub issue #295 with rationale comment linking to this doc
2. Create follow-up cleanup ticket: "chore: remove unused MobileCardLayout + MobileDevicePreview + related mobile helpers"
3. Delete files:
   - `components/ui/data-display/meeple-card/mobile/MobileCardLayout.tsx`
   - `components/ui/data-display/meeple-card/mobile/MobileDevicePreview.tsx`
   - `components/ui/data-display/meeple-card/mobile/HandCard.tsx`
   - `components/ui/data-display/meeple-card/mobile/HandSidebar.tsx`
   - `components/ui/data-display/meeple-card/mobile/FocusedCard.tsx`
   - `components/ui/data-display/meeple-card/mobile/MobileCardDrawer.tsx` (verify no usage first)
   - `components/ui/data-display/meeple-card/mobile/drawerTabs.ts`
4. Update `/dev/meeple-card` demo page to remove mobile section
5. Delete `components/showcase/stories/mobile-card-layout.story.tsx` (the new one added in `chore/showcase-orphan-audit`)
6. Update `components/showcase/stories/index.ts` + `metadata.ts` to remove entry
7. Update `orphan-components-integration-plan.md` to remove #295
