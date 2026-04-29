# V2 Migration Component Matrix

> Wave A closeout — Step 5 (Issue #573).
> Pre-requisite for Phase 1+2 of the v2 design migration ([spec](../superpowers/specs/2026-04-26-v2-design-migration.md), section 3.3).

This matrix is the **single source of truth** for the ~46 v2 feature components that the
SP4 wave 1 + 2 mockups introduced and that do not yet exist in the codebase. Each row
binds a mockup definition to a target component path, route, and acceptance criteria so
that downstream PRs can pick up an entry and turn it from `pending` → `done` without
ambiguity.

## Scope and ground rules

- **In scope**: 46 feature components extracted from `admin-mockups/design_files/sp4-*.jsx`
  wave 1 + 2 (10 mockups). Stubs live under `apps/web/src/components/v2/<feature>/`.
- **Out of scope**: existing v2 primitives at `apps/web/src/components/ui/v2/` (auth-card,
  btn, divider, drawer, entity-card, entity-chip, entity-pip, faq, hero-gradient,
  input-field, invites, join, notification-card, oauth-buttons, pricing-card, pwd-input,
  settings-list, settings-row, shared-game-detail, shared-games, step-progress,
  strength-meter, success-card). These are reused, not re-stubbed.
- **Path divergence is intentional** (per spec §3.3): primitives stay under
  `components/ui/v2/`; *feature* compositions for SP4 routes live under
  `components/v2/<feature>/`. Do not collapse the two trees.
- **Component count**: refined from the spec Appendix A (52 entries) to **46** by
  deferring six entries that are better served by v2 *primitives* once those exist
  (`PauseOverlay`, `EndgameDialog`, `ConnectionLostBanner` → v2 dialog/banner primitives;
  `ChatHighlights`, `SessionShareCard`, `PlayAgainCta` → composed inline from primitives).
  These are tracked in the [Deferred entries](#deferred-entries) section below.

## How to use

1. Pick a row with `Status = pending`.
2. Read the acceptance criteria column for what counts as "done" for that component.
3. Implement the stub at the listed path; keep the path stable.
4. Open a PR; on merge, update `Status = done` and `PR ref = #N` in this file via the same PR.
5. The matrix moves with the codebase: never edit it from a side branch unless you are
   landing the implementation in the same PR.

## Status legend

| Symbol | Meaning |
|--------|---------|
| `pending` | Stub exists, no implementation yet |
| `in-progress` | A PR is open against the component |
| `done` | Implementation merged; PR linked |

## Acceptance criteria abbreviations

Used in the **AC** column to keep the table compact.

| Abbrev | Meaning |
|--------|---------|
| `T` | **Token compliance** — uses design tokens from `tailwind.config` / CSS vars; no hard-coded colors / spacing. |
| `A` | **a11y role/aria** — correct `role`, `aria-*`, focus order; passes axe. WCAG 2.1 AA target. |
| `M` | **prefers-reduced-motion** — animations gated; provides static fallback. |
| `V` | **Viewport coverage** — renders correctly across `375px` (mobile), `768px` (tablet), `1280px` (desktop). |

A "done" component must satisfy all four (`T`, `A`, `M`, `V`) unless explicitly waived in
the PR review.

## Wave 1 — 30 components

### Games index — `/games` — 5 components

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-games-index.jsx` | `GamesHero` | `apps/web/src/components/v2/games/GamesHero.tsx` | `/games` | pending | — | T A M V |
| `sp4-games-index.jsx` | `GamesFiltersInline` | `apps/web/src/components/v2/games/GamesFiltersInline.tsx` | `/games` | pending | — | T A V |
| `sp4-games-index.jsx` | `AdvancedFiltersDrawer` | `apps/web/src/components/v2/games/AdvancedFiltersDrawer.tsx` | `/games` | pending | — | T A M V |
| `sp4-games-index.jsx` | `GamesResultsGrid` | `apps/web/src/components/v2/games/GamesResultsGrid.tsx` | `/games` | pending | — | T A V |
| `sp4-games-index.jsx` | `GamesEmptyState` | `apps/web/src/components/v2/games/GamesEmptyState.tsx` | `/games` | pending | — | T A V |

### Game detail — `/games/[id]` — 8 components

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-game-detail.jsx` | `GameDetailHero` | `apps/web/src/components/v2/game-detail/GameDetailHero.tsx` | `/games/[id]` | pending | — | T A M V |
| `sp4-game-detail.jsx` | `GameDetailTabsAnimated` | `apps/web/src/components/v2/game-detail/GameDetailTabsAnimated.tsx` | `/games/[id]` | pending | — | T A M V |
| `sp4-game-detail.jsx` | `GameDetailKpiCards` | `apps/web/src/components/v2/game-detail/GameDetailKpiCards.tsx` | `/games/[id]` | pending | — | T A V |
| `sp4-game-detail.jsx` | `GameDetailFaqList` | `apps/web/src/components/v2/game-detail/GameDetailFaqList.tsx` | `/games/[id]` | pending | — | T A V |
| `sp4-game-detail.jsx` | `GameDetailRulesAccordion` | `apps/web/src/components/v2/game-detail/GameDetailRulesAccordion.tsx` | `/games/[id]` | pending | — | T A M V |
| `sp4-game-detail.jsx` | `GameDetailSessionsRail` | `apps/web/src/components/v2/game-detail/GameDetailSessionsRail.tsx` | `/games/[id]` | pending | — | T A V |
| `sp4-game-detail.jsx` | `GameDetailAgentsList` | `apps/web/src/components/v2/game-detail/GameDetailAgentsList.tsx` | `/games/[id]` | pending | — | T A V |
| `sp4-game-detail.jsx` | `GameDetailKbDocList` | `apps/web/src/components/v2/game-detail/GameDetailKbDocList.tsx` | `/games/[id]` | pending | — | T A V |

### Agents index — `/agents` — 5 components

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-agents-index.jsx` | `AgentsHero` | `apps/web/src/components/v2/agents/AgentsHero.tsx` | `/agents` | pending | — | T A M V |
| `sp4-agents-index.jsx` | `AgentsSidebarList` | `apps/web/src/components/v2/agents/AgentsSidebarList.tsx` | `/agents` | pending | — | T A V |
| `sp4-agents-index.jsx` | `AgentDetailPanel` | `apps/web/src/components/v2/agents/AgentDetailPanel.tsx` | `/agents` | pending | — | T A V |
| `sp4-agents-index.jsx` | `AgentsFiltersStrip` | `apps/web/src/components/v2/agents/AgentsFiltersStrip.tsx` | `/agents` | pending | — | T A V |
| `sp4-agents-index.jsx` | `EmptyAgents` | `apps/web/src/components/v2/agents/EmptyAgents.tsx` | `/agents` | pending | — | T A V |

### Agent detail — `/agents/[id]` — 7 components

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-agent-detail.jsx` | `AgentCharacterSheet` | `apps/web/src/components/v2/agent-detail/AgentCharacterSheet.tsx` | `/agents/[id]` | pending | — | T A V |
| `sp4-agent-detail.jsx` | `PersonaCard` | `apps/web/src/components/v2/agent-detail/PersonaCard.tsx` | `/agents/[id]` | pending | — | T A V |
| `sp4-agent-detail.jsx` | `SystemPromptViewer` | `apps/web/src/components/v2/agent-detail/SystemPromptViewer.tsx` | `/agents/[id]` | pending | — | T A V |
| `sp4-agent-detail.jsx` | `KbDocList` | `apps/web/src/components/v2/agent-detail/KbDocList.tsx` | `/agents/[id]` | pending | — | T A V |
| `sp4-agent-detail.jsx` | `ChatHistoryTimeline` | `apps/web/src/components/v2/agent-detail/ChatHistoryTimeline.tsx` | `/agents/[id]` | pending | — | T A M V |
| `sp4-agent-detail.jsx` | `AgentSettingsForm` | `apps/web/src/components/v2/agent-detail/AgentSettingsForm.tsx` | `/agents/[id]` | pending | — | T A V |
| `sp4-agent-detail.jsx` | `AgentDangerZone` | `apps/web/src/components/v2/agent-detail/AgentDangerZone.tsx` | `/agents/[id]` | pending | — | T A V |

### Library — `/library` — 5 components

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-library-desktop.jsx` | `LibraryHeroDesktop` | `apps/web/src/components/v2/library/LibraryHeroDesktop.tsx` | `/library` | pending | — | T A M V |
| `sp4-library-desktop.jsx` | `LibraryTabs` | `apps/web/src/components/v2/library/LibraryTabs.tsx` | `/library` | pending | — | T A M V |
| `sp4-library-desktop.jsx` | `LibraryHybridGrid` | `apps/web/src/components/v2/library/LibraryHybridGrid.tsx` | `/library` | pending | — | T A V |
| `sp4-library-desktop.jsx` | `BulkSelectionBar` | `apps/web/src/components/v2/library/BulkSelectionBar.tsx` | `/library` | pending | — | T A M V |
| `sp4-library-desktop.jsx` | `RecentActivityRail` | `apps/web/src/components/v2/library/RecentActivityRail.tsx` | `/library` | pending | — | T A V |

## Wave 2 — 16 components

### Sessions index — `/sessions` — 3 components

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-sessions-index.jsx` | `SessionsHero` | `apps/web/src/components/v2/sessions/SessionsHero.tsx` | `/sessions` | pending | — | T A M V |
| `sp4-sessions-index.jsx` | `SessionsFilters` | `apps/web/src/components/v2/sessions/SessionsFilters.tsx` | `/sessions` | pending | — | T A V |
| `sp4-sessions-index.jsx` | `ConnectionChipStripFooter` | `apps/web/src/components/v2/sessions/ConnectionChipStripFooter.tsx` | `/sessions` | pending | — | T A V |

### Session live — `/sessions/[id]` — 7 components

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-session-live-parts.jsx` | `LiveTopBar` | `apps/web/src/components/v2/session-live/LiveTopBar.tsx` | `/sessions/[id]` | pending | — | T A V |
| `sp4-session-live-parts.jsx` | `TurnIndicator` | `apps/web/src/components/v2/session-live/TurnIndicator.tsx` | `/sessions/[id]` | pending | — | T A M V |
| `sp4-session-live-parts.jsx` | `PlayerRosterLive` | `apps/web/src/components/v2/session-live/PlayerRosterLive.tsx` | `/sessions/[id]` | pending | — | T A V |
| `sp4-session-live-parts.jsx` | `LiveScoringPanel` | `apps/web/src/components/v2/session-live/LiveScoringPanel.tsx` | `/sessions/[id]` | pending | — | T A V |
| `sp4-session-live-parts.jsx` | `ActionLogTimeline` | `apps/web/src/components/v2/session-live/ActionLogTimeline.tsx` | `/sessions/[id]` | pending | — | T A V |
| `sp4-session-live.jsx` | `SessionToolsRail` | `apps/web/src/components/v2/session-live/SessionToolsRail.tsx` | `/sessions/[id]` | pending | — | T A V |
| `sp4-session-live.jsx` | `LiveAgentChat` | `apps/web/src/components/v2/session-live/LiveAgentChat.tsx` | `/sessions/[id]` | pending | — | T A V |

### Session summary — `/sessions/[id]/summary` — 6 components

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-session-summary-parts.jsx` | `SessionSummaryHero` | `apps/web/src/components/v2/session-summary/SessionSummaryHero.tsx` | `/sessions/[id]/summary` | pending | — | T A M V |
| `sp4-session-summary-parts.jsx` | `SessionKpiGrid` | `apps/web/src/components/v2/session-summary/SessionKpiGrid.tsx` | `/sessions/[id]/summary` | pending | — | T A V |
| `sp4-session-summary-parts.jsx` | `ScoringBreakdownTable` | `apps/web/src/components/v2/session-summary/ScoringBreakdownTable.tsx` | `/sessions/[id]/summary` | pending | — | T A V |
| `sp4-session-summary.jsx` | `SessionDiaryTimeline` | `apps/web/src/components/v2/session-summary/SessionDiaryTimeline.tsx` | `/sessions/[id]/summary` | pending | — | T A V |
| `sp4-session-summary.jsx` | `PhotosGallery` | `apps/web/src/components/v2/session-summary/PhotosGallery.tsx` | `/sessions/[id]/summary` | pending | — | T A V |
| `sp4-session-summary.jsx` | `SessionShareCard` | `apps/web/src/components/v2/session-summary/SessionShareCard.tsx` | `/sessions/[id]/summary` | pending | — | T A V |

## Stub format (informational)

Each stub follows this minimal contract so `pnpm typecheck` stays green and downstream
PRs can replace bodies without touching imports:

```tsx
// TODO: implement per admin-mockups/design_files/sp4-<mockup>.jsx
// Mapped from mockup component: <MockupName>
// Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)

import type { ReactElement } from 'react';

export interface <Component>Props {
  // TODO: extract props contract from mockup analysis
}

export function <Component>(_props: <Component>Props): ReactElement | null {
  return null;
}
```

## Deferred entries

These six components from the spec Appendix A are intentionally **not stubbed** under
`components/v2/`; they will be implemented as compositions of primitives once the
relevant primitive lands under `components/ui/v2/`:

| Spec name | Reason | Replacement strategy |
|-----------|--------|----------------------|
| `PauseOverlay` | Modal-shaped, no feature-specific state | v2 dialog primitive + content slot |
| `EndgameDialog` | Modal-shaped, deterministic content | v2 dialog primitive + content slot |
| `ConnectionLostBanner` | Pure status banner | v2 banner / toast primitive |
| `ChatHighlights` | Inline section under summary | inline composition with v2 list primitive |
| `SessionShareCard` (was deferred → restored) | The card *is* feature-specific (game state, score, image export); kept as feature stub | — |
| `PlayAgainCta` | Single CTA button | v2 button primitive |
| `GamesSortBar` | Small UI sliver, often inlined with filters | inline composition with v2 button + dropdown primitives |

Note: `SessionShareCard` was originally on the deferred list during refinement but moved
back to the matrix because the card composes feature-specific data (final score, photo,
session title) into a single artifact suitable for image export — that is feature-level
work, not a primitive composition. The total stays at 46 by dropping `GamesSortBar`
instead.

## References

- Issue #573 — *[V2 Phase 0] Migration contract matrix + 46 component stub*.
- Wave A umbrella #579.
- v2 design migration spec: [`docs/superpowers/specs/2026-04-26-v2-design-migration.md`](../superpowers/specs/2026-04-26-v2-design-migration.md).
- Existing v2 primitives index: [`apps/web/src/components/ui/v2/`](../../apps/web/src/components/ui/v2/).
- Mockups: [`admin-mockups/design_files/`](../../admin-mockups/design_files/) (sp4-* wave 1+2).
