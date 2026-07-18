# Wave #3 WS-A — Activate NightLive mobile — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate the already-built (but unwired) mobile layout of `NightLiveHub` on `/game-nights/[id]/live`, and resolve the bottom-of-screen stacking so the organizer CTAs don't overlap the hub's mobile tab bar.

**Architecture:** `NightLiveHub` already renders a full mobile UI (3-tab bottom nav) when passed `mobile`; its caller `NightLiveClientView` never passes it. We wire viewport detection (`useResponsive`, SSR-safe) to pass `mobile={!isDesktop}`, add the route to `isImmersiveRoute` so the global `MobileBottomBar` hides, and offset the 3 fixed organizer CTAs above the hub's mobile tab bar with safe-area padding. No new UI is designed.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript, Tailwind 4, Vitest + Testing Library.

**Spec:** `docs/superpowers/specs/2026-07-18-wave3-mobile-session-live-parity-design.md` · **Tracking:** #3150

## Global Constraints

- Branch: `feature/issue-3150-wave3-nightlive-mobile` (already created, parent `main-dev`).
- Breakpoint parity: desktop = `>= lg` (1024px, `BREAKPOINTS.lg`), matching `SessionLiveView`'s `lg:` CSS split. Mobile hub renders `< lg`.
- Return types: do NOT annotate React components with an explicit `: JSX.Element` return where it exists already it's fine, but new code uses inference (pre-commit typecheck can fail on global JSX namespace).
- No new hardcoded hex/scrim colors — semantic tokens / entity utilities only.
- `env(safe-area-inset-bottom)` for safe-area (not a color; allowed).
- Commit type must be `feat|fix|docs|refactor|test|chore`. Each task commits `fix(game-night): #3150 …`.
- Commits: run `git commit` with `run_in_background: true` (pre-commit runs full FE `pnpm typecheck` unconditionally, ~5min). Clear `apps/web/.next/types` before committing if a stale-types failure appears.
- Run tests from `apps/web`: `cd apps/web && pnpm exec vitest run <path>`.

---

### Task 1: Add `/game-nights/[id]/live` to immersive routes

Enabler: makes `MobileBottomBar` hide (and `DesktopShell` drop bottom padding) on the night-live route, so the hub's own mobile tab bar is the only bottom nav. Isolated and independently reviewable.

**Files:**
- Modify: `apps/web/src/components/layout/AppNav/immersive-routes.ts`
- Create (test): `apps/web/src/components/layout/AppNav/immersive-routes.test.ts`

**Interfaces:**
- Consumes: `isImmersiveRoute(pathname: string): boolean` (existing export).
- Produces: no new exports; behavior change only.

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/AppNav/immersive-routes.test.ts`:

```ts
import { describe, it, expect } from 'vitest';

import { isImmersiveRoute } from './immersive-routes';

