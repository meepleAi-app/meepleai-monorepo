# Architectural Audit: `/components/toolkit/` vs `/components/features/session-live/`

**Date**: 2026-06-16
**Branch**: `main-dev`
**Scope**: Epic #2354 G5 (ScoringPanelRenderer #2373, TurnIndicatorRenderer #2378, ToolkitRenderer #2376)
**Auditor**: Claude Code

---

## 1. Executive Summary

- **Two `TurnIndicatorRenderer` components exist simultaneously.** `toolkit/TurnIndicatorRenderer` (issue #1749, shipped PR #1763) serves the **toolkit configuration preview** context (`AiTurnTemplateSuggestion` props, AI-generated template display). `features/session-live/turn-indicator-renderer/TurnIndicatorRenderer` (issue #2378 G5b, PR #2411 in review) serves the **live session** context (`TurnState` discriminated union, SSE-driven real-time display). They share the same name but have different contracts, data sources, and display purposes.
- **`ScoringPanelRenderer` in `toolkit/` is currently unreferenced from any live-session route.** `SessionLiveView.tsx` uses `LiveScoringPanel` (from `features/session-live/`), not `toolkit/ScoringPanelRenderer`. The toolkit renderer is a configuration-preview component and is not the polymorphic live dispatcher planned for G5a #2373.
- **`SessionToolsRail` (session-live) and `ToolkitDashboard` (toolkit) are complementary, not duplicates.** `SessionToolsRail` is a role-gated tool action grid for live play (3 hardcoded icons). `ToolkitDashboard` is a full admin/player configuration grid for the B-context widgets (6 domain widgets, persistent state).
- **The `toolkit/` directory is a standalone island.** It is consumed exclusively by toolkit configuration routes (`/toolkit/[sessionId]`, `/library/.../toolkit/configure`). It has zero consumption from `/sessions/[id]/live`. There is no current cross-dependency — the duplication is about naming, not actual shared code.
- **Recommended architectural direction**: treat `toolkit/` as the **AI-generation config domain** and `features/session-live/` as the **live play runtime domain**. The two `TurnIndicatorRenderer` instances should remain separate with distinct names. G5a `ScoringPanelRenderer` and G5c `ToolkitRenderer` should be built new inside `features/session-live/` and should NOT wrap or reference `toolkit/` components.

---

## 2. Inventory: `/components/toolkit/`

All 19 files. Git origin: Epic B #5128 (ToolkitDashboard + widgets, commit `369750cf0`) + B19-4a #1749 (polymorphic renderers, commit `3d1f92e96`).

| File | Purpose | Props Contract | Consumers (app routes) | Test Coverage |
|---|---|---|---|---|
| `TurnIndicatorRenderer.tsx` | Polymorphic turn-order display for **AI toolkit suggestions**. Switches on `AiTurnTemplateSuggestion.turnOrderType` string. 6 layout variants: RoundRobin, Sequential, Simultaneous, Realtime, None, Custom/Free. | `{ template: AiTurnTemplateSuggestion \| null, currentRound?, currentTurn?, currentPhaseIndex?, activePlayer?, players? }` | **None** (no import found in app pages) | `__tests__/TurnIndicatorRenderer.test.tsx` — 14+ cases |
| `ScoringPanelRenderer.tsx` | Polymorphic scoring display for **AI toolkit suggestions**. Switches on `AiScoringTemplateSuggestion.scoreType`. 4 layouts: Points, Ranking, BinaryWin, Objectives. | `{ template: AiScoringTemplateSuggestion \| null, scores?, players? }` | **None** (no import found in app pages) | `__tests__/ScoringPanelRenderer.test.tsx` — 15+ cases |
| `ToolkitDashboard.tsx` | Admin/player widget configuration grid. Renders all 6 B-context widgets in displayOrder. | `{ toolkit: ToolkitDashboardDto \| null, sessionId?, players?, isLoading?, onWidgetToggle?, onWidgetStateChange? }` | `/toolkit/[sessionId]/_content.tsx` (via barrel via session component) | `__tests__/ToolkitDashboard.test.tsx` |
| `AiToolkitGenerator.tsx` | AI toolkit generation UI (3 states: trigger / loading / review). | `{ gameId, onGenerate, onApply, onDismiss }` | `/library/private/[privateGameId]/toolkit/configure/client.tsx` | `__tests__/AiToolkitGenerator.test.tsx` |
| `TurnManagerWidget.tsx` | Widget: player turn tracker with cycling. Uses `useWidgetSync`. | `{ isEnabled, players?, sessionId?, toolkitId?, onToggle?, onStateChange? }` | `ToolkitDashboard.tsx` | `__tests__/TurnManagerWidget.test.tsx` |
| `ScoreTrackerWidget.tsx` | Widget: per-player score accumulator. | `{ isEnabled, players?, sessionId?, toolkitId?, onToggle?, onStateChange? }` | `ToolkitDashboard.tsx` | `__tests__/ScoreTrackerWidget.test.tsx` |
| `RandomGeneratorWidget.tsx` | Widget: random result generator. | `{ isEnabled, onToggle?, onStateChange? }` | `ToolkitDashboard.tsx` | `__tests__/RandomGeneratorWidget.test.tsx` |
| `ResourceManagerWidget.tsx` | Widget: resource tracking. | `{ isEnabled, onToggle?, onStateChange? }` | `ToolkitDashboard.tsx` | `__tests__/ResourceManagerWidget.test.tsx` |
| `NoteManagerWidget.tsx` | Widget: session notes manager. | `{ isEnabled, onToggle?, onStateChange? }` | `ToolkitDashboard.tsx` | `__tests__/NoteManagerWidget.test.tsx` |
| `WhiteboardWidget.tsx` | Widget: free-form whiteboard. | `{ isEnabled, onToggle?, onStateChange? }` | `ToolkitDashboard.tsx` | (covered by ToolkitDashboard test) |
| `WidgetCard.tsx` | Card wrapper with enabled/disabled visual state and toggle. | `{ isEnabled, title, icon, onToggle?, children }` | All 6 widget components | `__tests__/WidgetCard.test.tsx` |
| `CardDeckTool.tsx` | Card deck draw tool. | (self-contained stateful) | `/toolkit/[sessionId]/_content.tsx` (via barrel) | `__tests__/CardDeckTool.test.tsx` |
| `Counter.tsx` | Numeric counter primitive. | (self-contained) | Internally via `CounterTool.tsx` | (no dedicated test, covered by CounterTool) |
| `CounterTool.tsx` | Counter tool with min/max/reset. | (self-contained stateful) | `/toolkit/play/page.tsx` | `__tests__/CounterTool.test.tsx` |
| `DiceRoller.tsx` | Dice roller with history. | (self-contained stateful) | `/toolkit/play/page.tsx`, `/toolkit/[sessionId]/_content.tsx` | `__tests__/DiceRoller.test.tsx` |
| `Timer.tsx` | Countdown/countup timer. | (self-contained stateful) | `/toolkit/play/page.tsx`, `/toolkit/[sessionId]/_content.tsx` | `__tests__/Timer.test.tsx` |
| `Randomizer.tsx` | Random picker from list. | (self-contained stateful) | `/toolkit/play/page.tsx` | `__tests__/RandomizerTool.test.tsx` |
| `Scoreboard.tsx` | Simple scoreboard primitive. | (self-contained stateful) | Internally (not via app pages) | (none found) |
| `index.ts` | Barrel export: `ToolkitDashboard`, widgets, tools, polymorphic renderers | — | — | — |

**Data contracts used by `toolkit/`**: `AiTurnTemplateSuggestion`, `AiScoringTemplateSuggestion`, `ToolkitDashboardDto`, `WidgetType` — all from `@/lib/api/schemas/toolkit.schemas`.

---

## 3. Inventory: `/components/features/session-live/`

19 components (16 leaf + 3 directory structures). Origin: Wave D.2 (issues #746, #750). G1 additions: ChatAgentPanel (#2374). G5b: turn-indicator-renderer/ (#2378 — PR #2411 in review).

| File/Dir | Purpose | Props Contract | Consumers | Test Coverage |
|---|---|---|---|---|
| `TurnIndicatorRenderer.tsx` (dir) | **G5b** polymorphic turn dispatcher for **live session SSE state**. Narrows on `TurnState.type` discriminated union. 7 branches + UnknownBranch fallback. Adds `FirstPlayerToken` variant absent in `toolkit/`. | `{ state: TurnState, players: PlayerInfo[], viewerId: string, compact?, labels }` | `SessionLiveView.tsx` (desktop tab `turn` + mobile sheet `turn`) | `__tests__/TurnIndicatorRenderer.test.tsx` — 8+ cases |
| `TurnIndicator.tsx` | Simple progress bar + active-player display. Owned by `RoundRobinBranch` (delegation). | `{ current, total, activePlayerName, isMyTurn, compact?, labels }` | `turn-indicator-renderer/branches/RoundRobinBranch.tsx` | (covered by TurnIndicatorRenderer tests) |
| `LiveScoringPanel.tsx` | Real-time role-aware scoreboard with ±1 delta buttons. NOT polymorphic on `scoreType` — single Points layout. Planned replacement by G5a #2373. | `{ scores: LiveScoringPanelScoreEntry[], viewerRole, viewerId, onScoreUpdate?, compact?, labels }` | `SessionLiveView.tsx` (desktop tab `score` + mobile `score`) | `(no dedicated test found in __tests__/)` |
| `SessionToolsRail.tsx` | Role-gated tool action grid. Spectator → null. Player/Host → grid of 3 hardcoded icon buttons. | `{ tools: [{id,name,icon}][], viewerRole, onToolExecute, compact?, labels }` | `SessionLiveView.tsx` (desktop tab `widget` + mobile `widget`) | `__tests__/SessionToolsRail.test.tsx` |
| `ChatAgentPanel.tsx` | **G1 #2374 T3**. Agent header + accordion + `LiveAgentChat` body. Frozen §5 contract for G3. | `{ sessionId?, messages, viewerRole, viewerId, onSendMessage, agentName, agentEmoji?, latencyMs, collapsed?, onHeaderClick?, labels }` | `SessionLiveView.tsx` (desktop LEFT column) | `__tests__/ChatAgentPanel.test.tsx` |
| `LiveAgentChat.tsx` | Chat message list + compose area with private/shared visibility toggle. | `{ sessionId, messages, viewerRole, viewerId, onSendMessage, compact?, labels }` | `ChatAgentPanel.tsx` | `__tests__/LiveAgentChat.test.tsx` |
| `DesktopBody.tsx` | 2-col 60/40 CSS grid layout shell (lg+). | `{ leftColumn, rightColumn, className? }` | `SessionLiveView.tsx` | `__tests__/DesktopBody.test.tsx` |
| `MobileBody.tsx` | Mobile layout: main content + bottom-sheet drawer. | `{ mainContent, sheetContent, ... }` | `SessionLiveView.tsx` | `__tests__/MobileBody.test.tsx` |
| `MobileBottomSheetDrawer.tsx` | Radix Sheet bottom drawer for mobile tab content. | `{ activeTab, onTabChange, children, labels }` | `MobileBody.tsx` | `__tests__/MobileBottomSheetDrawer.test.tsx` |
| `RightColumnTabs.tsx` | Tab list + panel for desktop RIGHT col (score/turn/widget/notes). | `{ activeTab, onTabChange, children, labels }` | `SessionLiveView.tsx` | `__tests__/RightColumnTabs.test.tsx` |
| `ActionLogTimeline.tsx` | Append-only event log. | `{ entries, labels }` | `SessionLiveView.tsx` | (no dedicated test) |
| `PlayerRosterLive.tsx` | Participant list with online status. | `{ players, viewerId, viewerRole, labels }` | `SessionLiveView.tsx` | (no dedicated test) |
| `LiveTopBar.tsx` | Session top bar with role-based CTAs + connection state. | (complex labels) | `SessionLiveView.tsx` | `__tests__/LiveTopBar.test.tsx` |
| `LiveSessionNotes.tsx` | Notes list + add form. | `{ notes, viewerRole, viewerId, onAddNote, labels }` | `SessionLiveView.tsx` | `__tests__/LiveSessionNotes.test.tsx` |
| `ConnectionLostBanner.tsx` | SSE connection state banner (reconnecting/degraded/failed). | `{ kind, labels }` | `SessionLiveView.tsx` | `__tests__/ConnectionLostBanner.test.tsx` |
| `PauseOverlay.tsx` / `EndgameDialog.tsx` | Modal overlays with focus traps. | (role + labels) | `SessionLiveView.tsx` (lazy-loaded) | `__tests__/PauseOverlay.test.tsx` / `EndgameDialog.test.tsx` |
| `SessionStateRenderer.tsx` | 4-state FSM shell (loading/error/not-found/default). | — | `ScoreboardPage.tsx` | `__tests__/SessionStateRenderer.test.tsx` |
| `index.ts` | Barrel: all session-live + G5b TurnIndicatorRenderer | — | `SessionLiveView.tsx` (2 import statements) | — |

**Data contracts used by `session-live/`**: `TurnState` (from `@/lib/session-live/turn-state`), `ParticipantRole`, `PlayerInfo` — all in `lib/session-live/`. No reference to `toolkit.schemas`.

---

## 4. Duplication Matrix

| `toolkit/` Component | `features/session-live/` Component | Relationship | Notes |
|---|---|---|---|
| `TurnIndicatorRenderer` | `turn-indicator-renderer/TurnIndicatorRenderer` | **DUPLICATE names, different contracts** | Same dispatch concept (turn-order type → layout), different data sources. toolkit: `AiTurnTemplateSuggestion` (flat string props, AI config preview). session-live: `TurnState` discriminated union (SSE runtime state, 7 typed variants). `FirstPlayerToken` variant exists only in session-live. |
| `ScoringPanelRenderer` | `LiveScoringPanel` | **COMPLEMENTARY — different purpose** | toolkit: shows AI-suggested scoring structure (4 ScoreType layouts, static template display). session-live: shows real-time score delta UI (1 layout, role-aware ±1 buttons, live entry data). G5a #2373 will add a third — a polymorphic live scorer. |
| `ToolkitDashboard` | `SessionToolsRail` | **COMPLEMENTARY — different purpose** | toolkit: full B-context widget configuration grid (6 domain widgets, persistent `WidgetType` state). session-live: simple tool action grid (3 hardcoded icons, role-gated, ephemeral). |
| `TurnManagerWidget` | `TurnIndicator` (session-live primitive) | **COMPLEMENTARY — different purpose** | toolkit: full add/remove/cycle player UI widget with persistent server state. session-live: thin progress bar + active player display (presentation only). |
| `ScoreTrackerWidget` | `LiveScoringPanel` | **COMPLEMENTARY — different purpose** | toolkit: full widget with manual add/edit player scores via `useWidgetSync`. session-live: role-aware live scoreboard (read + ±1 delta). |
| `AiToolkitGenerator` | *(no counterpart)* | **UNIQUE to `toolkit/`** | Configuration-phase AI generation. No live-session equivalent. |
| `CardDeckTool`, `DiceRoller`, `Timer`, `Counter`, `Randomizer` | *(no counterpart)* | **UNIQUE to `toolkit/`** | Stateful game tool primitives. session-live uses `SessionToolsRail` as a thin triggering surface that eventually executes these tools. |

---

## 5. Migration Path Recommendation

### 5.1 PR #2411 (#2378 G5b) — MERGE AS-IS

**Recommendation: Merge without modification.**

The new `features/session-live/turn-indicator-renderer/TurnIndicatorRenderer` is architecturally correct and not duplicating the `toolkit/TurnIndicatorRenderer` in any harmful way. They serve different domains:

- `toolkit/TurnIndicatorRenderer`: consumes `AiTurnTemplateSuggestion` — an AI suggestion object used during toolkit configuration preview. It is a **config-domain UI** component.
- `features/session-live/turn-indicator-renderer/TurnIndicatorRenderer`: consumes `TurnState` — a SSE-driven runtime discriminated union. It is a **live-session runtime UI** component. It adds `FirstPlayerToken` (not in the toolkit schema) and is i18n-ready via `labels`.

The two should remain separate. The toolkit one may eventually be deprecated if the AI-config flow is reworked to emit `TurnState`-shaped data, but that is a future refactoring not related to G5.

**One naming action needed**: the `toolkit/index.ts` barrel exports `TurnIndicatorRenderer` without namespace qualification. To prevent future import confusion, consider adding a comment or an alias re-export like `export { TurnIndicatorRenderer as ToolkitTurnIndicatorRenderer }` — but this is a non-blocking cleanup, not a blocker for #2411.

### 5.2 #2376 G5c ToolkitRenderer — BUILD NEW in `features/session-live/`

**Recommendation: Build a new `ToolkitRenderer` inside `features/session-live/` that does NOT wrap or re-export `ToolkitDashboard`.**

The G5c "ToolkitRenderer" for the session-live `widget` tab is architecturally different from `ToolkitDashboard`:

- `ToolkitDashboard` renders the full **6-widget B-context configuration grid** with per-widget toggle and persistent server state. It is a heavy stateful admin/config component.
- The session-live `widget` tab currently uses `SessionToolsRail` — a **3-tool action trigger surface** (dice, timer, card). G5c should either:
  - (a) **Keep `SessionToolsRail` as-is** and make it data-driven from the game's toolkit configuration (fetching which tools are enabled), or
  - (b) **Create a new `ToolkitRenderer`** in `features/session-live/` that bridges the `ToolkitDashboardDto` data to a compact read-only session-play UI — not the full editable `ToolkitDashboard` grid.

Do NOT mount `ToolkitDashboard` inside the session-live `widget` tab — it is a configuration component, not a play component. The widget/toggle UX is wrong for an active session.

### 5.3 #2373 G5a ScoringPanelRenderer — BUILD NEW in `features/session-live/`

**Recommendation: Build a new polymorphic `ScoringPanelRenderer` inside `features/session-live/` that wraps or replaces `LiveScoringPanel`.**

The existing `toolkit/ScoringPanelRenderer` is a **config-preview** component — it displays the AI-suggested scoring structure (`AiScoringTemplateSuggestion`) with no live data. It is NOT what G5a needs.

G5a needs a **live polymorphic scorer** that:
- Accepts `scoreType` (`Points | Ranking | BinaryWin | Objectives`) from the session's toolkit
- Renders the appropriate live score UI with the existing `LiveScoringPanel` role-aware delta buttons
- Replaces the current `LiveScoringPanel` mount in `SessionLiveView.tsx` at the `score` tab

The `toolkit/ScoringPanelRenderer` visual layouts (Points/Ranking/BinaryWin/Objectives) can serve as **reference implementation** for the G5a component, but should not be imported directly. The live version needs `LiveScoringPanelScoreEntry[]` data and `onScoreUpdate` callbacks — props that `toolkit/ScoringPanelRenderer` does not have.

### 5.4 Long-term Architecture

**Canonical direction: `toolkit/` = config domain, `features/session-live/` = runtime domain. No cross-imports.**

| Domain | Location | Data Contract | Purpose |
|---|---|---|---|
| AI toolkit config preview | `components/toolkit/` | `AiToolkitSuggestion`, `ToolkitDashboardDto` | Display AI suggestions, configure widget settings |
| Live session runtime | `components/features/session-live/` | `TurnState`, `LiveScoringPanelScoreEntry`, `ParticipantRole` | Real-time play UI, SSE-driven |

**The `toolkit/TurnIndicatorRenderer` and `toolkit/ScoringPanelRenderer` should be renamed to prevent future confusion:**
- `toolkit/TurnIndicatorRenderer` → `toolkit/AiTurnPreviewRenderer` (clarifies config-only purpose)
- `toolkit/ScoringPanelRenderer` → `toolkit/AiScoringPreviewRenderer`

This rename should be tracked as a separate cleanup issue (e.g., `refactor(toolkit): rename preview renderers for clarity`) distinct from G5 work.

---

## 6. Suggested ADR

**Title**: ADR-078 — Toolkit Config Domain vs Session-Live Runtime Domain: Separation of UI Concerns

**Summary** (3 lines):

The `components/toolkit/` directory owns UI components for the AI-config and widget-configuration bounded context, consuming `AiToolkitSuggestion`/`ToolkitDashboardDto` contracts. The `components/features/session-live/` directory owns UI components for the live play runtime, consuming SSE-driven `TurnState`/`LiveScoringPanelScoreEntry` contracts. Cross-imports between the two directories are forbidden; G5 polymorphic renderers (#2373, #2376, #2378) are built exclusively in `session-live/` and may use `toolkit/` schemas as read references but not toolkit UI components.

**File**: `docs/for-claude/architecture/adr/adr-078-toolkit-vs-session-live-ui-separation.md`

---

## Appendix: File Reference Map

| File | Absolute Path |
|---|---|
| toolkit/TurnIndicatorRenderer | `apps/web/src/components/toolkit/TurnIndicatorRenderer.tsx` |
| toolkit/ScoringPanelRenderer | `apps/web/src/components/toolkit/ScoringPanelRenderer.tsx` |
| toolkit/ToolkitDashboard | `apps/web/src/components/toolkit/ToolkitDashboard.tsx` |
| session-live TurnIndicatorRenderer | `apps/web/src/components/features/session-live/turn-indicator-renderer/TurnIndicatorRenderer.tsx` |
| session-live TurnState | `apps/web/src/lib/session-live/turn-state.ts` |
| session-live LiveScoringPanel | `apps/web/src/components/features/session-live/LiveScoringPanel.tsx` |
| session-live SessionToolsRail | `apps/web/src/components/features/session-live/SessionToolsRail.tsx` |
| toolkit.schemas | `apps/web/src/lib/api/schemas/toolkit.schemas.ts` |
| SessionLiveView orchestrator | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` |
| toolkit route content | `apps/web/src/app/(authenticated)/toolkit/[sessionId]/_content.tsx` |
| toolkit configure client | `apps/web/src/app/(authenticated)/library/private/[privateGameId]/toolkit/configure/client.tsx` |
