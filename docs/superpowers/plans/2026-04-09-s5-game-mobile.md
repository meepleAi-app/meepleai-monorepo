# S5 — Game Page Mobile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`.

**Goal:** Rewrite the mobile variant of the game detail page to use a MeepleCard hero + a slide-down drawer containing the 5 tab components (Info / AI Chat / Toolbox / House Rules / Partite) built in S4.

**Architecture:** Mobile-only change. Reuses the S4 shared tab component contract (`GameTabProps` with `variant='mobile'`), the existing `Sheet` primitive (Radix Dialog based, already in the codebase), and the existing `useLibraryGameDetail` hook. The drawer-from-top mechanism is implemented via `Sheet` with `side="top"` to mirror the reference mockup `admin-mockups/mobile-card-layout-mockup.html`.

**Scope corrections from the spec (§4.5):**
- **Hand stack deferred**: the 44px vertical strip of mini-cards for cross-game navigation is out of S5 scope. Reason: it requires virtualization + library fetch + swipe gestures on the focused card, which balloons the sub-feature without delivering core value. Documented as a known limitation; can be re-added in a polish follow-up.
- **Top drawer via `Sheet side="top"`**: no custom drawer component. The existing `Sheet` primitive already supports top-anchored variants.
- **No Zustand store**: local `useState` is sufficient for drawer open/close + active tab.
- **Cross-game navigation** happens via the standard back button + library list, not the hand stack.

**Reference spec:** `docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md` §4.5
**Branch:** `feature/s5-game-mobile` (from `epic/library-to-game`)

---

## File Structure

**New files:**
- `apps/web/src/components/game-detail/mobile/GameDetailsDrawer.tsx` — Sheet-based top drawer with 5 tabs (horizontal scrollable tab bar + scrollable content)
- `apps/web/src/components/game-detail/mobile/FocusedGameCard.tsx` — hero MeepleCard wrapper + "Dettagli" CTA button that opens the drawer
- `apps/web/src/components/game-detail/mobile/__tests__/GameDetailsDrawer.test.tsx`
- `apps/web/src/components/game-detail/mobile/__tests__/FocusedGameCard.test.tsx`

**Modified files:**
- `apps/web/src/app/(authenticated)/library/games/[gameId]/game-detail-mobile.tsx` — full rewrite (delete current 303-line implementation, replace with ~60-line composition of new components)

**Total estimate:** ~5 new files + 1 modified, ~400 LOC + ~200 LOC tests.

---

## Tasks

### Task 1 — `GameDetailsDrawer` (5 tabs via shared contract)

Writes a `Sheet` (side="top") with:
- Drawer handle at top
- Horizontal tab bar (5 tabs, scrollable on small screens)
- Active tab content rendered inline using the S4 tab components with `variant='mobile'`
- Close button in header
- State: `activeTab: GameTabId` via `useState`

Props: `{ open: boolean; onOpenChange: (open: boolean) => void; gameId: string; isPrivateGame?: boolean; isNotInLibrary?: boolean; initialTab?: GameTabId }`

### Task 2 — `FocusedGameCard` (MeepleCard hero + Dettagli button)

Renders `MeepleCard variant='hero'` with a prominent "📋 Dettagli" button below. Tap → opens the drawer. Delegates all data fetching to the parent.

Props: `{ gameDetail: LibraryGameDetail; onOpenDrawer: () => void }`

### Task 3 — Rewrite `game-detail-mobile.tsx`

Replace the current 303-line implementation. New composition:
```tsx
function GameDetailMobile({ gameId }) {
  const { data: game, isLoading, isError } = useLibraryGameDetail(gameId);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [initialTab, setInitialTab] = useState<GameTabId>('info');

  // ... loading / error states ...

  return (
    <>
      <FocusedGameCard gameDetail={game} onOpenDrawer={() => setDrawerOpen(true)} />
      <GameDetailsDrawer
        open={drawerOpen}
        onOpenChange={setDrawerOpen}
        gameId={gameId}
        initialTab={initialTab}
        isNotInLibrary={!game}
      />
    </>
  );
}
```

### Task 4 — Tests + push + PR

- Unit test for `GameDetailsDrawer` (tabs switching, close on backdrop)
- Unit test for `FocusedGameCard` (renders MeepleCard, click dispatches callback)
- Typecheck + lint
- Push + PR to `epic/library-to-game`

---

## Known limitations

- No hand stack (deferred to polish follow-up).
- No swipe-to-navigate between games (deferred with hand stack).
- MobileActionBar auto-hide (§4.5 bullet 5) is NOT implemented — current `ActionBar` is global (managed by UserShell) and its visibility is not coupled to the game page drawer state.
- The Partite tab still uses the summary data from `useLibraryGameDetail` (full pagination deferred to S6b follow-up).

These are acceptable scope cuts that keep S5 mergeable in the epic timeline.