describe('isImmersiveRoute', () => {
  it('matches the session-live route (existing)', () => {
    expect(isImmersiveRoute('/sessions/abc/live')).toBe(true);
    expect(isImmersiveRoute('/sessions/abc/live/score')).toBe(true);
  });

  it('matches the library play route (existing)', () => {
    expect(isImmersiveRoute('/library/abc/play')).toBe(true);
  });

  it('matches the game-night live route (new)', () => {
    expect(isImmersiveRoute('/game-nights/abc/live')).toBe(true);
    expect(isImmersiveRoute('/game-nights/abc/live/anything')).toBe(true);
  });

  it('does not match non-immersive routes', () => {
    expect(isImmersiveRoute('/game-nights/abc')).toBe(false);
    expect(isImmersiveRoute('/game-nights/abc/summary')).toBe(false);
    expect(isImmersiveRoute('/sessions/abc')).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm exec vitest run src/components/layout/AppNav/immersive-routes.test.ts`
Expected: FAIL on the "game-night live route (new)" case (`false` !== `true`).

- [ ] **Step 3: Add the pattern**

In `apps/web/src/components/layout/AppNav/immersive-routes.ts`, add to `IMMERSIVE_ROUTE_PATTERNS`:

```ts
const IMMERSIVE_ROUTE_PATTERNS = [
  /^\/sessions\/[^/]+\/live(\/|$)/,
  /^\/library\/[^/]+\/play(\/|$)/,
  /^\/game-nights\/[^/]+\/live(\/|$)/,
];
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm exec vitest run src/components/layout/AppNav/immersive-routes.test.ts`
Expected: PASS (all cases).

- [ ] **Step 5: Lint + commit**

```bash
cd apps/web && pnpm exec eslint src/components/layout/AppNav/immersive-routes.ts src/components/layout/AppNav/immersive-routes.test.ts
```
Then (background commit):
```bash
git add apps/web/src/components/layout/AppNav/immersive-routes.ts apps/web/src/components/layout/AppNav/immersive-routes.test.ts
git commit -m "fix(game-night): #3150 add /game-nights/[id]/live to immersive routes"
```

---

### Task 2: Wire viewport → `mobile` prop in NightLiveClientView

Activate the hub's mobile layout below `lg`. Must mock `useResponsive` in the existing test file (default desktop) so existing desktop-assuming tests stay green, then add mobile-activation tests.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx`
- Modify: `apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx`

**Interfaces:**
- Consumes: `useResponsive(): ResponsiveState` from `@/hooks/useResponsive` (has `isDesktop: boolean`, SSR-safe: returns `isDesktop:false` until hydrated). `NightLiveHub` prop `mobile?: boolean` + `initialMobileTab?: 'current'|'planned'|'diary'`.
- Produces: no new exports.

- [ ] **Step 1: Add the `useResponsive` mock to the test file (default desktop) so existing tests keep asserting the desktop layout**

In `NightLiveClientView.test.tsx`, after the other `vi.mock` blocks (near line 94), add:

```ts
const responsiveState = vi.hoisted(() => ({ isDesktop: true }));
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    isDesktop: responsiveState.isDesktop,
    isMobile: !responsiveState.isDesktop,
    isTablet: false,
    deviceType: responsiveState.isDesktop ? 'desktop' : 'mobile',
    viewportWidth: responsiveState.isDesktop ? 1280 : 390,
  }),
}));
```

In the existing `beforeEach` (line ~150), add as the first line inside:

```ts
responsiveState.isDesktop = true;
```

- [ ] **Step 2: Write the failing mobile-activation tests**

Add a new `describe` block at the end of the file:

```ts
describe('NightLiveClientView — mobile activation', () => {
  it('renders the hub mobile tab bar below lg (isDesktop false)', () => {
    responsiveState.isDesktop = false;
    mockQuery({ data: vm() });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.getByRole('tablist', { name: 'Hub mobile tabs' })).toBeInTheDocument();
  });

  it('does NOT render the mobile tab bar at lg+ (isDesktop true)', () => {
    responsiveState.isDesktop = true;
    mockQuery({ data: vm() });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    expect(screen.queryByRole('tablist', { name: 'Hub mobile tabs' })).toBeNull();
  });
});
```

- [ ] **Step 3: Run tests to verify the new ones fail**

Run: `cd apps/web && pnpm exec vitest run "src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx"`
Expected: the "renders the hub mobile tab bar" case FAILS (tablist not found — `mobile` never passed). Existing tests still PASS (default desktop).

- [ ] **Step 4: Wire the viewport in the component**

In `NightLiveClientView.tsx`:

1. Add the import (with the other `@/hooks`/`@/lib` imports):

```ts
import { useResponsive } from '@/hooks/useResponsive';
```

2. Inside the component, near the top (after `const router = useRouter();`):

```ts
  // WS-A (#3150): NightLiveHub already ships a mobile layout (3-tab bottom nav) — activate it
  // below lg. useResponsive is SSR-safe (isDesktop=false until hydrated); the data-loading
  // skeleton masks the pre-hydration mobile→desktop settle so there is no visible flash.
  const { isDesktop } = useResponsive();
```

3. In the `return`, pass `mobile` to `NightLiveHub` (add the prop to the existing `<NightLiveHub ... />`):

```tsx
      <NightLiveHub
        readOnly
        mobile={!isDesktop}
        night={vm.night}
        status={vm.status}
        current={vm.current}
        total={vm.total}
        elapsed={vm.elapsed}
        confirmedPlayers={vm.confirmedPlayers}
        totalPlayers={vm.totalPlayers}
        plannedGames={vm.plannedGames}
        currentGame={vm.currentGame}
        diaryEvents={diary.diaryEvents}
        diaryGames={diary.diaryGames}
        diaryPlayers={diary.diaryPlayers}
        onBack={handleBack}
        onJumpToSession={handleJumpToSession}
      />
```

- [ ] **Step 5: Run tests to verify all pass**

Run: `cd apps/web && pnpm exec vitest run "src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx"`
Expected: PASS (existing desktop tests + both new mobile-activation tests).

- [ ] **Step 6: Lint + commit**

```bash
cd apps/web && pnpm exec eslint "src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx" "src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx"
```
Then (background commit):
```bash
git add "apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx" "apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx"
git commit -m "fix(game-night): #3150 activate NightLiveHub mobile layout below lg"
```

---

### Task 3: Offset organizer CTAs above the mobile tab bar + safe-area

The 3 organizer CTA bars (`showStartCta`, `showCompleteCta`, `showFinalizeCta`) are `fixed inset-x-0 bottom-0`. Below `lg` the hub renders an in-flow 3-tab bar at the bottom, so the fixed CTAs overlap it. Offset the CTAs above the tab bar (mobile) + safe-area; reset to `bottom-0` at `lg` (no tab bar on desktop).

**Files:**
- Modify: `apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx`
- Modify: `apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx`

**Interfaces:**
- Consumes: nothing new.
- Produces: module-const `CTA_BAR_POSITION` (internal to the file, not exported).

- [ ] **Step 1: Write the failing test**

Add to the `describe('NightLiveClientView — mobile activation')` block:

```ts
  it('offsets the organizer CTA above the mobile tab bar (resets at lg)', () => {
    responsiveState.isDesktop = false;
    mockQuery({ data: vm({ isViewerOrganizer: true, status: 'transition', nextGame: NEXT_GAME }) });
    render(<NightLiveClientView nightId={NIGHT_ID} />);
    const cta = screen.getByRole('button', { name: /Avvia: Catan/ });
    const bar = cta.closest('div');
    expect(bar).not.toBeNull();
    // desktop reset present → the responsive offset class is applied
    expect(bar?.className).toContain('lg:bottom-0');
    // and it is NOT the old unconditional bottom-0 (which had no lg: reset marker meaning)
    expect(bar?.className).toContain('safe-area-inset-bottom');
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm exec vitest run "src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx" -t "offsets the organizer CTA"`
Expected: FAIL (current className is `fixed inset-x-0 bottom-0 z-40 flex justify-center p-4` — no `lg:bottom-0`, no `safe-area-inset-bottom`).

- [ ] **Step 3: Introduce the shared CTA position class and apply it**

In `NightLiveClientView.tsx`, add a module const above the component (after the imports):

```ts
// WS-A (#3150): below lg the hub renders an in-flow 3-tab bar (~3.5rem) at the bottom; lift the
// fixed organizer CTAs above it + iOS safe-area. At lg there is no tab bar → reset to bottom-0.
const CTA_BAR_POSITION =
  'fixed inset-x-0 z-40 p-4 bottom-[calc(3.5rem_+_env(safe-area-inset-bottom))] lg:bottom-0';
```

Then replace the three CTA container `className`s:

- `showStartCta` container:
```tsx
        <div className={`${CTA_BAR_POSITION} flex justify-center`}>
```
- `showCompleteCta` container:
```tsx
        <div className={`${CTA_BAR_POSITION} flex justify-center`}>
```
- `showFinalizeCta` container:
```tsx
        <div className={`${CTA_BAR_POSITION} flex flex-col items-center gap-2`}>
```

(Remove the old `fixed inset-x-0 bottom-0 z-40 ... p-4` fragments from each — the shared const now carries `fixed inset-x-0 z-40 p-4` + the responsive bottom.)

- [ ] **Step 4: Run tests to verify all pass**

Run: `cd apps/web && pnpm exec vitest run "src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx"`
Expected: PASS (all, including the new CTA-offset test).

- [ ] **Step 5: Lint + commit**

```bash
cd apps/web && pnpm exec eslint "src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx"
```
Then (background commit):
```bash
git add "apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx" "apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/__tests__/NightLiveClientView.test.tsx"
git commit -m "fix(game-night): #3150 offset organizer CTAs above mobile tab bar + safe-area"
```

---

## Manual / visual verification (post-implementation, DoD)

jsdom cannot verify layout. After the 3 tasks, verify visually (dev server or Playwright) at 390px and 1280px on `/game-nights/{id}/live` (Published night, organizer):
- < lg: hub shows the 3-tab bottom bar; global `MobileBottomBar` is hidden; organizer CTA sits above the tab bar, not overlapping; safe-area respected.
- >= lg: unchanged 3-col desktop hub; CTA at `bottom-0`.

## Self-Review

- **Spec coverage:** A1 (Task 2) · A2 (Task 1) · A3 (Task 3) · A4 (tests folded into each task). WS-B and deferred flavor UX are out of this plan's scope by design.
- **Placeholders:** none — every step has concrete code/commands.
- **Type consistency:** `useResponsive` returns `ResponsiveState` (`isDesktop`); `NightLiveHub` `mobile?: boolean`; `CTA_BAR_POSITION` string const used by all 3 CTA containers. Consistent.
- **Regression guard:** Task 2 Step 1 mocks `useResponsive` default-desktop so existing desktop tests stay green; mobile tests flip the flag explicitly.
