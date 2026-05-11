# V2 Migration Component Matrix

> ✅ **FREEZE LIFTED 2026-05-09** (post P2 #807 token redesign — was issued 2026-05-06 via [#808](https://github.com/meepleAi-app/meepleai-monorepo/issues/808)):
> SP6 v2 expansion e nuovi v2 components ora **pickable**.
> CSS vars `--c-*` + `--e-*` AA-aligned (audit Iter 2: tutti 18 ratios ≥ 4.5:1 light + dark).
> Tailwind utilities `text-entity-*`, `bg-entity-event/10`, etc. generano colori AA-compliant.
> [#807](https://github.com/meepleAi-app/meepleai-monorepo/issues/807) e [#808](https://github.com/meepleAi-app/meepleai-monorepo/issues/808) entrambi closed via questa PR.
> Reference: `docs/for-developers/frontend/v2-token-system.md` + audit `v2-a11y-token-audit.md`.

> Wave A closeout — Step 5 (Issue #573).
> Pre-requisite for Phase 1+2 of the v2 design migration ([spec](../specs/2026-04-26-v2-design-migration.md), section 3.3).
> **Tier classification added 2026-05-04** post spec-panel critique Wave C.1 fail RCA — see [Tier classification](#tier-classification) section.

This matrix is the **single source of truth** for the ~80 v2 feature components that the
SP4 wave 1 + 2 + 3 + 4 mockups introduced and that do not yet exist in the codebase. Each
row binds a mockup definition to a target component path, route, and acceptance criteria
so that downstream PRs can pick up an entry and turn it from `pending` → `done` without
ambiguity. Each route is also classified by **Tier** (S/M/L) to gate dispatch strategy.

> **Updated 2026-04-30** (Wave B.2 spec-panel review): count refined 46 → 45.
> `AgentsSidebarList` + `AgentDetailPanel` removed from `/agents` row set (mockup
> `sp4-agents-index.jsx` is grid-pattern only, not master-detail). `AgentsResultsGrid`
> added (mirror B.1 `GamesResultsGrid` pattern, riusa `MeepleCard` con
> `entity="agent"`). `AgentsFiltersStrip` renamed `AgentFilters` to match mockup naming.

> **Updated 2026-05-04** (Wave 3 + Wave 4 partial mockups merged via PR #640): added
> 35 new components across 6 routes — Wave 3 (5 routes, 31 components: `/players/[id]`
> · `/toolkits/[id]` · `/kb/[id]` · `/game-nights` · `/discover`) and Wave 4 partial
> (1 route, 4 components: `/players`). Total grew 45 → 80. Stubs live under
> `apps/web/src/components/features/{player-detail,toolkit-detail,kb-detail,game-nights,discover,players}/`
> and `pnpm typecheck` stays green. Wave 4 remaining (E1 toolkits-index · F1 kb-index
> · G2 game-night-detail) and SP5 admin batch tracked separately — pending Claude Design
> production resumption (post 2026-05-10).

## Scope and ground rules

- **In scope**: 80 feature components extracted from `admin-mockups/design_files/sp4-*.jsx`
  wave 1 + 2 + 3 + 4 partial (16 mockups). Stubs live under
  `apps/web/src/components/features/<feature>/`.
- **Out of scope**: existing v2 primitives at `apps/web/src/components/ui/` (auth-card,
  btn, divider, drawer, entity-card, entity-chip, entity-pip, faq, hero-gradient,
  input-field, invites, join, notification-card, oauth-buttons, pricing-card, pwd-input,
  settings-list, settings-row, shared-game-detail, shared-games, step-progress,
  strength-meter, success-card). These are reused, not re-stubbed.
- **Path divergence is intentional** (per spec §3.3): primitives stay under
  `components/ui/`; *feature* compositions for SP4 routes live under
  `components/features/<feature>/`. Do not collapse the two trees.
- **Component count**: refined from the spec Appendix A (52 entries) to **45** by
  deferring six entries that are better served by v2 *primitives* once those exist
  (`PauseOverlay`, `EndgameDialog`, `ConnectionLostBanner` → v2 dialog/banner primitives;
  `ChatHighlights`, `SessionShareCard`, `PlayAgainCta` → composed inline from primitives)
  and by Wave B.2 spec-panel review (`AgentsSidebarList` + `AgentDetailPanel` removed,
  `AgentsResultsGrid` added — net −1). These are tracked in the
  [Deferred entries](#deferred-entries) section below.

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

## Tier classification

> Added 2026-05-04 post spec-panel critique Wave C.1 fail (PR #697 closed). See [v2 spec section 3.4](../specs/2026-04-26-v2-design-migration.md#34-phase-05--sub-hook-contract-per-tier-l-routes-only) for Phase 0.5 sub-hook contract gate.

Each route is classified by **Tier** (S/M/L) which gates implementation strategy:

| Tier | Hook count | FSM complexity | Strategy | Bundle budget per PR |
|------|-----------|----------------|----------|----------------------|
| **S** | 1 hook | 5-state lineare | Single-shot subagent dispatch (Wave B pattern) | < +50 KB |
| **M** | 2 hook indipendenti | 5-state per hook + composition | Single-shot subagent OK; review extra-careful FSM | < +80 KB |
| **L** | ≥3 hook indipendenti / cross-resource | Cartesian FSM (≥16 celle) | **Phase 0.5 sub-hook contract OBBLIGATORIA** + multi-iter subagent OR coexistence flag | < +120 KB |

### Route Tier mapping

| Route | Tier | Rationale | Status |
|-------|------|-----------|--------|
| `/games?tab=library` | **S** | useLibraryGames single hook, 5-state lineare | ✅ done (B.1, PR #635) |
| `/agents` | **S** | useAgentList single hook, grid pattern | ✅ done (B.2, PR #637) |
| `/library` | **S** | useLibrary single hook, hybrid grid | ✅ done (B.3, PR #638) |
| `/players` | **S** | usePlayerStatistics single hook, games-as-players grid (Wave 4 D1) — v1 carryover anti-pattern preserved | ✅ done (Wave 4 D1, PR #717) |
| `/games/[id]` | **L** | useGame + useAgents/Faqs/KbDocs by gameId — Phase 0.5 contract enforced | ✅ done (Wave C.1, PR #702) — `contracts/games-id-hooks.md` (TBD) |
| `/agents/[id]` | **L** | useAgent + chat history + KB docs cross-resource (2-step chain agent.gameId) | ✅ done (Wave C.2, PR #711) — `contracts/agents-id-hooks.md` (TBD) |
| `/sessions/[id]/live` | **L+** | Real-time SSE + multi-hook + dialog states | pending — Phase 0.5 + sub-PR split |
| `/discover` | **L** | Multiple horizontal-row hooks | pending — Phase 0.5 required |
| `/game-nights` | **L** | Calendar + day-detail drawer + filters | pending — Phase 0.5 required |
| `/sessions` | **M** | Sessions list + filters composition | pending |
| `/sessions/[id]` | **M-L** | Post-game summary: podium + KPI + diary + photos + share + tie-group computation | ✅ done (Wave D.3, PR #762) — `contracts/sessions-id-summary-hooks.md` (TBD) |
| `/players/[id]` | **M** | usePlayerStatistics single hook (current user only — schema reality v1 carryover) | ✅ done (Wave 3, PR #724) |
| `/toolkits/[id]` | **M** | Toolkit summary + version timeline | pending |
| `/gamebook` | **M** | Libro-game index: Hero + QuotaWidget + Card grid + EmptyState | ✅ done (SP6 Phase B, PR #792) |
| `/gamebook/upload` | **L** | 3-step wizard: game search + camera + indexing — 14-state FSM + camera permission matrix + offline retry | ✅ done (SP6 Phase C, contract PR #794 + Foundation PR #796 + Interactions PR #800) — [`contracts/gamebook-upload-hooks.md`](contracts/gamebook-upload-hooks.md) |
| `/library/[gameId]/play/[campaignId]/translate` | **S** | Nanolith demo — paragraph translate via chat-stream workaround. Route consolidated from `/library/games/[gameId]/translate` under campaign in IA refactor #871. | ✅ done (SP6 Phase A, PR #790; route refactored in #871) |
| `/kb/[id]` | **M** | KB header + chunks + search | **deferred** — pivot legale 2026-05-10, vedi `2026-05-10-citation-pdf-viewer-design.md` (G4 v3) |

**Anti-pattern**: dispatchare implementation subagent senza Phase 0.5 per route Tier L. Wave C.1 PR #697 ha esattamente questo come root cause (vedi [post-mortem](../specs/2026-04-26-v2-design-migration.md#34-phase-05--sub-hook-contract-per-tier-l-routes-only)).

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

## Wave 1 — 29 components

### Games index — `/games` — 6 components — **Tier S**

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-games-index.jsx` | `GamesHero` | `apps/web/src/components/features/games/GamesHero.tsx` | `/games` | pending | — | T A M V |
| `sp4-games-index.jsx` | `GamesFiltersInline` | `apps/web/src/components/features/games/GamesFiltersInline.tsx` | `/games` | pending | — | T A V |
| `sp4-games-index.jsx` | `AdvancedFiltersDrawer` | `apps/web/src/components/features/games/AdvancedFiltersDrawer.tsx` | `/games` | pending | — | T A M V |
| `sp4-games-index.jsx` | `GamesResultsGrid` | `apps/web/src/components/features/games/GamesResultsGrid.tsx` | `/games` | pending | — | T A V |
| `sp4-games-index.jsx` | `GamesEmptyState` | `apps/web/src/components/features/games/GamesEmptyState.tsx` | `/games` | pending | — | T A V |
| (extension G3) | `GamesRecentRail` | `apps/web/src/components/features/games/GamesRecentRail.tsx` | `/games?tab=*` | done | #907 | T A V |

### Game detail — `/games/[id]` — 8 components — **Tier L** ⚠️ Phase 0.5 required

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-game-detail.jsx` | `GameDetailHero` | `apps/web/src/components/features/game-detail/GameDetailHero.tsx` | `/games/[id]` | done | #702 | T A M V |
| `sp4-game-detail.jsx` | `GameDetailTabsAnimated` | `apps/web/src/components/features/game-detail/GameDetailTabsAnimated.tsx` | `/games/[id]` | done | #702 | T A M V |
| `sp4-game-detail.jsx` | `GameDetailKpiCards` | `apps/web/src/components/features/game-detail/GameDetailKpiCards.tsx` | `/games/[id]` | done | #702 | T A V |
| `sp4-game-detail.jsx` | `GameDetailFaqList` | `apps/web/src/components/features/game-detail/GameDetailFaqList.tsx` | `/games/[id]` | done | #702 | T A V |
| `sp4-game-detail.jsx` | `GameDetailRulesAccordion` | `apps/web/src/components/features/game-detail/GameDetailRulesAccordion.tsx` | `/games/[id]` | done | #702 | T A M V |
| `sp4-game-detail.jsx` | `GameDetailSessionsRail` | `apps/web/src/components/features/game-detail/GameDetailSessionsRail.tsx` | `/games/[id]` | done | #702 | T A V |
| `sp4-game-detail.jsx` | `GameDetailAgentsList` | `apps/web/src/components/features/game-detail/GameDetailAgentsList.tsx` | `/games/[id]` | done | #702 | T A V |
| `sp4-game-detail.jsx` | `GameDetailKbDocList` | `apps/web/src/components/features/game-detail/GameDetailKbDocList.tsx` | `/games/[id]` | done | #702 | T A V |
| `sp4-game-chat-tab.html` (G1+G5) | `GameChatTabV2 + 11 game-chat components` | `apps/web/src/components/features/game-chat/` | `/library/games/[id]?tab=aiChat` | done | #918 | T A V |

### Agents index — `/agents` — 4 components — **Tier S**

> **Updated 2026-04-30** (Wave B.2 spec-panel review): mockup `sp4-agents-index.jsx`
> implementa pattern grid (3-col 1280 desktop), NON master-detail. `AgentsSidebarList`
> + `AgentDetailPanel` rimossi da scope `/agents` (stub files restano on disk come
> placeholder senza consumer — eventual cleanup follow-up post-Wave B). Ratio dietro
> rimozione: nessun mockup SP4 wave 1+2 referenzia un layout master-detail per
> `/agents`; quei componenti erano artefatti di una pianificazione iniziale che il
> design finale non ha confermato. `AgentsFiltersStrip` rinominato `AgentFilters`
> (allineamento mockup naming). `AgentsResultsGrid` aggiunto (mirror B.1
> `GamesResultsGrid`: CSS Grid 3-col `auto-fit minmax(320px, 1fr)`, riusa `MeepleCard`
> con `entity="agent"` `variant="grid"` — NO fork).

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-agents-index.jsx` | `AgentsHero` | `apps/web/src/components/features/agents/AgentsHero.tsx` | `/agents` | pending | — | T A M V |
| `sp4-agents-index.jsx` | `AgentFilters` | `apps/web/src/components/features/agents/AgentFilters.tsx` | `/agents` | pending | — | T A V |
| `sp4-agents-index.jsx` | `AgentsResultsGrid` | `apps/web/src/components/features/agents/AgentsResultsGrid.tsx` | `/agents` | pending | — | T A V |
| `sp4-agents-index.jsx` | `EmptyAgents` | `apps/web/src/components/features/agents/EmptyAgents.tsx` | `/agents` | pending | — | T A V |

### Agent detail — `/agents/[id]` — 7 components — **Tier L** ⚠️ Phase 0.5 required

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-agent-detail.jsx` | `AgentHero` *(was AgentCharacterSheet stub — renamed per mockup)* | `apps/web/src/components/features/agent-detail/AgentHero.tsx` | `/agents/[id]` | done | #711 | T A M V |
| `sp4-agent-detail.jsx` | `PersonaCard` | `apps/web/src/components/features/agent-detail/PersonaCard.tsx` | `/agents/[id]` | done | #711 | T A V |
| `sp4-agent-detail.jsx` | `SystemPromptViewer` | `apps/web/src/components/features/agent-detail/SystemPromptViewer.tsx` | `/agents/[id]` | done | #711 | T A V |
| `sp4-agent-detail.jsx` | `KbDocList` | `apps/web/src/components/features/agent-detail/KbDocList.tsx` | `/agents/[id]` | done | #711 | T A V |
| `sp4-agent-detail.jsx` | `ChatHistoryTimeline` | `apps/web/src/components/features/agent-detail/ChatHistoryTimeline.tsx` | `/agents/[id]` | done | #711 | T A M V |
| `sp4-agent-detail.jsx` | `AgentSettingsForm` | `apps/web/src/components/features/agent-detail/AgentSettingsForm.tsx` | `/agents/[id]` | done | #711 | T A V |
| `sp4-agent-detail.jsx` | `AgentDangerZone` | `apps/web/src/components/features/agent-detail/AgentDangerZone.tsx` | `/agents/[id]` | done | #711 | T A V |

### Library — `/library` — 5 components — **Tier S**

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-library-desktop.jsx` | `LibraryHeroDesktop` | `apps/web/src/components/features/library/LibraryHeroDesktop.tsx` | `/library` | done | #574 | T A M V |
| `sp4-library-desktop.jsx` | `LibraryTabs` | `apps/web/src/components/features/library/LibraryTabs.tsx` | `/library` | done | #574 | T A M V |
| `sp4-library-desktop.jsx` | `LibraryHybridGrid` | `apps/web/src/components/features/library/LibraryHybridGrid.tsx` | `/library` | done | #574 | T A V |
| `sp4-library-desktop.jsx` | `BulkSelectionBar` | `apps/web/src/components/features/library/BulkSelectionBar.tsx` | `/library` | done | #574 | T A M V |
| `sp4-library-desktop.jsx` | `RecentActivityRail` | `apps/web/src/components/features/library/RecentActivityRail.tsx` | `/library` | done | #574 | T A V |

## Wave 2 — 16 components

### Sessions index — `/sessions` — 8 components — **Tier S** (per spec-panel review PR #734)

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-sessions-index.jsx` | `SessionsHero` | `apps/web/src/components/features/sessions/SessionsHero.tsx` | `/sessions` | done | TBD | T A M V |
| `sp4-sessions-index.jsx` | `SessionsFilters` | `apps/web/src/components/features/sessions/SessionsFilters.tsx` | `/sessions` | done | TBD | T A V |
| `sp4-sessions-index.jsx` | `SessionCardList` | `apps/web/src/components/features/sessions/SessionCardList.tsx` | `/sessions` | done | TBD | T A V |
| `sp4-sessions-index.jsx` | `SessionCardGrid` | `apps/web/src/components/features/sessions/SessionCardGrid.tsx` | `/sessions` | done | TBD | T A V |
| `sp4-sessions-index.jsx` | `EmptySessions` | `apps/web/src/components/features/sessions/EmptySessions.tsx` | `/sessions` | done | TBD | T A V |
| `sp4-sessions-index.jsx` | `OutcomeBadge` | `apps/web/src/components/features/sessions/OutcomeBadge.tsx` | `/sessions` | done | TBD | T A V |
| `sp4-sessions-index.jsx` | `ScoringInline` | `apps/web/src/components/features/sessions/ScoringInline.tsx` | `/sessions` | done | TBD | T A V |
| `sp4-sessions-index.jsx` | `ConnectionChipStripFooter` | `apps/web/src/components/features/sessions/ConnectionChipStripFooter.tsx` | `/sessions` | done | TBD | T A V |

### Session live — `/sessions/[id]/live` — Tier L+ ⚠️ Phase 0.5 + sub-PR split

**Sub-PR split**: Foundation (read-only static fixture) → Interactions (real SSE + write actions + dialogs)

#### Foundation sub-PR (7 read-only components — IN REVIEW PR TBD)

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-session-live-parts.jsx` | `LiveTopBar` | `apps/web/src/components/features/session-live/LiveTopBar.tsx` | `/sessions/[id]/live` | done | TBD | T A V |
| `sp4-session-live-parts.jsx` | `TurnIndicator` | `apps/web/src/components/features/session-live/TurnIndicator.tsx` | `/sessions/[id]/live` | done | TBD | T A M V |
| `sp4-session-live-parts.jsx` | `PlayerRosterLive` | `apps/web/src/components/features/session-live/PlayerRosterLive.tsx` | `/sessions/[id]/live` | done | TBD | T A V |
| `sp4-session-live-parts.jsx` | `LiveScoringPanel` | `apps/web/src/components/features/session-live/LiveScoringPanel.tsx` | `/sessions/[id]/live` | done | TBD | T A V |
| `sp4-session-live-parts.jsx` | `ActionLogTimeline` | `apps/web/src/components/features/session-live/ActionLogTimeline.tsx` | `/sessions/[id]/live` | done | TBD | T A V |
| `sp4-session-live-parts.jsx` | `DesktopBody` | `apps/web/src/components/features/session-live/DesktopBody.tsx` | `/sessions/[id]/live` | done | TBD | T A V |
| `sp4-session-live-parts.jsx` | `MobileBody` | `apps/web/src/components/features/session-live/MobileBody.tsx` | `/sessions/[id]/live` | done | TBD | T A V |

#### Interactions sub-PR (6 interactive + 2 lazy dialogs — PR #750)

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-session-live.jsx` | `SessionToolsRail` | `apps/web/src/components/features/session-live/SessionToolsRail.tsx` | `/sessions/[id]/live` | done | #750 | T A V |
| `sp4-session-live.jsx` | `LiveAgentChat` | `apps/web/src/components/features/session-live/LiveAgentChat.tsx` | `/sessions/[id]/live` | done | #750 | T A V |
| `sp4-session-live.jsx` | `LiveSessionNotes` | `apps/web/src/components/features/session-live/LiveSessionNotes.tsx` | `/sessions/[id]/live` | done | #750 | T A V |
| `sp4-session-live.jsx` | `RightColumnTabs` | `apps/web/src/components/features/session-live/RightColumnTabs.tsx` | `/sessions/[id]/live` | done | #750 | T A V |
| `sp4-session-live.jsx` | `ConnectionLostBanner` | `apps/web/src/components/features/session-live/ConnectionLostBanner.tsx` | `/sessions/[id]/live` | done | #750 | T A V |
| `sp4-session-live.jsx` | `PauseOverlay` (lazy) | `apps/web/src/components/features/session-live/PauseOverlay.tsx` | `/sessions/[id]/live` | done | #750 | T A V dialog |
| `sp4-session-live.jsx` | `EndgameDialog` (lazy) | `apps/web/src/components/features/session-live/EndgameDialog.tsx` | `/sessions/[id]/live` | done | #750 | T A V dialog |

### Session summary — `/sessions/[id]` — 11 components — **Tier M-L**

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-session-summary-parts.jsx` | `SessionSummaryHero` | `apps/web/src/components/features/session-summary/SessionSummaryHero.tsx` | `/sessions/[id]` | done | #762 | T A M V |
| `sp4-session-summary-parts.jsx` | `SessionKpiGrid` | `apps/web/src/components/features/session-summary/SessionKpiGrid.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary-parts.jsx` | `ScoringBreakdownTable` | `apps/web/src/components/features/session-summary/ScoringBreakdownTable.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary-parts.jsx` | `ConnectionBar` | `apps/web/src/components/features/session-summary/ConnectionBar.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary-parts.jsx` | `AchievementsCarousel` | `apps/web/src/components/features/session-summary/AchievementsCarousel.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary.jsx` | `SessionDiaryTimeline` | `apps/web/src/components/features/session-summary/SessionDiaryTimeline.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary.jsx` | `PhotosGallery` | `apps/web/src/components/features/session-summary/PhotosGallery.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary.jsx` | `ChatHighlights` | `apps/web/src/components/features/session-summary/ChatHighlights.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary.jsx` | `SessionShareCard` | `apps/web/src/components/features/session-summary/SessionShareCard.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary.jsx` | `PlayAgainCta` | `apps/web/src/components/features/session-summary/PlayAgainCta.tsx` | `/sessions/[id]` | done | #762 | T A V |
| `sp4-session-summary.jsx` | `Confetti` | `apps/web/src/components/features/session-summary/Confetti.tsx` | `/sessions/[id]` | done | #762 | T A V |

## Wave 3 — 31 components

> **Mockup PR**: #640 (merged 2026-05-03). Stubs added in same PR series.
> Mirror Wave B.1/B.2/B.3 implementation pattern (5-commit TDD: foundation → components → orchestrator → E2E → cleanup).

### Player detail — `/players/[id]` — 5 components — **Tier M**

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-player-detail.jsx` | `PlayerHero` | `apps/web/src/components/features/player-detail/PlayerHero.tsx` | `/players/[id]` | pending | — | T A M V |
| `sp4-player-detail.jsx` | `PlayerStatsGrid` | `apps/web/src/components/features/player-detail/PlayerStatsGrid.tsx` | `/players/[id]` | pending | — | T A V |
| `sp4-player-detail.jsx` | `PlayerLeaderboardCard` | `apps/web/src/components/features/player-detail/PlayerLeaderboardCard.tsx` | `/players/[id]` | pending | — | T A V |
| `sp4-player-detail.jsx` | `FavoriteAgentCard` | `apps/web/src/components/features/player-detail/FavoriteAgentCard.tsx` | `/players/[id]` | pending | — | T A V |
| `sp4-player-detail.jsx` | `AchievementBadgeGrid` | `apps/web/src/components/features/player-detail/AchievementBadgeGrid.tsx` | `/players/[id]` | pending | — | T A V |

### Toolkit detail — `/toolkits/[id]` — 6 components — **Tier M**

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-toolkit-detail.jsx` | `ToolkitSummaryPanel` | `apps/web/src/components/features/toolkit-detail/ToolkitSummaryPanel.tsx` | `/toolkits/[id]` | pending | — | T A V |
| `sp4-toolkit-detail.jsx` | `ToolkitIncludesGrid` | `apps/web/src/components/features/toolkit-detail/ToolkitIncludesGrid.tsx` | `/toolkits/[id]` | pending | — | T A V |
| `sp4-toolkit-detail.jsx` | `VersionTimeline` | `apps/web/src/components/features/toolkit-detail/VersionTimeline.tsx` | `/toolkits/[id]` | pending | — | T A V |
| `sp4-toolkit-detail.jsx` | `RatingBreakdown` | `apps/web/src/components/features/toolkit-detail/RatingBreakdown.tsx` | `/toolkits/[id]` | pending | — | T A V |
| `sp4-toolkit-detail.jsx` | `PromptPreviewBlock` | `apps/web/src/components/features/toolkit-detail/PromptPreviewBlock.tsx` | `/toolkits/[id]` | pending | — | T A V |
| `sp4-toolkit-detail.jsx` | `Stars` | `apps/web/src/components/features/toolkit-detail/Stars.tsx` | `/toolkits/[id]` | pending | — | T A V |

### KB detail — `/kb/[id]` — 6 components — **Tier M**

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-kb-detail.jsx` | `KbHeader` | `apps/web/src/components/features/kb-detail/KbHeader.tsx` | `/kb/[id]` | deferred (G4 v3) | — | T A V |
| `sp4-kb-detail.jsx` | `KbChunkListPanel` | `apps/web/src/components/features/kb-detail/KbChunkListPanel.tsx` | `/kb/[id]` | deferred (G4 v3) | — | T A V |
| `sp4-kb-detail.jsx` | `KbChunkPreview` | `apps/web/src/components/features/kb-detail/KbChunkPreview.tsx` | `/kb/[id]` | deferred (G4 v3) | — | T A V |
| `sp4-kb-detail.jsx` | `ChunkSearchBox` | `apps/web/src/components/features/kb-detail/ChunkSearchBox.tsx` | `/kb/[id]` | deferred (G4 v3) | — | T A V |
| `sp4-kb-detail.jsx` | `MarkdownRenderBlock` | `apps/web/src/components/features/kb-detail/MarkdownRenderBlock.tsx` | `/kb/[id]` | deferred (G4 v3) | — | T A V |
| `sp4-kb-detail.jsx` | `KbProcessingState` | `apps/web/src/components/features/kb-detail/KbProcessingState.tsx` | `/kb/[id]` | deferred (G4 v3) | — | T A V |

### Game nights index — `/game-nights` — 8 components — **Tier L** ⚠️ Phase 0.5 required

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-game-nights-index.jsx` | `GameNightsHeader` | `apps/web/src/components/features/game-nights/GameNightsHeader.tsx` | `/game-nights` | pending | — | T A V |
| `sp4-game-nights-index.jsx` | `CalendarMonthGrid` | `apps/web/src/components/features/game-nights/CalendarMonthGrid.tsx` | `/game-nights` | pending | — | T A V |
| `sp4-game-nights-index.jsx` | `CalendarDayCell` | `apps/web/src/components/features/game-nights/CalendarDayCell.tsx` | `/game-nights` | pending | — | T A V |
| `sp4-game-nights-index.jsx` | `GameNightListCard` | `apps/web/src/components/features/game-nights/GameNightListCard.tsx` | `/game-nights` | pending | — | T A V |
| `sp4-game-nights-index.jsx` | `DayDetailDrawer` | `apps/web/src/components/features/game-nights/DayDetailDrawer.tsx` | `/game-nights` | pending | — | T A M V |
| `sp4-game-nights-index.jsx` | `FilterPillBar` | `apps/web/src/components/features/game-nights/FilterPillBar.tsx` | `/game-nights` | pending | — | T A V |
| `sp4-game-nights-index.jsx` | `StatusPill` | `apps/web/src/components/features/game-nights/StatusPill.tsx` | `/game-nights` | pending | — | T A V |
| `sp4-game-nights-index.jsx` | `PlayerAvatars` | `apps/web/src/components/features/game-nights/PlayerAvatars.tsx` | `/game-nights` | pending | — | T A V |

### Discover — `/discover` — 6 components — **Tier L** ⚠️ Phase 0.5 required

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-discover.jsx` | `DiscoverHero` | `apps/web/src/components/features/discover/DiscoverHero.tsx` | `/discover` | pending | — | T A M V |
| `sp4-discover.jsx` | `DiscoverSearchBox` | `apps/web/src/components/features/discover/DiscoverSearchBox.tsx` | `/discover` | pending | — | T A V |
| `sp4-discover.jsx` | `EntityFilterPillBar` | `apps/web/src/components/features/discover/EntityFilterPillBar.tsx` | `/discover` | pending | — | T A V |
| `sp4-discover.jsx` | `HorizontalRow` | `apps/web/src/components/features/discover/HorizontalRow.tsx` | `/discover` | pending | — | T A M V |
| `sp4-discover.jsx` | `RowScroller` | `apps/web/src/components/features/discover/RowScroller.tsx` | `/discover` | pending | — | T A V |
| `sp4-discover.jsx` | `FooterCTA` | `apps/web/src/components/features/discover/FooterCTA.tsx` | `/discover` | pending | — | T A V |

## Wave 4 — 4 components (partial — 1/4 routes)

> **Status**: D1 players-index landed via PR #640 mockup batch (2026-05-03).
> Remaining routes (E1 toolkits-index, F1 kb-index, G2 game-night-detail) blocked
> until Claude Design production resumes (post 2026-05-10).

### Players index — `/players` — 4 components — **Tier S**

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `sp4-players-index.jsx` | `PlayersHero` | `apps/web/src/components/features/players/PlayersHero.tsx` | `/players` | done | #717 | T A M V |
| `sp4-players-index.jsx` | `PlayersFiltersInline` | `apps/web/src/components/features/players/PlayersFiltersInline.tsx` | `/players` | done | #717 | T A V |
| `sp4-players-index.jsx` | `PlayersResultsGrid` | `apps/web/src/components/features/players/PlayersResultsGrid.tsx` | `/players` | done | #717 | T A V |
| `sp4-players-index.jsx` | `EmptyPlayers` | `apps/web/src/components/features/players/EmptyPlayers.tsx` | `/players` | done | #717 | T A V |

## SP6 — Nanolith libro-game (Iter 1.B / Iter 4) — 2 components

> Surfaces the Nanolith runthrough dogfood flow on the canonical
> `/library/[gameId]` page when the game qualifies as a "libro game"
> (helper at `apps/web/src/lib/games/is-libro-game.ts`). Mockups from the
> Aaron Iter 1 / Iter 4 storyboard batch (2026-05-07+). Route lives under
> `/library/[gameId]` after IA consolidation in #871 (was previously planned
> under `/library/games/[gameId]`).

### Libro-game detail surface — `/library/[gameId]` (libro variant) — 2 components — **Tier M**

| Mockup | Component | Path | Route | Status | PR | AC |
|--------|-----------|------|-------|--------|----|----|
| `nanolith-runthrough-game-detail.html` | `LibroGameDetailView` | `apps/web/src/components/v2/gamebook/LibroGameDetailView.tsx` | `/library/[gameId]` (libro variant) | done | #1037 | T A V |
| `nanolith-runthrough-setup-wizard.html` | `CampaignSetupDrawer` | `apps/web/src/components/v2/gamebook/CampaignSetupDrawer.tsx` | `/library/[gameId]` (libro variant — drawer) | done | #1037 | T A V |

## Stub format (informational)

Each stub follows this minimal contract so `pnpm typecheck` stays green and downstream
PRs can replace bodies without touching imports:

```tsx
// TODO: implement per admin-mockups/design_files/sp4-<mockup>.jsx
// Mapped from mockup component: <MockupName>
// Spec: docs/for-developers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)

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
`components/features/`; they will be implemented as compositions of primitives once the
relevant primitive lands under `components/ui/`:

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
- v2 design migration spec: [`docs/for-developers/specs/2026-04-26-v2-design-migration.md`](../specs/2026-04-26-v2-design-migration.md).
- Existing v2 primitives index: [`apps/web/src/components/ui/`](../../../apps/web/src/components/ui/).
- Mockups: [`admin-mockups/design_files/`](../../../admin-mockups/design_files/) (sp4-* wave 1+2).
