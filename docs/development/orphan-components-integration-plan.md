# Orphan Components Integration Plan

**Date:** 2026-04-08
**Context:** Showcase audit (`/admin/ui-library`) revealed 9 components with stories but no adoption in production pages. Each was designed for a future integration that never landed.

## Audit Summary

| Component | Path | Orphan Type |
|---|---|---|
| RatingStars | `ui/data-display/rating-stars.tsx` | Public API variant |
| TagStrip | `ui/tags/TagStrip.tsx` | Public API variant |
| ChatMessage | `ui/meeple/chat-message.tsx` | Refactor pending |
| MeepleAvatar | `ui/meeple/meeple-avatar.tsx` | Transitive (via ChatMessage) |
| AgentStatsDisplay | `ui/agent/AgentStatsDisplay.tsx` | Design divergent |
| CollectionProgressBar | `ui/feedback/collection-progress-bar.tsx` | Gate UX incomplete |
| UpgradePrompt | `ui/feedback/upgrade-prompt.tsx` | Gate UX incomplete |
| PageTransition | `ui/animations/PageTransition.tsx` | Infrastructure not wired |
| MobileCardLayout | `ui/data-display/meeple-card/mobile/MobileCardLayout.tsx` | Mobile UX deferred |

All 9 have been annotated with `@status ORPHAN` JSDoc and tagged `[ORPHAN]` in the showcase metadata.

## Tickets Backlog

The following 6 tickets have been opened on GitHub on 2026-04-08:

| # | Title | Issue |
|---|---|---|
| 1 | Integrate RatingStars in game detail and review surfaces | meepleAi-app/meepleai-monorepo#290 |
| 2 | Ship tier-gate UX kit (UpgradePrompt + CollectionProgressBar) | meepleAi-app/meepleai-monorepo#291 |
| 3 | Refactor ChatMessageList to compose ChatMessage + MeepleAvatar | meepleAi-app/meepleai-monorepo#292 |
| 4 | Wire PageTransition into (chat) or (authenticated) layout | meepleAi-app/meepleai-monorepo#293 |
| 5 | Integrate AgentStatsDisplay in admin agent list | meepleAi-app/meepleai-monorepo#294 |
| 6 | Adopt MobileCardLayout in a mobile browsing surface | meepleAi-app/meepleai-monorepo#295 |

They are grouped by natural boundaries (e.g., UpgradePrompt + CollectionProgressBar ship together as part of the tier-gate UX).

---

### Ticket 1 — Integrate `RatingStars` in game detail and review surfaces

**Type:** Technical Task
**Effort:** S
**Priority:** P3

**Objective**
Replace ad-hoc rating displays in non-MeepleCard surfaces with the standalone `RatingStars` component to unify visual treatment and reduce duplication.

**Scope**
- Audit `library/games/[id]` (game detail page) for inline rating rendering
- Replace with `<RatingStars rating={game.averageRating} maxRating={10} size="lg" showValue />`
- Same audit on review lists, BGG import preview, shared-games detail

**Out of scope**
- Do NOT touch `MeepleCard` internal `parts/Rating.tsx` — it uses token-based styling and is not a duplicate

**DoD**
- [ ] At least 1 real page consumes `RatingStars` (not just showcase)
- [ ] Remove `[ORPHAN]` prefix from story metadata
- [ ] Remove `@status ORPHAN` JSDoc from source file
- [ ] Visual diff snapshot matches design tokens

---

### Ticket 2 — Ship tier-gate UX kit (`UpgradePrompt` + `CollectionProgressBar`)

**Type:** Technical Task / Feature
**Effort:** M
**Priority:** P2
**Related:** Epic #4068, Issue #4179, Issue #4183

**Objective**
Close the tier-gate UX epic by wiring the remaining two components into the library dashboard and feature-gate flows. `TierBadge` (the third component of the kit) already ships in `library/UsageWidget.tsx`.

**Contesto Tecnico**
- File/moduli coinvolti:
  - `apps/web/src/components/library/UsageWidget.tsx` (existing `TierBadge` consumer)
  - `apps/web/src/app/library/page.tsx` (or equivalent dashboard)
  - Feature gate handlers (search for tier checks across `hooks/` or `lib/gates/`)
- Architettura attuale:
  - `TierBadge` shows tier inline in `UsageWidget`
  - No centralized gate handler — tier checks are inline in feature components
  - `CollectionProgressBar` accepts `current/max/label/unit` → needs binding to quota API
  - `UpgradePrompt` has `inline` and `modal` variants → needs trigger flow
