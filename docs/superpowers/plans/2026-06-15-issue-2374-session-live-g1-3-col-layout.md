# Plan: Issue #2374 — Session-Live G1 (3-col 60/40 layout + polymorphic tabs)

> Generated 2026-06-15 via `Plan` subagent dispatched during `/sc:spec-panel "find next 5 issues"` session. Sub-issue G1 (foundation) of epic #2354 (Session live shell). HARD DEP downstream: #2375 (G3 ChatAgent always-visible). Parallel to #2373 (G5a renderer).

## §1 Context + spec links

- **Issue:** #2374 — `feat(session-live): 3-col desktop layout (60/40 chat+log / polymorphic tabs)` (G1 of epic #2354)
- **Estimate:** 4–6 days
- **Hard dep downstream:** #2375 (G3 ChatAgent always-visible) consumes the LEFT-column primitive shipped here
- **Mockup (canonical):**
  - `admin-mockups/design_files/sp4-session-skeleton-live.html`
  - `admin-mockups/design_files/sp4-session-skeleton-live.jsx`
  - `admin-mockups/design_files/sp4-session-skeleton-parts.jsx` (the load-bearing `DesktopSessionBody` + `RightColumnTabs` definitions)
  - `admin-mockups/design_files/sp4-session-skeleton-live.fidelity.json` (`design_intent: current`, viewports: `desktop`)