- Problema tecnico:
  - No surface shows the user their quota usage before hitting the cap
  - When a free user tries a premium feature, the current block is silent (UX dead end)

**Soluzione Proposta**
1. Add `CollectionProgressBar` row to `UsageWidget` (alongside existing tier display)
2. Bind to `useUserQuota()` hook or equivalent (create if missing)
3. Intercept tier-gated actions (identify 1–3 representative flows: bulk actions, agent creation, storage upload)
4. Show `UpgradePrompt` modal on gated action attempt

**DoD**
- [ ] `CollectionProgressBar` visible in `UsageWidget` showing games count + storage
- [ ] At least 1 tier-gated action triggers `UpgradePrompt` modal
- [ ] `useUserQuota()` hook tested
- [ ] Remove `[ORPHAN]` from both stories
- [ ] Remove `@status ORPHAN` from both source files
- [ ] E2E test for the gate flow

**Effort:** M | **Priority:** P2

---

### Ticket 3 — Refactor `ChatMessageList` to compose `ChatMessage` + `MeepleAvatar`

**Type:** Technical Task (refactor)
**Effort:** L
**Priority:** P2
**Related:** Issue #1831 (UI-004), Issue #3352 (AI Response Feedback System)

**Objective**
Replace inline message rendering in `chat-unified/ChatMessageList.tsx` (311 lines) with the `<ChatMessage>` component, which was designed for this role but never adopted. Revive `MeepleAvatar` transitively.

**Contesto Tecnico**
- File coinvolti:
  - `apps/web/src/components/chat-unified/ChatMessageList.tsx` (current inline renderer)
  - `apps/web/src/components/ui/meeple/chat-message.tsx` (target component)
  - `apps/web/src/components/ui/meeple/meeple-avatar.tsx` (transitive)
- Architettura attuale:
  - `ChatMessageList` manages citations/confidence/feedback inline via JSX
  - `ChatMessage` already provides the same API as a reusable atom
  - Two separate code paths for the same visual element = drift risk
- Problema tecnico:
  - 300+ lines of inline JSX duplicates what `ChatMessage` already does
  - Type mismatch: `ChatMessageList` uses `ChatMessageItem`, `ChatMessage` uses `Citation`
  - Feedback button wiring is custom in `ChatMessageList`, but `ChatMessage` supports it via props

**Soluzione Proposta**
1. Create adapter function `toChatMessageProps(item: ChatMessageItem): ChatMessageProps`
2. Replace inline message block in `ChatMessageList` with `<ChatMessage {...toChatMessageProps(msg)} />`
3. Preserve existing feedback submission handler via callback prop
4. Verify `MeepleAvatar` state mapping (idle/thinking/confident/searching/uncertain) aligns with agent response states
5. Update tests

**Impatto**
- **Performance:** neutral (same render cost)
- **Manutenibilità:** +++ (single source of truth for message UI)
- **Scalabilità:** +++ (new chat surfaces can reuse `ChatMessage` directly)

**Rischi**
- Feedback submission flow regression — requires careful test coverage
- Confidence badge styling delta between inline and component versions
- SSE streaming integration with typing indicator

**DoD**
- [ ] `ChatMessageList` uses `<ChatMessage>` for all message rendering
- [ ] `ChatMessageList` shrinks below 150 lines
- [ ] Existing tests pass (ChatMessageList.test.tsx, RuleSourceCard.test.tsx)
- [ ] Add test: MeepleAvatar state mapping
- [ ] Remove `@status ORPHAN` from ChatMessage + MeepleAvatar
- [ ] Remove `[ORPHAN]` from both stories
- [ ] Manual test: chat thread with streaming, citations, and feedback

---

### Ticket 4 — Wire `PageTransition` into layout(s)

**Type:** Technical Task
**Effort:** S
**Priority:** P3
**Related:** Issue #2965 (Wave 8)

**Objective**
Wrap layout `children` in `<PageTransition>` to provide smooth route transitions app-wide. Defer global rollout in favor of a scoped first adoption.

**Contesto Tecnico**
- File coinvolti:
  - `apps/web/src/app/(chat)/layout.tsx` (proposed first host — contained scope)
  - Secondary option: `apps/web/src/app/(authenticated)/layout.tsx`
  - NOT `app/layout.tsx` (too high blast radius)
- Architettura attuale:
  - Next.js App Router with streaming + Suspense boundaries
  - No route-level motion today
- Problema tecnico:
  - `PageTransition` component exists (fade/slide/scale variants) but no consumer
  - Wrapping `children` in a layout can conflict with streaming — must validate