- **Architectural anchors:**
  - ADR-071 (Live Session 5-state FSM, PR #2380 shipped 2026-06-15)
  - ADR-060 (LiveSession persistence xmin, #2097 → #2305)
  - Asse B sidebar/topbar (sess.33, #1897) — `useMiniNavConfig` registered in parent layout
  - Asse A polymorphic `ScoreType` BE (sess.32, c1efb4fb6)
  - `PolymorphicScoreEditor` (sess.35) lives at `apps/web/src/components/sessions/PolymorphicScoreEditor.tsx` (NOT under `features/session-live/scoring/...` as the issue body states; flag for correction in the issue)
  - Token canonicalization Tier 1–4 complete (CLAUDE.md §306); `local/no-hardcoded-color-utility` is **error**

## §2 Discovery notes (current state)

### 2.1 Current `SessionLiveView` 3-column wiring
`apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`:
- L949–969 `desktopLeftSidebar`: `TurnIndicator` + `PlayerRosterLive`
- L971–981 `desktopCenterColumn`: `LiveScoringPanel` + `ActionLogTimeline`
- L984–1013 `desktopRightColumn`: `RightColumnTabs` with `tools|chat|notes`
- L1055–1060 `<DesktopBody>` composition

### 2.2 Current `DesktopBody` widths
`apps/web/src/components/features/session-live/DesktopBody.tsx`:
- L40–45 left aside: **`w-[280px]` fixed**
- L48 center: `flex-1`
- L51–54 right aside: **`w-[340px]` fixed**
- Uses raw `bg-[hsl(240,40%,10%)]` (token-discipline violation — needs replacement)

### 2.3 Current `RightColumnTabs` tab keys
`apps/web/src/components/features/session-live/RightColumnTabs.tsx`:
- L31 `LiveTab = 'tools' | 'chat' | 'notes'` (only 3 tabs)
- L33 `ORDERED_TABS` defines order
- Roving tabindex via `useTablistKeyboardNav` is already correct

### 2.4 Gap vs canonical mockup
| Aspect | Current | Mockup spec |
|---|---|---|
| Desktop columns | LEFT 280px / CENTER flex / RIGHT 340px (3 zones) | LEFT 60% (chat+log) / RIGHT 40% (tabs) — only 2 zones at the body level |
| Tab keys | `tools \| chat \| notes` | `scoring \| turn \| widget \| notes` (per mockup: Score / Turni / Widget / Note) |
| LEFT content | `LiveScoringPanel + ActionLogTimeline` in CENTER, roster+turn in LEFT | `ChatAgentPanel + ActionLogTimeline` stacked, **with expanded/collapsed accordion behaviour** (see parts.jsx L252–263) |
| ChatAgent visibility | Lives inside RIGHT tab `chat` (not always visible) | "Magnete" — always visible in LEFT |
| Background tokens | `bg-[hsl(240,40%,10%)]` raw HSL | `bg-card` / `bg-background` semantic tokens |
| Mobile | bottom-nav 4 tabs (`score\|log\|tools\|chat`) | LEFT same stack full-width, RIGHT becomes bottom-sheet drawer (parts.jsx L266–286) |

### 2.5 Existing primitives reusable
- `LiveAgentChat` (`features/session-live/LiveAgentChat.tsx`) — full chat panel, accepts `compact`, has `data-slot="live-agent-chat"`. Will become the body of new `ChatAgentPanel` primitive (G3 will hoist this).
- `ActionLogTimeline` (`features/session-live/ActionLogTimeline.tsx`) — append-only timeline with `compact` prop and labels.
- `LiveScoringPanel` (`features/session-live/LiveScoringPanel.tsx`) — read-only scoreboard (no polymorphic dispatch yet).
- `PolymorphicScoreEditor` (`components/sessions/PolymorphicScoreEditor.tsx`) + `score-strategies/*` (BinaryWinEditor, ObjectivesEditor, PointsEditor, RankingEditor). **Existing**, but currently used in summary route, not in live.
- `TurnIndicator` (`features/session-live/TurnIndicator.tsx`) — single-shape turn display.
- `LiveSessionNotes` (`features/session-live/LiveSessionNotes.tsx`) — notes with private/shared toggle.
- `SessionToolsRail` (`features/session-live/SessionToolsRail.tsx`) — kept for backward compat; relabelled "Widget" in new tab key.
- `useTablistKeyboardNav` — keyboard navigation hook used by `RightColumnTabs`.
- Asse B `useMiniNavConfig` (parent `layout.tsx` L58) — already registers a session-level tab strip on the desktop topbar. **NOT to be touched** in this scope.

### 2.6 Existing tests
- `apps/web/src/components/features/session-live/__tests__/RightColumnTabs.test.tsx` — 14 tests covering tablist contract; will be **rewritten** (tab keys change tools→score/turn/widget/notes).
- `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx` — 40+ tests; tabs in T3.2a–e need new tab key values.
- `apps/web/e2e/a11y/session-live.spec.ts` — axe-AA scan; ensures we keep 0 violations after refactor.
- `apps/web/e2e/smoke-real-backend/session-live.smoke.spec.ts` — smoke probe (basic data-slots).
- No dedicated `DesktopBody.test.tsx` exists today — we add one in Task 2.

### 2.7 i18n
- `apps/web/src/locales/it.json` L3241–3246 has only `tabTools | tabChat | tabNotes`. Needs `tabScore | tabTurn | tabWidget | tabNotes` (with backwards-compat alias for `tabTools→tabWidget`).
- Mirror in `apps/web/src/locales/en.json` L3138.

## §3 Architectural decisions

### D-1 Layout topology — refactor in place, no new files for `DesktopBody`
The mockup uses **2 body zones** (LEFT 60% / RIGHT 40%). The existing `DesktopBody` already accepts `leftSidebar | centerColumn | rightColumn` — too many slots. We **rename slot semantics** rather than rewrite the file path:

- `leftSidebar` (DEPRECATED) → still supported, default `undefined`. Kept to avoid breaking any callers.
- New props: `mainColumn` (LEFT 60% — chat+log stacked) + `rightColumn` (RIGHT 40% — polymorphic tabs).
- Grid template: `grid-template-columns: minmax(0, 3fr) minmax(0, 2fr)` (60/40 ratio, fluid; min-width guards against squish).
- Roster + Turn indicator absorbed into the RIGHT `turn` tab content.

Rationale: the issue is "refactor in place"; mockup explicitly drops the 3rd sidebar zone. Single component, additive props with deprecation.

### D-2 Polymorphic tab keys (new `LiveTab` union)
New union: `'score' | 'turn' | 'widget' | 'notes'` (default `'score'`, per mockup).
- BREAKING for URL: `?tab=tools|chat|notes` → `?tab=score|turn|widget|notes`. Add a one-pass migration in `parseLiveTab`:
  - `tools` → `widget` (semantic equivalent — `SessionToolsRail`)
  - `chat` → `score` (chat is no longer a tab; it's always-visible in LEFT; falling back to default is safest)
  - `notes` → `notes` (unchanged)

### D-3 ChatAgent panel primitive (the G3 contract)
G1 introduces a thin **wrapper primitive** `ChatAgentPanel` (new file under `features/session-live/`) that:
- Wraps `LiveAgentChat` with a header (avatar, name, "Online" pip, "ChatAgent" pill) — see mockup parts.jsx L91–135.
- Accepts `collapsed: boolean` + `onHeaderClick: () => void` for the accordion behaviour shown in mockup `DesktopSessionBody` L253.
- Default uncollapsed in G1 (no accordion logic wired here); the `collapsed` prop is exposed so G3 (#2375) can wire the expand/collapse FSM without touching this file again.

**Exposed contract for #2375:**
```ts
export interface ChatAgentPanelProps {
  readonly messages: ReadonlyArray<ChatMessage>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly onSendMessage: (content: string, visibility: 'private' | 'shared') => void;
  readonly agentName: string;
  readonly agentEmoji: string;
  readonly latencyMs: number;
  readonly collapsed?: boolean;
  readonly onHeaderClick?: () => void;
  readonly compact?: boolean;
  readonly labels: ChatAgentPanelLabels;
}
```

### D-4 Token discipline
- Drop **every** `bg-[hsl(240,40%,8%)]` / `bg-[hsl(240,40%,10%)]` in `SessionLiveView.tsx` + `DesktopBody.tsx`; use `bg-background` / `bg-card`.
- Keep `data-theme="dark"` on the root (intentional dark default).
- ChatAgentPanel header uses `bg-entity-agent/10` + `text-entity-agent`.
- Active tab uses `border-entity-session` per mockup `eHsl(t.entity)` mapping: Score→session, Turn→player, Widget→toolkit, Notes→kb.

### D-5 Mobile fallback (revised 2026-06-15 sess.46r — INCLUDED via T9)
~~G1 only adds the desktop 60/40. Mobile (`MobileBody`) keeps current bottom-nav. The mockup's bottom-sheet drawer pattern (parts.jsx L266–286) is **out of scope** here.~~

**Revised**: Per spec-panel critique (sess.46r) the mockup's bottom-sheet drawer pattern is canonical (DEC-4 user-locked). G1 ships full mobile parity via **T9** (new task, see §4). Mobile = main full-width (ChatAgentPanel + ActionLogTimeline) + floating action button → bottom-sheet drawer (Score/Turn/Widget/Note tabs). Effort impact: +2.5 days (subtotal 4.75d → 7.25d).

### D-6 Polymorphic Score/Turn renderers
G1 introduces the **tab plumbing** (Score/Turn/Widget/Notes), not the polymorphic renderer interiors:
- `Score` tab: renders existing `LiveScoringPanel` (G5 will replace with polymorphic dispatcher).
- `Turn` tab: renders `TurnIndicator` + `PlayerRosterLive` (currently in LEFT sidebar).
- `Widget` tab: renders existing `SessionToolsRail` (G5: `ToolkitRenderer` dispatcher).
- `Notes` tab: renders existing `LiveSessionNotes`.

This isolates layout work from polymorphic-dispatch work and unblocks the 4-6 day budget.

## §4 Task breakdown (TDD-ordered)

### T1 — Update `RightColumnTabs` tab keys (Score/Turn/Widget/Notes)
**Test changes** (`__tests__/RightColumnTabs.test.tsx`):
1. Rename `LABELS` to use `tabScore | tabTurn | tabWidget | tabNotes`.
2. Replace `activeTab: 'tools'` defaults with `'score'`.
3. Update keyboard nav test ordering (score → turn → widget → notes; ArrowRight from `notes` wraps to `score`).
4. Add **new** test "tab buttons render in mockup order".
**Impl changes** (`RightColumnTabs.tsx`):
- L31 `LiveTab = 'score' | 'turn' | 'widget' | 'notes'`
- L33 `ORDERED_TABS = ['score', 'turn', 'widget', 'notes'] as const`
- L37–42 `RightColumnTabsLabels`: rename fields.
- L66–73 `tabLabels` map: new keys.

**Effort:** 0.5 day.

### T2 — Add `DesktopBody.test.tsx` + refactor for 60/40 + `mainColumn`
**New test file** (`__tests__/DesktopBody.test.tsx`):
1. `renders data-slot="desktop-body"` (regression).
2. `renders mainColumn + rightColumn slots` (new contract).
3. `applies 60/40 grid template` — inspect computed `gridTemplateColumns` includes `3fr` and `2fr`.
4. `omits leftSidebar slot when undefined (back-compat path)`.
5. `still mounts leftSidebar when provided (legacy callers)`.
6. `root uses bg-background (no raw HSL)` — assert `className` does not contain `bg-[hsl(`.
7. `hidden on mobile (< lg)` — assert `hidden lg:grid`.

**Impl** (`DesktopBody.tsx`):
- Add `mainColumn?: ReactNode` (new) AND keep `centerColumn?: ReactNode` (alias). When `mainColumn` provided, use the 2-zone grid; when only `centerColumn`, fall back to existing 3-zone flex (deprecation soft-landing).
- Switch root to `grid grid-cols-[minmax(0,3fr)_minmax(0,2fr)]` when 2-zone path is active.
- Replace `bg-[hsl(240,40%,10%)]` → `bg-card border-border/60`.
- JSDoc update: mark `leftSidebar` + `centerColumn` `@deprecated — use mainColumn for 60/40 layout (G1 #2374)`.

**Effort:** 0.5 day.

### T3 — Create `ChatAgentPanel` primitive
**New test** (`__tests__/ChatAgentPanel.test.tsx`):
1. `renders data-slot="chat-agent-panel"`.
2. `header shows agent name + emoji + Online pip`.
3. `header has "ChatAgent" pill (mockup magnet semantic)`.
4. `body renders LiveAgentChat with same data-slot`.
5. `collapsed=true hides body, shows expanded=false aria`.
6. `clicking header fires onHeaderClick (when provided)`.
7. `compact prop forwards to LiveAgentChat`.
8. `Gate A ICU: latency label uses pre-resolved string`.
9. `respects token discipline: no raw HSL; uses bg-entity-agent/* + text-entity-agent`.

**Impl** (new `ChatAgentPanel.tsx`):
- Functional component matching mockup parts.jsx L91–135 (header layout + emoji avatar + pulse pip + ChatAgent pill).
- Body delegates to `<LiveAgentChat />` (no duplication).
- Export from `index.ts` barrel.

**Effort:** 1 day.

### T4 — Refactor `SessionLiveView` to 60/40 + new tabs
**Test changes** (`__tests__/SessionLiveView.test.tsx`):
1. Tests T3.2a–e (L600–634): change expected `data-active-tab` values from `'tools'` → `'score'`, etc.
2. **New test:** `default ?tab is "score" (not "tools")`.
3. **New test:** `legacy ?tab=tools is treated as widget (back-compat)`.
4. **New test:** `Cell 5 default desktop renders <ChatAgentPanel /> in LEFT (60%)`.
5. **New test:** `Cell 5 default desktop renders <RightColumnTabs /> in RIGHT (40%)`.
6. **New test:** `LEFT column also renders ActionLogTimeline stacked below ChatAgent`.
7. **New test:** `RIGHT column "turn" tab renders TurnIndicator + PlayerRosterLive (moved from sidebar)`.
8. **New test:** `desktopBody has data-layout="2col-60-40"` (added attribute for E2E selectors).
9. Mobile tab routing tests: keep existing — mobile out-of-scope.
10. i18n: extend MESSAGES dict with `pages.sessionLive.rightColumn.tabScore | tabTurn | tabWidget | tabNotes` keys.

**Impl** (`SessionLiveView.tsx`):
- L135 `LiveTab = 'score' | 'turn' | 'widget' | 'notes'`.
- L137 `parseLiveTab`: map legacy `tools|chat|notes` → `widget|score|notes`.
- L687–695 `rightColumnTabsLabels` → use new label keys.
- L949–969 DELETE `desktopLeftSidebar`.
- L971–981 RENAME `desktopCenterColumn` → `desktopMainColumn`; replace contents:
  ```tsx
  <div className="flex flex-col gap-3 p-3 min-h-0 flex-1 overflow-hidden">
    <ChatAgentPanel messages={chatMessages} ... labels={chatAgentLabels} />
    <ActionLogTimeline entries={activeSession.actionLog} labels={actionLogLabels} />
  </div>
  ```
- L984–1013 `desktopRightColumn` switch (`score`/`turn`/`widget`/`notes` cases). The `turn` case mounts `<TurnIndicator />` + `<PlayerRosterLive />`. The `widget` case mounts `<SessionToolsRail />`.
- L1055 `<DesktopBody mainColumn={desktopMainColumn} rightColumn={desktopRightColumn} />`.
- Add new `chatAgentLabels` memo block.
- Replace `bg-[hsl(240,40%,8%)]` root background → `bg-background` (4 occurrences L886, L900, L919, L939, L1020).

**Effort:** 1.5 days.

### T5 — i18n keys + locale parity
**Impl** (`locales/it.json` + `en.json`):
- Add `pages.sessionLive.rightColumn.tabScore`, `tabTurn`, `tabWidget`. KEEP `tabTools | tabChat | tabNotes` for back-compat.
- Add `pages.sessionLive.chatAgent.title | onlineLabel | latencyAriaLabel | offlineLabel | …` for new `ChatAgentPanel`.

**Effort:** 0.25 day.

### T6 — Playwright a11y refresh (`session-live.spec.ts`)
**Test changes**:
- `axe-core default state` should still pass with 0 violations (regression).
- Add **new test:** `desktop 60/40 layout — both columns have correct role/landmarks`.
- Add **new test:** `RightColumnTabs has 4 tab buttons after refactor`.
- Add **new test:** `LEFT column is keyboard-traversable: Tab order ChatAgentPanel → ActionLogTimeline → RightColumnTabs`.

**Effort:** 0.5 day.

### T7 — Playwright visual baseline + designer review prep
Per CLAUDE.md L302: "Visual Gate REMOVED 2026-05-20 — replacement = manual designer review on PRs".
- Capture before/after screenshots.
- Update mockup fidelity meta: `sp4-session-skeleton-live.fidelity.json` → `designer_approved_by: "<designer>"` after review.

**Effort:** 0.25 day.

### T8 — Update SessionLiveView JSDoc + emit ADR breadcrumb
**Impl:** Update JSDoc at L1–42 of `SessionLiveView.tsx` to mention G1 (#2374). Optionally drop a one-paragraph ADR-072-stub note in CLAUDE.md.

**Effort:** 0.25 day.

### T9 — Mobile bottom-sheet drawer (added 2026-06-15 sess.46r per spec-panel DEC-4)
**Test changes** (`__tests__/MobileBody.test.tsx`):
1. Rename current tab-based assertions to drawer-based (`bottomSheetOpen` / `bottomSheetTab`).
2. New test: `default mobile renders ChatAgentPanel + ActionLogTimeline full-width`.
3. New test: `tapping floating action button opens bottom-sheet drawer with Score/Turn/Widget/Note tabs`.
4. New test: `bottom-sheet drawer is keyboard-traversable + has focus trap`.
5. New test: `swipe-down gesture closes drawer (touch handler)`.
6. New test: `?mtab=turn opens drawer pre-selected to Turn tab (URL SSOT preserved)`.
7. New test: `axe AA 0 violations on default and drawer-open states`.

**New primitive** (`components/features/session-live/MobileBottomSheetDrawer.tsx`):
- Wraps Radix UI `Sheet` (already in `components/ui/sheet`) or vaul-style drag handle.
- Props: `open: boolean`, `onOpenChange: (open: boolean) => void`, `activeTab: LiveTab`, `onTabChange: (tab: LiveTab) => void`, `children: ReactNode`.
- Header: drag handle (visual cue) + close button. Tab strip directly below.
- Content area: dispatches active tab to passed children (render-prop pattern OR slotted children matching mockup parts.jsx L266-286).
- data-slot=`mobile-bottom-sheet`.

**Impl** (`MobileBody.tsx`):
- DELETE bottom-nav (4 tabs) layout.
- Main area = `ChatAgentPanel` + `ActionLogTimeline` stacked (mirror desktop LEFT 60%).
- Floating action button bottom-right opens `MobileBottomSheetDrawer` with `LiveTab` tabs.
- Mobile `mtab` URL SSOT preserved: legacy `?mtab=score|log|tools|chat` → `?mtab=score|turn|widget|notes` (same back-compat map as desktop, log absorbed into ActionLogTimeline always-visible).
- Refactor `parseMobileTab` to new `LiveTab` union (reuses desktop's).
- E2E `apps/web/e2e/session-live-mobile.spec.ts` (new): 375×667 viewport — default render, drawer open, tab switch, swipe close.

**Impl** (`SessionLiveView.tsx`):
- L820–868 `mobileContent` switch: rewrite — main column always = ChatAgentPanel + ActionLogTimeline; drawer content = same switch as `desktopRightColumn`.
- Wire `mobileBottomSheetOpen` state (URL SSOT `?msheet=open|closed`, default closed).
- Reuse desktop's `desktopRightColumn` switch logic (DRY).

**Effort:** 2.5 days (1.5d primitive + tests, 1d SessionLiveView wiring + E2E).

### Sequencing (revised 2026-06-15 sess.46r)
T1 → T2 (parallel-safe) → T3 → T5 → T4 → T6 → T7 → T8 → T9.
T1 and T2 are independent; T3 must precede T4 (T4 imports `ChatAgentPanel`); T6 needs T4 deployed; T7 captures the final state; **T9 added at the end because it reuses T3 `ChatAgentPanel` + T4 `desktopRightColumn` switch logic + T1 `LiveTab` union**.

### Commits / PR strategy (revised)
**Single feature branch** `feature/issue-2374-3col-layout` with **9-10 logical commits**:
1. `test(session-live): T1 RightColumnTabs new tab keys (score/turn/widget/notes)`
2. `feat(session-live): T1 update RightColumnTabs tab keys`
3. `test(session-live): T2 DesktopBody 60/40 grid contract`
4. `feat(session-live): T2 DesktopBody mainColumn + 60/40 grid`
5. `test(session-live): T3 ChatAgentPanel primitive`
6. `feat(session-live): T3 ChatAgentPanel primitive`
7. `feat(session-live): T4 refactor SessionLiveView to 60/40 + new tabs (with back-compat)`
8. `feat(i18n): T5 polymorphic tab keys + chatAgent labels`
9. `test(e2e): T6 a11y desktop + JSDoc T8`
10. `feat(session-live): T9 mobile bottom-sheet drawer refactor`

## §5 Primitive contract for #2375 G3 (HARD DEP downstream)

`ChatAgentPanel` exposes the stable surface #2375 will lean on. Frozen contract:

```ts
// apps/web/src/components/features/session-live/ChatAgentPanel.tsx
export interface ChatAgentPanelLabels {
  readonly title: string;            // "ChatAgent"
  readonly agentNameAriaLabel: string;
  readonly onlineLabel: string;      // "Online"
  readonly latencyAriaLabel: string; // "Latenza {ms}ms" (Gate A pre-resolved)
  readonly chatPanelLabels: LiveAgentChatLabels;
}

export interface ChatAgentPanelProps {
  readonly messages: ReadonlyArray<ChatMessage>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly onSendMessage: (content: string, visibility: 'private' | 'shared') => void;
  readonly agentName: string;
  readonly agentEmoji?: string;       // default '🤖'
  readonly latencyMs: number;
  readonly collapsed?: boolean;       // default false; G3 controls
  readonly onHeaderClick?: () => void; // G3 wires accordion FSM
  readonly compact?: boolean;
  readonly labels: ChatAgentPanelLabels;
  readonly className?: string;
}
```

### Guarantees to #2375
1. **Data-slot stable**: `data-slot="chat-agent-panel"` will be the E2E selector forever (do not rename).
2. **Always-visible by default**: G3 only adds the collapsed=true semantics + URL state SSOT (`?chat=collapsed|expanded`), never moves the component to a tab.
3. **Header is a `<button>` when `onHeaderClick` provided**: A11y contract = `aria-expanded={!collapsed}` and proper Enter/Space activation.
4. **Body unmounts when `collapsed=true`** to avoid hidden focus traps.
5. **No SSE state** inside the primitive; messages flow in via prop (parent owns the stream).
6. **Token classes are entity-agent based** — G3 must not override colours.

### Co-owned dataset (left for #2375 to extend)
- `expanded: 'chat' | 'log'` accordion state lives in `SessionLiveView` (URL SSOT). G1 ships with both panels uncollapsed; G3 will introduce the toggle.

## §6 Risks + mitigations

### R-1 (HIGH) — Back-compat URL `?tab=tools|chat|notes`
**Risk:** Existing bookmarks + deep links to `?tab=chat` would 404-render the wrong panel.
**Mitigation:** `parseLiveTab` legacy alias map (D-2). Add explicit Vitest test (T4.3) to lock the mapping. Watch for breakage in `apps/web/e2e/a11y/session-live.spec.ts`.

### R-2 (HIGH → RESOLVED 2026-06-15 sess.46r) — Mobile layout drift
**Risk:** Mockup defines a bottom-sheet drawer for RIGHT on mobile; current `MobileBody` uses bottom-nav with 4 tabs.
**Mitigation (revised)**: Implemented in **T9** (new task). Mobile gets a bottom-sheet drawer primitive matching mockup parts.jsx L266–286. Old bottom-nav DELETED. URL SSOT `?mtab=` preserved with same back-compat alias map as desktop (`tools→widget`, `chat→score`, `log` absorbed). E2E mobile viewport tests added (`session-live-mobile.spec.ts`). Risk closed.

### R-3 (HIGH) — Asse B `useMiniNavConfig` collision
**Risk:** Parent layout (`sessions/[id]/layout.tsx` L58) registers a top-level tab bar on the Asse B mini-nav slot. The new RIGHT-column tabs sit ABOVE the same surface, which the user may experience as duplicate navigation on the `/live` route.
**Mitigation:**
- Inspect during T4: if the mini-nav tabs render on `/live` route, conditionally hide them via `useMiniNavConfig({ tabs: [] })`.
- Add Playwright assertion in T6: only ONE tablist rendered above-the-fold on `/live` page.
- Escalate to design if the duplicate looks intentional.

### R-4 (MEDIUM) — Token discipline regression
**Risk:** `SessionLiveView` currently has a file-level `eslint-disable local/no-hardcoded-color-utility` directive (L1). Token-canonicalization mode is **error**.
**Mitigation:**
- T4 explicitly replaces all `bg-[hsl(240,40%,8%)]` → `bg-background` (CLAUDE.md L313).
- After refactor, attempt to remove the file-level disable; if still needed, narrow to line-level. Run `pnpm lint:tokens` and update `audits/2026-05-12-token-violations.md`.

### R-5 (MEDIUM) — Test breakage cascade in `SessionLiveView.test.tsx`
**Risk:** 40+ tests reference current tab keys. Mass-rename will silently miss assertions.
**Mitigation:** Run Vitest in watch mode during T1–T4; explicit search-and-replace of `'tab=chat'`, `'tab=tools'`, `tabTools`, etc. as a single grep PR pass.

### R-6 (MEDIUM) — `data-theme="dark"` hardcoded
**Risk:** Mockup is dark default; SessionLiveView L886 hardcodes `data-theme="dark"`. A11y a11y/session-live.spec.ts L23 already documents the gap.
**Mitigation:** Use semantic tokens (`bg-card`/`text-foreground`) — they already react to `[data-theme]`. No new hardcoded dark colors introduced by G1.

### R-7 (LOW) — Mockup primitive `PolymorphicScoreEditor` mis-pathed
**Risk:** Issue body claims `apps/web/src/components/features/session-live/scoring/PolymorphicScoreEditor.tsx`; actual location is `apps/web/src/components/sessions/PolymorphicScoreEditor.tsx`.
**Mitigation:** Surface in PR description; not blocking for G1. Open follow-up `chore: move PolymorphicScoreEditor under features/session-live`.

### R-8 (LOW → RESOLVED 2026-06-15 sess.46r) — Fidelity meta has `viewports: ["desktop"]`
**Risk:** Mockup spec is desktop-only; mobile layout has no canonical reference image.
**Mitigation (revised)**: T9 references mockup `sp4-session-skeleton-parts.jsx` L266-286 (`MobileBottomSheet` primitive) as canonical for mobile. Update fidelity meta `viewports: ["desktop", "mobile"]` in T9 alongside the implementation. Designer reviews both viewports in T7.

## §7 Effort estimate breakdown (revised 2026-06-15 sess.46r)

| Task | Description | Effort |
|---|---|---|
| T1 | RightColumnTabs tab keys refactor + tests | 0.5 d |
| T2 | DesktopBody 60/40 grid + tests | 0.5 d |
| T3 | ChatAgentPanel primitive + tests | 1.0 d |
| T4 | SessionLiveView refactor + tests + back-compat | 1.5 d |
| T5 | i18n keys it/en parity | 0.25 d |
| T6 | Playwright a11y refresh | 0.5 d |
| T7 | Visual baseline + designer review prep | 0.25 d |
| T8 | JSDoc + ADR breadcrumb | 0.25 d |
| T9 | **Mobile bottom-sheet drawer (NEW)** | **2.5 d** |
| **Subtotal** | | **7.25 d** |
| Buffer (15%) | Risk R-1, R-3 + T9 integration | 1.0 d |
| **Total** | | **~8.25 days** |

Exceeds original issue estimate of 4–6 days. Revised effort matches spec-panel critique DEC-4 (mockup mobile parity in-scope). Issue body to be updated post-merge.

## Critical Files for Implementation
- `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- `apps/web/src/components/features/session-live/DesktopBody.tsx`
- `apps/web/src/components/features/session-live/RightColumnTabs.tsx`
- `apps/web/src/components/features/session-live/MobileBody.tsx` (T9 refactor)
- `apps/web/src/components/features/session-live/ChatAgentPanel.tsx` (NEW T3)
- `apps/web/src/components/features/session-live/MobileBottomSheetDrawer.tsx` (NEW T9)
- `apps/web/src/components/features/session-live/index.ts` (barrel update)
- `apps/web/e2e/session-live-mobile.spec.ts` (NEW T9)

## §8 Spec-panel critique log (2026-06-15 sess.46r)

Plan revised post `/sc:spec-panel "lavora 2374"` user invocation. Expert panel: Wiegers, Fowler, Adzic, Nygard, Cockburn. Identified 8 ambiguità; lockate 4 BLOCKING/HIGH via AskUserQuestion:

- **DEC-1** (Q1): Layout = 2-col 60/40 (mockup authoritative) — title "3-col" was legacy, body conferma 60/40.
- **DEC-2** (Q2): G1 scope = layout + **full reuse esistente** (LiveScoringPanel/TurnIndicator/SessionToolsRail wired in tabs); G3/G5 polymorphic refactor separate.
- **DEC-3** (Q3): TurnIndicator+PlayerRosterLive → inside RIGHT 'Turni' tab (D-6 already had this).
- **DEC-4** (Q4): Mobile fallback = **bottom-sheet drawer (mockup)** — required T9 addition + effort revision +2.5d.

Plan otherwise validated: D-1/D-2/D-3/D-4/D-6 unchanged. Tasks T1-T8 unchanged. Effort 5.5d → 8.25d.