**Soluzione Proposta**
1. Start with `(chat)/layout.tsx` as pilot (narrow scope, high visibility)
2. Wrap `children` in `<PageTransition variant="fade">`
3. Validate no flickering on client-side navigation
4. If successful, repeat for `(authenticated)/layout.tsx`

**Rischi**
- Streaming/Suspense interaction — may need `<PageTransition>` inside a Suspense boundary
- Framer Motion bundle size impact (already imported)

**DoD**
- [ ] Pilot layout wrapped
- [ ] No regression in streaming pages
- [ ] Motion decision owner sign-off
- [ ] Remove `@status ORPHAN` from source file
- [ ] Remove `[ORPHAN]` from story

---

### Ticket 5 — Integrate `AgentStatsDisplay` in admin agent list

**Type:** Technical Task
**Effort:** S
**Priority:** P3

**Objective**
Surface `AgentStatsDisplay` in admin agent listing pages or dashboard cards where a terse horizontal metadata summary is valuable.

**Contesto Tecnico**
- File coinvolti:
  - `apps/web/src/app/admin/(dashboard)/agents/definitions/page.tsx` (agent list)
  - Possibly: `apps/web/src/components/admin/agents/*` (dashboard widgets)
  - NOT `AgentCharacterSheet.tsx` (uses custom RPG design with `AgentDetailData`, not interchangeable)
- Architettura attuale:
  - Agent list rows show basic name + status inline
  - `AgentCharacterSheet` uses 2x2 `StatPip` grid + mana pips (design divergent)
- Problema tecnico:
  - `AgentStatsDisplay` (horizontal flex with capabilities badges + model info) has no consumer
  - Admin list surfaces would benefit from a compact metadata summary

**Soluzione Proposta**
1. Identify agent list table rows or dashboard cards
2. Bind `AgentMetadata` from API to `<AgentStatsDisplay metadata={...} />`
3. Type coercion: `AgentDefinition` → `AgentMetadata` (may need mapper)

**DoD**
- [ ] `AgentStatsDisplay` consumed in at least 1 admin page
- [ ] Capability badges and model info render correctly
- [ ] Remove `@status ORPHAN` from source file
- [ ] Remove `[ORPHAN]` from story

---

### Ticket 6 — Adopt `MobileCardLayout` in a mobile browsing surface

**Type:** Feature / Technical Task
**Effort:** M
**Priority:** P3

**Objective**
Wire `MobileCardLayout` (hand sidebar + focused card + swipe gesture) into a real mobile surface. Currently only consumed by `/dev/meeple-card` demo.

**Contesto Tecnico**
- File coinvolti:
  - `apps/web/src/components/ui/data-display/meeple-card/mobile/MobileCardLayout.tsx`
  - Target: a mobile-first page (e.g., library on mobile, session list on mobile)
- Architettura attuale:
  - Real mobile pages use `MeepleCard variant="list"` or `variant="compact"` with vertical scroll
  - No tactile one-card-at-a-time browsing anywhere in production
- Problema tecnico:
  - Mobile UX is scroll-heavy; the swipe/hand layout was built to offer an alternative but never activated
  - Product decision needed: is tactile browsing worth the added complexity?

**Soluzione Proposta**
1. Product decision: identify which mobile surface benefits from swipe browsing (e.g., library discovery, session resume)
2. Add a layout toggle (list vs. swipe) on the chosen surface
3. Bind existing data source to `MeepleCardProps[]`
4. Validate on actual mobile devices (not just DevTools emulation)

**Rischi**
- No adoption = feature churn
- Layout toggle adds cognitive load if not discoverable

**DoD**
- [ ] At least 1 mobile surface mounts `MobileCardLayout`
- [ ] Swipe gesture works on iOS Safari + Android Chrome
- [ ] Remove `@status ORPHAN` from source file
- [ ] Remove `[ORPHAN]` from story
- [ ] Usability test with 2+ users

---

## Not Included

The following orphan does NOT get a ticket:

- **TagStrip** (`ui/tags/TagStrip.tsx`) — standalone variant is not wrong, it's just waiting for a non-MeepleCard card surface. No forcing function. Revisit when a feature page introduces custom card layouts (which is not on roadmap).

## Execution Order (Suggested)

1. **Ticket 1** (RatingStars) — smallest, lowest risk, warmup
2. **Ticket 5** (AgentStatsDisplay) — small, admin-scoped
3. **Ticket 2** (Tier Gate Kit) — medium, product-facing, closes Epic #4068
4. **Ticket 3** (ChatMessageList refactor) — large, high value
5. **Ticket 4** (PageTransition) — UX motion owner required
6. **Ticket 6** (MobileCardLayout) — product decision required
