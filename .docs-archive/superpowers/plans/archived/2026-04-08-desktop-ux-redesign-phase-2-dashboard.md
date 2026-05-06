# Desktop UX Redesign — Phase 2: Dashboard Gaming Hub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the Dashboard Gaming Hub with the new MeepleCard design language (greeting + hero LiveSession + KPI strip + "Continua a giocare" carousel + recent chats + friends), gated by the Phase 1 `NEXT_PUBLIC_UX_REDESIGN` flag. Also fixes the 4 carry-forward items from Phase 1 code review (M1/M3/M4/M6).

**Architecture:** The existing `dashboard-client.tsx` (Bento grid) is kept intact as the legacy path. A new `DashboardClientV2` component is mounted when the flag is on (internal branch inside the existing `DashboardClient`). The v2 layout reuses the existing data layer (`useDashboardStore`, `useAuth`, `StatsRow`, `WelcomeHero`) but replaces the Bento grid with vertical sections composed of `MeepleCard` variants (GridCard for games, HeroCard for live session, CompactCard for friends, StatCard for KPI).

**Tech Stack:** Next.js 16, React 19, TypeScript, Tailwind 4, Zustand, Vitest + Testing Library. All design tokens reused from `apps/web/src/styles/design-tokens.css`. No new tokens.

**Spec reference:** `docs/superpowers/specs/2026-04-08-desktop-ux-redesign-design.md` §5.1 (Dashboard)

**Prior phase:** Phase 1 (shell foundation) — see PR #296.

---

## Prerequisite: Phase 1 carry-forward fixes

Before touching the dashboard, we fix 4 items flagged during the Phase 1 code review. These are bundled into the first 3 tasks of this plan so that Phase 2 pages consuming the hook can do so safely.

## File Structure

### New files

| Path | Responsibility |
|---|---|
| `apps/web/src/app/(authenticated)/dashboard/v2/DashboardClientV2.tsx` | New Phase 2 dashboard layout: greeting + hero + KPI + carousel + chat + friends |
| `apps/web/src/app/(authenticated)/dashboard/v2/sections/GreetingRow.tsx` | "Ciao {name}" row with gradient wordmark + quick stats |
| `apps/web/src/app/(authenticated)/dashboard/v2/sections/HeroLiveSession.tsx` | Full-width HeroCard variant showing active session (replaced by "Start a game" card if no active session) |
| `apps/web/src/app/(authenticated)/dashboard/v2/sections/KpiStrip.tsx` | 4-column StatCard strip (games / sessions / friends / chats) |
| `apps/web/src/app/(authenticated)/dashboard/v2/sections/ContinueCarousel.tsx` | Horizontal scroll carousel of "continue playing" games as GridCard |
| `apps/web/src/app/(authenticated)/dashboard/v2/sections/ChatRecentCards.tsx` | 3 compact chat preview cards with ConfidenceBadge |
| `apps/web/src/app/(authenticated)/dashboard/v2/sections/FriendsRow.tsx` | 4 CompactCard variants for active friends with online dot |
| `apps/web/src/app/(authenticated)/dashboard/v2/index.ts` | Barrel export |
| `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/GreetingRow.test.tsx` | unit test |
| `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/HeroLiveSession.test.tsx` | unit test |
| `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/KpiStrip.test.tsx` | unit test |
| `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/ContinueCarousel.test.tsx` | unit test |
| `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/ChatRecentCards.test.tsx` | unit test |
| `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/FriendsRow.test.tsx` | unit test |
| `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/DashboardClientV2.test.tsx` | integration test |

### Modified files

| Path | Change |
|---|---|
| `apps/web/src/hooks/useMiniNavConfig.ts` | **M3 fix** — stabilize config reference with shallow equality (no infinite re-registration on inline objects) |
| `apps/web/src/lib/stores/mini-nav-config-store.ts` | **M4 fix** — tighten `MiniNavPrimaryAction.icon` type to `string` emoji with JSDoc clarification |
| `apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx` | **M6 fix** — add TODO comment on `isExpanded` explaining Phase 2 width wiring; wire expand transition with `w-[var(--card-rack-hover-width)]` |
| `apps/web/src/components/layout/UserShell/v2/TopBarLogo.tsx` | **M1 fix** — `font-[var(--font-quicksand)]` → `font-quicksand` |
| `apps/web/src/components/layout/UserShell/v2/TopBarNavLinks.tsx` | **M1 fix** — font utility cleanup |
| `apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx` | **M1 fix** — font utility cleanup (in the same commit as M6) |
| `apps/web/src/components/layout/UserShell/v2/HandRailItem.tsx` | **M1 fix** — font utility cleanup |
| `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx` | Internal flag branch: `isUxRedesignEnabled() ? <DashboardClientV2 /> : <legacy bento grid />` |

### Not touched in Phase 2

- `apps/web/src/app/(authenticated)/dashboard/dashboard-mobile.tsx` — mobile path unchanged
- Existing widgets under `dashboard/widgets/` — kept for legacy path, not modified
- `useDashboardStore` store and API — reused as-is

---

## Task 1: M1 — Font utility cleanup in v2 shell files

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/v2/TopBarLogo.tsx`
- Modify: `apps/web/src/components/layout/UserShell/v2/TopBarNavLinks.tsx`
- Modify: `apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx`
- Modify: `apps/web/src/components/layout/UserShell/v2/HandRailItem.tsx`

- [ ] **Step 1: Find all occurrences**

Run: `grep -rn "font-\[var(--font-" apps/web/src/components/layout/UserShell/v2/`
Expected occurrences: 4 files, ~6-8 lines total.

- [ ] **Step 2: Replace arbitrary-value syntax with Tailwind utilities**

In each file, replace:
- `font-[var(--font-quicksand)]` → `font-quicksand`
- `font-[var(--font-nunito)]` → `font-nunito`

These utilities are already registered in `apps/web/tailwind.config.js`.

- [ ] **Step 3: Run existing tests to confirm no regression**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2`
Expected: all existing v2 tests pass (no behavior change, pure style refactor).

- [ ] **Step 4: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-dev
git add apps/web/src/components/layout/UserShell/v2/TopBarLogo.tsx apps/web/src/components/layout/UserShell/v2/TopBarNavLinks.tsx apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx apps/web/src/components/layout/UserShell/v2/HandRailItem.tsx
git commit -m "refactor(web): use font-quicksand/nunito utilities (M1 from phase 1 review)"
```

---

## Task 2: M3 — Stabilize useMiniNavConfig with shallow equality

**Files:**
- Modify: `apps/web/src/hooks/useMiniNavConfig.ts`
- Modify: `apps/web/src/hooks/__tests__/useMiniNavConfig.test.tsx` (add regression test)

- [ ] **Step 1: Write failing regression test**

Add a new test to `apps/web/src/hooks/__tests__/useMiniNavConfig.test.tsx` at the end of the describe block, BEFORE modifying the hook:

```typescript
  it('does not re-trigger setConfig when rerendered with equivalent inline config', () => {
    const setConfigSpy = vi.spyOn(useMiniNavConfigStore.getState(), 'setConfig');

    const { rerender } = renderHook(() =>
      useMiniNavConfig({
        breadcrumb: 'Home',
        tabs: [{ id: 'a', label: 'A', href: '/' }],
        activeTabId: 'a',
      })
    );

    const initialCallCount = setConfigSpy.mock.calls.length;
    // Simulate a page re-render with a new inline object that has equivalent content
    rerender();
    const finalCallCount = setConfigSpy.mock.calls.length;

    // setConfig should NOT be called on the equivalent rerender
    expect(finalCallCount).toBe(initialCallCount);
    setConfigSpy.mockRestore();
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useMiniNavConfig.test.tsx`
Expected: the new test FAILS — `setConfig` is called twice (initial mount + rerender).

- [ ] **Step 3: Fix the hook with shallow-compare stability**

Replace the contents of `apps/web/src/hooks/useMiniNavConfig.ts` with:

```typescript
'use client';

import { useEffect, useRef } from 'react';

import { useMiniNavConfigStore, type MiniNavConfig } from '@/lib/stores/mini-nav-config-store';

/**
 * Pages call this hook to register their mini-nav config with the global shell.
 * The shell reads the store and renders MiniNavSlot automatically.
 * Config is cleared on unmount (so navigating away hides the mini-nav).
 *
 * Shallow equality check on the config primitive fields prevents
 * re-registration loops when consumers pass inline object literals.
 * (Carry-forward fix M3 from Phase 1 code review.)
 */
export function useMiniNavConfig(config: MiniNavConfig): void {
  const setConfig = useMiniNavConfigStore(s => s.setConfig);
  const clear = useMiniNavConfigStore(s => s.clear);
  const previousKeyRef = useRef<string | null>(null);

  // Build a stable structural key from the config's observable fields.
  // Tabs and primaryAction are part of the identity; onClick is excluded
  // because function references change between renders but rarely carry
  // meaning independent of the breadcrumb/activeTabId.
  const key = JSON.stringify({
    breadcrumb: config.breadcrumb,
    activeTabId: config.activeTabId,
    tabs: config.tabs,
    primaryActionLabel: config.primaryAction?.label ?? null,
    primaryActionIcon: config.primaryAction?.icon ?? null,
  });

  useEffect(() => {
    if (previousKeyRef.current === key) return; // shallow equality: skip redundant updates
    previousKeyRef.current = key;
    setConfig(config);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, setConfig]);

  // Separate effect for unmount cleanup — runs once, clears on teardown.
  useEffect(() => {
    return () => {
      clear();
      previousKeyRef.current = null;
    };
  }, [clear]);
}
```

- [ ] **Step 4: Run tests to verify all pass**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useMiniNavConfig.test.tsx`
Expected: all 4 tests pass (3 original + 1 new regression test).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useMiniNavConfig.ts apps/web/src/hooks/__tests__/useMiniNavConfig.test.tsx
git commit -m "fix(web): stabilize useMiniNavConfig with shallow equality (M3 from phase 1 review)"
```

---

## Task 3: M4 — Tighten MiniNavPrimaryAction.icon type + M6 hand rail expand wiring

**Files:**
- Modify: `apps/web/src/lib/stores/mini-nav-config-store.ts`
- Modify: `apps/web/src/components/layout/UserShell/v2/MiniNavSlot.tsx`
- Modify: `apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx`

- [ ] **Step 1: Update the store type**

Edit `apps/web/src/lib/stores/mini-nav-config-store.ts` — update the `MiniNavPrimaryAction` interface:

```typescript
export interface MiniNavPrimaryAction {
  label: string;
  onClick: () => void;
  /**
   * Optional emoji or icon character prepended to the label.
   * Keep it short — single emoji or ASCII symbol (e.g. '＋', '🎲', '▶').
   * For complex icons, render them in the label text itself.
   */
  icon?: string;
}
```

- [ ] **Step 2: Render the icon in MiniNavSlot**

Edit `apps/web/src/components/layout/UserShell/v2/MiniNavSlot.tsx` — update the primary action button to render the icon:

```typescript
{config.primaryAction && (
  <button
    type="button"
    onClick={config.primaryAction.onClick}
    className="px-3.5 py-2 rounded-[10px] text-[0.78rem] font-bold text-white border-none cursor-pointer flex items-center gap-1.5 transition-all hover:-translate-y-px"
    style={{
      background: 'linear-gradient(135deg, hsl(25 95% 48%), hsl(25 95% 40%))',
      boxShadow: '0 2px 6px hsla(25, 95%, 45%, 0.3)',
    }}
  >
    {config.primaryAction.icon && <span aria-hidden>{config.primaryAction.icon}</span>}
    {config.primaryAction.label}
  </button>
)}
```

- [ ] **Step 3: Wire isExpanded in DesktopHandRail**

Edit `apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx` — replace the hardcoded `w-[76px]` with a conditional and add a TODO-removal note:

Find:
```tsx
<aside
  data-testid="desktop-hand-rail"
  aria-label="Cards in hand"
  className="hidden md:flex flex-col w-[76px] shrink-0 border-r border-[var(--nh-border-default)] py-3.5 pb-3 gap-2"
```

Replace with:
```tsx
<aside
  data-testid="desktop-hand-rail"
  aria-label="Cards in hand"
  data-expanded={isExpanded}
  className={cn(
    'hidden md:flex flex-col shrink-0 border-r border-[var(--nh-border-default)] py-3.5 pb-3 gap-2 transition-[width] duration-200 ease-out',
    isExpanded ? 'w-[220px]' : 'w-[76px]'
  )}
```

Add `import { cn } from '@/lib/utils';` to the imports if not already present.

- [ ] **Step 4: Update existing DesktopHandRail test**

Edit `apps/web/src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx` — keep the `w-[76px]` default test but relax it to accept either class OR add a new test for expanded state. Replace the existing "is 76px wide by default" test with:

```typescript
  it('is 76px wide when collapsed (default)', () => {
    const { container } = render(<DesktopHandRail />);
    const rail = container.querySelector('[data-testid="desktop-hand-rail"]');
    expect(rail).toHaveClass('w-[76px]');
    expect(rail).toHaveAttribute('data-expanded', 'false');
  });
```

Add a new test after it:

```typescript
  it('expands to 220px when toggle is clicked', async () => {
    const user = userEvent.setup();
    const { container } = render(<DesktopHandRail />);
    const expandBtn = screen.getByRole('button', { name: /expand/i });
    await user.click(expandBtn);
    const rail = container.querySelector('[data-testid="desktop-hand-rail"]');
    expect(rail).toHaveClass('w-[220px]');
    expect(rail).toHaveAttribute('data-expanded', 'true');
  });
```

Add imports if missing:
```typescript
import userEvent from '@testing-library/user-event';
import { screen } from '@testing-library/react';
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx src/components/layout/UserShell/v2/__tests__/MiniNavSlot.test.tsx`
Expected: all tests pass.

- [ ] **Step 6: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/stores/mini-nav-config-store.ts apps/web/src/components/layout/UserShell/v2/MiniNavSlot.tsx apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx apps/web/src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx
git commit -m "feat(web): wire hand rail expand + clarify primary action icon (M4, M6 from phase 1 review)"
```

---

## Task 4: GreetingRow section

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/v2/sections/GreetingRow.tsx`
- Test: `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/GreetingRow.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/GreetingRow.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { GreetingRow } from '../sections/GreetingRow';

describe('GreetingRow', () => {
  it('renders the greeting with user display name', () => {
    render(
      <GreetingRow
        displayName="Marco"
        subtitle="Hai una partita in corso"
        stats={[
          { label: 'Partite mese', value: '24' },
          { label: 'Win rate', value: '68%' },
          { label: 'Tempo gioco', value: '42h' },
        ]}
      />
    );
    expect(screen.getByText(/Ciao/)).toBeInTheDocument();
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('Hai una partita in corso')).toBeInTheDocument();
  });

  it('renders all provided quick stats', () => {
    render(
      <GreetingRow
        displayName="Anna"
        subtitle="subtitle"
        stats={[
          { label: 'Partite mese', value: '24' },
          { label: 'Win rate', value: '68%' },
        ]}
      />
    );
    expect(screen.getByText('24')).toBeInTheDocument();
    expect(screen.getByText('Partite mese')).toBeInTheDocument();
    expect(screen.getByText('68%')).toBeInTheDocument();
    expect(screen.getByText('Win rate')).toBeInTheDocument();
  });

  it('omits the stats cluster when no stats provided', () => {
    const { container } = render(
      <GreetingRow displayName="X" subtitle="Y" stats={[]} />
    );
    expect(container.querySelector('[data-testid="greet-stats"]')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/GreetingRow.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement GreetingRow**

Create `apps/web/src/app/(authenticated)/dashboard/v2/sections/GreetingRow.tsx`:

```typescript
'use client';

export interface GreetingStat {
  label: string;
  value: string;
}

interface GreetingRowProps {
  displayName: string;
  subtitle: string;
  stats: GreetingStat[];
}

/**
 * Phase 2 dashboard greeting row: "Ciao {name}" with gradient wordmark on the name,
 * subtitle underneath, and a right-aligned cluster of quick stats.
 */
export function GreetingRow({ displayName, subtitle, stats }: GreetingRowProps) {
  return (
    <div className="mb-6 flex items-end justify-between gap-6">
      <div>
        <h1 className="font-quicksand text-[1.8rem] font-extrabold leading-tight text-[var(--nh-text-primary)]">
          Ciao{' '}
          <span
            className="bg-clip-text text-transparent"
            style={{
              backgroundImage:
                'linear-gradient(135deg, hsl(25 95% 48%), hsl(38 92% 55%))',
            }}
          >
            {displayName}
          </span>{' '}
          <span aria-hidden>👋</span>
        </h1>
        <p className="mt-1 text-sm text-[var(--nh-text-muted)]">{subtitle}</p>
      </div>
      {stats.length > 0 && (
        <div data-testid="greet-stats" className="flex gap-5">
          {stats.map(stat => (
            <div key={stat.label} className="text-right">
              <div className="font-quicksand text-[1.35rem] font-extrabold leading-none text-[var(--nh-text-primary)]">
                {stat.value}
              </div>
              <div className="mt-1 text-[0.68rem] font-bold uppercase tracking-wider text-[var(--nh-text-muted)]">
                {stat.label}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/GreetingRow.test.tsx`
Expected: PASS — 3 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/v2/sections/GreetingRow.tsx apps/web/src/app/\(authenticated\)/dashboard/v2/__tests__/GreetingRow.test.tsx
git commit -m "feat(web): add Phase 2 dashboard GreetingRow section"
```

---

## Task 5: KpiStrip section (using existing StatCard showcase component)

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/v2/sections/KpiStrip.tsx`
- Test: `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/KpiStrip.test.tsx`

- [ ] **Step 1: Verify StatCard component location**

Run: `find apps/web/src/components -name "stat-card*" -o -name "StatCard*"`
Expected: find the existing StatCard component path (likely `apps/web/src/components/ui/data-display/stat-card.tsx` or similar). If found, import from there. If not found, STOP and report NEEDS_CONTEXT.

- [ ] **Step 2: Write the failing test**

Create `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/KpiStrip.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { KpiStrip } from '../sections/KpiStrip';

describe('KpiStrip', () => {
  const kpis = {
    games: 47,
    sessions: 128,
    friends: 8,
    chats: 36,
  };

  it('renders all 4 KPI values', () => {
    render(<KpiStrip kpis={kpis} />);
    expect(screen.getByText('47')).toBeInTheDocument();
    expect(screen.getByText('128')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByText('36')).toBeInTheDocument();
  });

  it('renders all 4 KPI labels', () => {
    render(<KpiStrip kpis={kpis} />);
    expect(screen.getByText(/libreria/i)).toBeInTheDocument();
    expect(screen.getByText(/sessioni/i)).toBeInTheDocument();
    expect(screen.getByText(/amici/i)).toBeInTheDocument();
    expect(screen.getByText(/chat/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/KpiStrip.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 4: Implement KpiStrip**

Create `apps/web/src/app/(authenticated)/dashboard/v2/sections/KpiStrip.tsx`:

```typescript
'use client';

export interface KpiValues {
  games: number;
  sessions: number;
  friends: number;
  chats: number;
}

interface KpiStripProps {
  kpis: KpiValues;
}

interface KpiCardData {
  key: keyof KpiValues;
  label: string;
  icon: string;
  entity: 'game' | 'session' | 'player' | 'chat';
}

const CARDS: KpiCardData[] = [
  { key: 'games', label: 'Giochi in libreria', icon: '🎲', entity: 'game' },
  { key: 'sessions', label: 'Sessioni totali', icon: '🎯', entity: 'session' },
  { key: 'friends', label: 'Amici preferiti', icon: '👥', entity: 'player' },
  { key: 'chats', label: 'Chat con agente', icon: '💬', entity: 'chat' },
];

const ENTITY_BG: Record<KpiCardData['entity'], string> = {
  game: 'hsla(25, 95%, 45%, 0.12)',
  session: 'hsla(240, 60%, 55%, 0.12)',
  player: 'hsla(262, 83%, 58%, 0.12)',
  chat: 'hsla(220, 80%, 55%, 0.12)',
};
const ENTITY_FG: Record<KpiCardData['entity'], string> = {
  game: 'hsl(25 95% 38%)',
  session: 'hsl(240 60% 45%)',
  player: 'hsl(262 83% 50%)',
  chat: 'hsl(220 80% 45%)',
};

export function KpiStrip({ kpis }: KpiStripProps) {
  return (
    <div className="mb-7 grid grid-cols-4 gap-3.5">
      {CARDS.map(card => (
        <div
          key={card.key}
          className="relative overflow-hidden rounded-2xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] p-5 shadow-[var(--shadow-warm-sm)] transition-all duration-300 hover:-translate-y-0.5 hover:shadow-[var(--shadow-warm-md)]"
        >
          <div
            className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl text-lg"
            style={{ background: ENTITY_BG[card.entity], color: ENTITY_FG[card.entity] }}
            aria-hidden
          >
            {card.icon}
          </div>
          <div className="text-[0.7rem] font-bold uppercase tracking-wider text-[var(--nh-text-muted)]">
            {card.label}
          </div>
          <div className="mt-1 font-quicksand text-2xl font-extrabold leading-none text-[var(--nh-text-primary)]">
            {kpis[card.key]}
          </div>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/KpiStrip.test.tsx`
Expected: PASS — 2 tests passing.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/v2/sections/KpiStrip.tsx apps/web/src/app/\(authenticated\)/dashboard/v2/__tests__/KpiStrip.test.tsx
git commit -m "feat(web): add Phase 2 dashboard KpiStrip section"
```

---

## Task 6: HeroLiveSession section

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/v2/sections/HeroLiveSession.tsx`
- Test: `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/HeroLiveSession.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/HeroLiveSession.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { HeroLiveSession } from '../sections/HeroLiveSession';

describe('HeroLiveSession', () => {
  const baseSession = {
    id: 'ses-1',
    gameName: 'Azul',
    locationName: 'Casa di Marco',
    playerCount: 3,
    roundCurrent: 4,
    roundTotal: 6,
    startedMinutesAgo: 38,
  };

  it('renders the session title and meta when a session is active', () => {
    render(<HeroLiveSession session={baseSession} onContinue={() => {}} />);
    expect(screen.getByText(/Serata Azul/)).toBeInTheDocument();
    expect(screen.getByText(/Casa di Marco/)).toBeInTheDocument();
    expect(screen.getByText(/In corso/i)).toBeInTheDocument();
  });

  it('calls onContinue when the primary action is clicked', async () => {
    const onContinue = vi.fn();
    const user = userEvent.setup();
    render(<HeroLiveSession session={baseSession} onContinue={onContinue} />);
    await user.click(screen.getByRole('button', { name: /Continua/i }));
    expect(onContinue).toHaveBeenCalledOnce();
  });

  it('renders the empty state when no active session', () => {
    render(<HeroLiveSession session={null} onContinue={() => {}} />);
    expect(screen.getByText(/Nessuna partita/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Inizia nuova partita/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/HeroLiveSession.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement HeroLiveSession**

Create `apps/web/src/app/(authenticated)/dashboard/v2/sections/HeroLiveSession.tsx`:

```typescript
'use client';

export interface LiveSessionPreview {
  id: string;
  gameName: string;
  locationName: string;
  playerCount: number;
  roundCurrent: number;
  roundTotal: number;
  startedMinutesAgo: number;
}

interface HeroLiveSessionProps {
  session: LiveSessionPreview | null;
  onContinue: () => void;
}

export function HeroLiveSession({ session, onContinue }: HeroLiveSessionProps) {
  if (!session) {
    return (
      <div
        className="mb-6 flex min-h-[180px] items-center justify-between gap-6 overflow-hidden rounded-3xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] p-8 shadow-[var(--shadow-warm-sm)]"
      >
        <div>
          <h2 className="font-quicksand text-xl font-extrabold text-[var(--nh-text-primary)]">
            Nessuna partita in corso
          </h2>
          <p className="mt-1 text-sm text-[var(--nh-text-muted)]">
            Pronto per una nuova serata di gioco?
          </p>
        </div>
        <button
          type="button"
          onClick={onContinue}
          className="rounded-xl px-5 py-3 font-nunito text-sm font-bold text-white transition-all hover:-translate-y-0.5"
          style={{
            background: 'linear-gradient(135deg, hsl(25 95% 48%), hsl(25 95% 40%))',
            boxShadow: '0 2px 8px hsla(25, 95%, 45%, 0.35)',
          }}
        >
          ▶ Inizia nuova partita
        </button>
      </div>
    );
  }

  return (
    <div
      className="group relative mb-6 flex min-h-[220px] cursor-pointer gap-0 overflow-hidden rounded-3xl border border-[var(--nh-border-default)] shadow-[var(--shadow-warm-md)] transition-all duration-300 hover:-translate-y-1 hover:shadow-[var(--shadow-warm-xl)]"
      style={{
        background:
          'linear-gradient(135deg, hsla(240,60%,55%,0.12), hsla(262,83%,58%,0.08)), var(--nh-bg-elevated)',
      }}
      onClick={onContinue}
      role="button"
      tabIndex={0}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onContinue();
        }
      }}
    >
      <div
        aria-hidden
        className="absolute left-0 top-0 bottom-0 w-[5px] z-10"
        style={{ background: 'hsl(240 60% 55%)' }}
      />
      <div
        className="relative hidden md:flex w-[300px] shrink-0 items-center justify-center text-[80px] overflow-hidden"
        style={{
          background: 'linear-gradient(135deg, hsl(240 55% 65%), hsl(262 80% 55%))',
        }}
        aria-hidden
      >
        🎯
      </div>
      <div className="relative z-[2] flex flex-1 flex-col justify-between p-7 md:p-8">
        <div>
          <span
            className="mb-2.5 inline-flex w-fit items-center gap-1.5 rounded-md px-2.5 py-1 font-quicksand text-[10px] font-extrabold uppercase tracking-wider text-white"
            style={{ background: 'hsl(240 60% 55%)' }}
          >
            🎯 Session
          </span>
          <h2 className="mb-1.5 font-quicksand text-[1.75rem] font-extrabold leading-tight text-[var(--nh-text-primary)]">
            Serata {session.gameName} · {session.locationName}
          </h2>
          <p className="mb-3.5 text-[0.92rem] text-[var(--nh-text-secondary)]">
            {session.playerCount} giocatori · round {session.roundCurrent} di {session.roundTotal} · iniziata {session.startedMinutesAgo} min fa
          </p>
        </div>
        <div className="flex gap-2.5">
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              onContinue();
            }}
            className="rounded-xl px-5 py-2.5 font-nunito text-[0.82rem] font-bold text-white transition-all hover:-translate-y-0.5"
            style={{
              background: 'linear-gradient(135deg, hsl(240 60% 55%), hsl(240 60% 42%))',
              boxShadow: '0 2px 8px hsla(240, 60%, 55%, 0.35)',
            }}
          >
            ▶ Continua partita
          </button>
        </div>
      </div>
      <div
        className="absolute right-7 top-6 flex items-center gap-1.5 rounded-full border px-2.5 py-1 font-nunito text-[0.7rem] font-extrabold uppercase tracking-wider"
        style={{
          background: 'hsla(140, 60%, 45%, 0.12)',
          borderColor: 'hsla(140, 60%, 45%, 0.25)',
          color: 'hsl(140 60% 30%)',
        }}
      >
        <span className="inline-block h-1.5 w-1.5 animate-pulse rounded-full" style={{ background: 'hsl(140 60% 45%)' }} />
        In corso
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/HeroLiveSession.test.tsx`
Expected: PASS — 3 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/v2/sections/HeroLiveSession.tsx apps/web/src/app/\(authenticated\)/dashboard/v2/__tests__/HeroLiveSession.test.tsx
git commit -m "feat(web): add Phase 2 dashboard HeroLiveSession section"
```

---

## Task 7: ContinueCarousel section

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/v2/sections/ContinueCarousel.tsx`
- Test: `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/ContinueCarousel.test.tsx`

- [ ] **Step 1: Check MeepleCard export location**

Run: `find apps/web/src/components/ui/data-display/meeple-card -name "index.ts" -maxdepth 2`
Confirm the public `MeepleCard` export exists, plus `GridCard` variant. The barrel is at `apps/web/src/components/ui/data-display/meeple-card/index.ts`. Verify by running `cat apps/web/src/components/ui/data-display/meeple-card/index.ts`.

- [ ] **Step 2: Write the failing test**

Create `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/ContinueCarousel.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ContinueCarousel } from '../sections/ContinueCarousel';

describe('ContinueCarousel', () => {
  const games = [
    {
      id: 'g1',
      title: 'Azul',
      subtitle: 'Plan B Games',
      imageUrl: 'https://example.com/azul.jpg',
      rating: 7.8,
      players: '2–4',
      duration: '45m',
    },
    {
      id: 'g2',
      title: 'Wingspan',
      subtitle: 'Stonemaier',
      rating: 8.1,
      players: '1–5',
      duration: '70m',
    },
  ];

  it('renders the section header and see-all link', () => {
    render(<ContinueCarousel games={games} />);
    expect(screen.getByText(/Continua a giocare/i)).toBeInTheDocument();
    expect(screen.getByText(/Vedi tutto/i)).toBeInTheDocument();
  });

  it('renders all provided games as cards', () => {
    render(<ContinueCarousel games={games} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders nothing (empty message) when no games', () => {
    render(<ContinueCarousel games={[]} />);
    expect(screen.queryByText(/Continua a giocare/i)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/ContinueCarousel.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 4: Implement ContinueCarousel**

Create `apps/web/src/app/(authenticated)/dashboard/v2/sections/ContinueCarousel.tsx`:

```typescript
'use client';

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

export interface ContinueCarouselGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  players?: string;
  duration?: string;
}

interface ContinueCarouselProps {
  games: ContinueCarouselGame[];
}

export function ContinueCarousel({ games }: ContinueCarouselProps) {
  if (games.length === 0) return null;

  return (
    <section className="mb-7">
      <div className="mb-3.5 flex items-center justify-between">
        <h3 className="flex items-center gap-3 font-quicksand text-[1.1rem] font-extrabold">
          <span
            aria-hidden
            className="inline-block h-[18px] w-1 rounded-sm"
            style={{ background: 'hsl(25 95% 45%)' }}
          />
          Continua a giocare
        </h3>
        <Link
          href="/library"
          className="rounded-lg px-3 py-1.5 text-[0.78rem] font-bold text-[hsl(25_95%_40%)] transition-colors hover:bg-[hsla(25,95%,45%,0.08)]"
        >
          Vedi tutto →
        </Link>
      </div>
      <div className="grid grid-cols-5 gap-4">
        {games.slice(0, 5).map(game => (
          <MeepleCard
            key={game.id}
            variant="grid"
            entity="game"
            title={game.title}
            subtitle={game.subtitle}
            imageUrl={game.imageUrl}
            rating={game.rating}
            metadata={[
              ...(game.players ? [{ icon: '👥' as const, label: game.players }] : []),
              ...(game.duration ? [{ icon: '⏱' as const, label: game.duration }] : []),
            ]}
          />
        ))}
      </div>
    </section>
  );
}
```

**Note on MeepleCard props:** The real `MeepleCard` component's `metadata` prop shape may differ. If the test fails because of mismatched types, consult `apps/web/src/components/ui/data-display/meeple-card/types.ts` and align the `metadata` entry shape. Fall back to omitting `metadata` and rendering chips inline if the type is incompatible.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/ContinueCarousel.test.tsx`
Expected: PASS — 3 tests passing.

If the test FAILS due to `MeepleCard` prop mismatch, STOP and report NEEDS_CONTEXT with the actual `MeepleCardProps` type.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/v2/sections/ContinueCarousel.tsx apps/web/src/app/\(authenticated\)/dashboard/v2/__tests__/ContinueCarousel.test.tsx
git commit -m "feat(web): add Phase 2 dashboard ContinueCarousel section"
```

---

## Task 8: ChatRecentCards section

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/v2/sections/ChatRecentCards.tsx`
- Test: `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/ChatRecentCards.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/ChatRecentCards.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatRecentCards } from '../sections/ChatRecentCards';

describe('ChatRecentCards', () => {
  const chats = [
    {
      id: 'c1',
      gameName: 'Azul',
      topic: 'Regola turno finale',
      snippet: 'Il turno finale inizia quando un giocatore completa una riga orizzontale.',
      confidence: 0.94,
      timestamp: 'Oggi, 14:32',
    },
    {
      id: 'c2',
      gameName: 'Wingspan',
      topic: 'Carte bonus a fine partita',
      snippet: 'Le carte bonus vengono rivelate solo alla fine della partita.',
      confidence: 0.98,
      timestamp: 'Ieri, 21:15',
    },
  ];

  it('renders the section header', () => {
    render(<ChatRecentCards chats={chats} />);
    expect(screen.getByText(/Chat recenti/i)).toBeInTheDocument();
  });

  it('renders all chat cards with titles and snippets', () => {
    render(<ChatRecentCards chats={chats} />);
    expect(screen.getByText('Azul · Regola turno finale')).toBeInTheDocument();
    expect(screen.getByText('Wingspan · Carte bonus a fine partita')).toBeInTheDocument();
    expect(screen.getAllByText(/accurata/i)).toHaveLength(2);
  });

  it('renders nothing when no chats', () => {
    const { container } = render(<ChatRecentCards chats={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/ChatRecentCards.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement ChatRecentCards**

Create `apps/web/src/app/(authenticated)/dashboard/v2/sections/ChatRecentCards.tsx`:

```typescript
'use client';

import Link from 'next/link';

export interface ChatRecentPreview {
  id: string;
  gameName: string;
  topic: string;
  snippet: string;
  confidence: number;
  timestamp: string;
}

interface ChatRecentCardsProps {
  chats: ChatRecentPreview[];
}

function formatConfidence(score: number): string {
  return `✓ ${Math.round(score * 100)}% accurata`;
}

export function ChatRecentCards({ chats }: ChatRecentCardsProps) {
  if (chats.length === 0) return null;

  return (
    <section className="mb-7">
      <div className="mb-3.5 flex items-center justify-between">
        <h3 className="flex items-center gap-3 font-quicksand text-[1.1rem] font-extrabold">
          <span
            aria-hidden
            className="inline-block h-[18px] w-1 rounded-sm"
            style={{ background: 'hsl(220 80% 55%)' }}
          />
          Chat recenti con l'agente
        </h3>
        <Link
          href="/chat"
          className="rounded-lg px-3 py-1.5 text-[0.78rem] font-bold text-[hsl(25_95%_40%)] transition-colors hover:bg-[hsla(25,95%,45%,0.08)]"
        >
          Vedi tutto →
        </Link>
      </div>
      <div className="grid grid-cols-3 gap-4">
        {chats.slice(0, 3).map(chat => (
          <div
            key={chat.id}
            className="relative flex min-h-[140px] cursor-pointer flex-col gap-3 overflow-hidden rounded-[20px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] p-5 shadow-[var(--shadow-warm-sm)] transition-all duration-300 hover:-translate-y-0.5 hover:shadow-[var(--shadow-warm-md)]"
          >
            <span
              aria-hidden
              className="absolute left-0 top-0 bottom-0 w-1"
              style={{ background: 'hsl(220 80% 55%)' }}
            />
            <div className="flex items-center gap-2.5">
              <div
                className="flex h-[38px] w-[38px] items-center justify-center rounded-[10px] text-lg"
                style={{ background: 'linear-gradient(135deg, hsl(220 72% 72%), hsl(220 80% 55%))' }}
                aria-hidden
              >
                💬
              </div>
              <div className="font-quicksand text-[0.88rem] font-extrabold leading-tight">
                {chat.gameName} · {chat.topic}
              </div>
            </div>
            <p className="line-clamp-2 text-[0.78rem] text-[var(--nh-text-secondary)]">
              {chat.snippet}
            </p>
            <div className="mt-auto flex items-center justify-between">
              <span
                className="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 font-nunito text-[0.68rem] font-extrabold uppercase tracking-wider"
                style={{
                  background: 'hsla(140, 60%, 45%, 0.12)',
                  color: 'hsl(140 60% 30%)',
                }}
              >
                {formatConfidence(chat.confidence)}
              </span>
              <span className="text-[0.7rem] font-semibold text-[var(--nh-text-muted)]">
                {chat.timestamp}
              </span>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/ChatRecentCards.test.tsx`
Expected: PASS — 3 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/v2/sections/ChatRecentCards.tsx apps/web/src/app/\(authenticated\)/dashboard/v2/__tests__/ChatRecentCards.test.tsx
git commit -m "feat(web): add Phase 2 dashboard ChatRecentCards section"
```

---

## Task 9: FriendsRow section

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/v2/sections/FriendsRow.tsx`
- Test: `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/FriendsRow.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/FriendsRow.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { FriendsRow } from '../sections/FriendsRow';

describe('FriendsRow', () => {
  const friends = [
    { id: 'f1', name: 'Luca Marconi', status: 'Sta giocando Azul', presence: 'online' as const },
    { id: 'f2', name: 'Sara Vitali', status: 'Online · 12 giochi', presence: 'online' as const },
    { id: 'f3', name: 'Giulia Bianchi', status: 'Inattiva da 2 ore', presence: 'idle' as const },
    { id: 'f4', name: 'Alessandro Rossi', status: 'Offline · ieri', presence: 'offline' as const },
  ];

  it('renders the section header', () => {
    render(<FriendsRow friends={friends} />);
    expect(screen.getByText(/Amici attivi/i)).toBeInTheDocument();
  });

  it('renders all 4 friends', () => {
    render(<FriendsRow friends={friends} />);
    expect(screen.getByText('Luca Marconi')).toBeInTheDocument();
    expect(screen.getByText('Sara Vitali')).toBeInTheDocument();
    expect(screen.getByText('Giulia Bianchi')).toBeInTheDocument();
    expect(screen.getByText('Alessandro Rossi')).toBeInTheDocument();
  });

  it('renders initials from the name', () => {
    render(<FriendsRow friends={[friends[0]]} />);
    expect(screen.getByText('LM')).toBeInTheDocument();
  });

  it('renders nothing when empty', () => {
    const { container } = render(<FriendsRow friends={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/FriendsRow.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement FriendsRow**

Create `apps/web/src/app/(authenticated)/dashboard/v2/sections/FriendsRow.tsx`:

```typescript
'use client';

import Link from 'next/link';

export interface FriendPreview {
  id: string;
  name: string;
  status: string;
  presence: 'online' | 'idle' | 'offline';
}

interface FriendsRowProps {
  friends: FriendPreview[];
}

function initials(name: string): string {
  return name
    .split(/\s+/)
    .map(part => part[0] ?? '')
    .join('')
    .slice(0, 2)
    .toUpperCase();
}

const PRESENCE_BG: Record<FriendPreview['presence'], string> = {
  online: 'hsl(140 60% 45%)',
  idle: 'hsl(38 92% 55%)',
  offline: '#cbd5e1',
};

export function FriendsRow({ friends }: FriendsRowProps) {
  if (friends.length === 0) return null;

  return (
    <section className="mb-7">
      <div className="mb-3.5 flex items-center justify-between">
        <h3 className="flex items-center gap-3 font-quicksand text-[1.1rem] font-extrabold">
          <span
            aria-hidden
            className="inline-block h-[18px] w-1 rounded-sm"
            style={{ background: 'hsl(262 83% 58%)' }}
          />
          Amici attivi
        </h3>
        <Link
          href="/players"
          className="rounded-lg px-3 py-1.5 text-[0.78rem] font-bold text-[hsl(25_95%_40%)] transition-colors hover:bg-[hsla(25,95%,45%,0.08)]"
        >
          Vedi tutto →
        </Link>
      </div>
      <div className="grid grid-cols-4 gap-3">
        {friends.slice(0, 4).map(friend => (
          <div
            key={friend.id}
            className="relative flex cursor-pointer items-center gap-2.5 rounded-xl border border-[var(--nh-border-default)] bg-[rgba(255,252,248,0.8)] px-3.5 py-2.5 transition-all duration-200 hover:translate-x-0.5 hover:bg-white hover:shadow-[var(--shadow-warm-sm)]"
          >
            <span
              aria-hidden
              className="absolute left-0 top-1/2 h-[60%] w-[3px] -translate-y-1/2 rounded-full"
              style={{ background: 'hsl(262 83% 58%)' }}
            />
            <div
              aria-hidden
              className="flex h-[42px] w-[42px] shrink-0 items-center justify-center rounded-[10px] font-quicksand text-sm font-extrabold text-white"
              style={{ background: 'linear-gradient(135deg, hsl(262 78% 75%), hsl(262 83% 55%))' }}
            >
              {initials(friend.name)}
            </div>
            <div className="min-w-0 flex-1">
              <div className="font-quicksand text-[0.82rem] font-extrabold text-[var(--nh-text-primary)]">
                {friend.name}
              </div>
              <div className="truncate text-[0.7rem] text-[var(--nh-text-muted)]">
                {friend.status}
              </div>
            </div>
            <span
              aria-label={`Presence: ${friend.presence}`}
              className="h-2 w-2 shrink-0 rounded-full"
              style={{
                background: PRESENCE_BG[friend.presence],
                boxShadow: '0 0 0 2px rgba(255,255,255,0.9)',
              }}
            />
          </div>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/FriendsRow.test.tsx`
Expected: PASS — 4 tests passing.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/v2/sections/FriendsRow.tsx apps/web/src/app/\(authenticated\)/dashboard/v2/__tests__/FriendsRow.test.tsx
git commit -m "feat(web): add Phase 2 dashboard FriendsRow section"
```

---

## Task 10: DashboardClientV2 composition + mini-nav registration

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/v2/DashboardClientV2.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/v2/index.ts`
- Test: `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/DashboardClientV2.test.tsx`

- [ ] **Step 1: Inspect the existing dashboard data layer**

Run: `cat apps/web/src/lib/stores/dashboard-store.ts | head -80` and `cat apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx | head -120`. Identify the data shape used by the existing dashboard (`SessionSummaryDto`, `TrendingGameDto`, `UserGameDto`, KPIs, live session).

- [ ] **Step 2: Write the failing integration test**

Create `apps/web/src/app/(authenticated)/dashboard/v2/__tests__/DashboardClientV2.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 'u1', displayName: 'Marco' },
  }),
}));

vi.mock('@/lib/stores/dashboard-store', () => ({
  useDashboardStore: () => ({
    totalGames: 47,
    totalSessions: 128,
    friendsCount: 8,
    chatCount: 36,
    activeLiveSession: null,
    continuePlayingGames: [
      { id: 'g1', title: 'Azul', subtitle: 'Plan B', rating: 7.8, players: '2–4', duration: '45m' },
      { id: 'g2', title: 'Wingspan', subtitle: 'Stonemaier', rating: 8.1, players: '1–5', duration: '70m' },
    ],
    recentChats: [],
    activeFriends: [],
    loading: false,
  }),
}));

vi.mock('@/stores/use-card-hand', () => {
  const state = {
    cards: [],
    pinnedIds: new Set(),
    drawCard: vi.fn(),
    pinCard: vi.fn(),
    unpinCard: vi.fn(),
  };
  return {
    useCardHand: (selector?: (s: typeof state) => unknown) =>
      selector ? selector(state) : state,
  };
});

import { DashboardClientV2 } from '../DashboardClientV2';

describe('DashboardClientV2', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the greeting with the user name', () => {
    render(<DashboardClientV2 />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
  });

  it('renders the KPI strip with store values', () => {
    render(<DashboardClientV2 />);
    expect(screen.getByText('47')).toBeInTheDocument();
    expect(screen.getByText('128')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByText('36')).toBeInTheDocument();
  });

  it('renders the continue carousel when games are present', () => {
    render(<DashboardClientV2 />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders the empty state when no live session', () => {
    render(<DashboardClientV2 />);
    expect(screen.getByText(/Nessuna partita in corso/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/DashboardClientV2.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 4: Implement DashboardClientV2**

Create `apps/web/src/app/(authenticated)/dashboard/v2/DashboardClientV2.tsx`:

```typescript
'use client';

import { useEffect } from 'react';

import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { useCardHand } from '@/stores/use-card-hand';

import { ChatRecentCards } from './sections/ChatRecentCards';
import { ContinueCarousel } from './sections/ContinueCarousel';
import { FriendsRow } from './sections/FriendsRow';
import { GreetingRow } from './sections/GreetingRow';
import { HeroLiveSession } from './sections/HeroLiveSession';
import { KpiStrip } from './sections/KpiStrip';

/**
 * Phase 2 dashboard client — new layout with greeting, hero, KPI, carousel,
 * chat cards, and friends row. Reads data from the existing useDashboardStore.
 */
export function DashboardClientV2() {
  const { user } = useAuth();
  const router = useRouter();
  const drawCard = useCardHand(s => s.drawCard);
  const store = useDashboardStore();

  // Register mini-nav config with the global shell
  useMiniNavConfig({
    breadcrumb: 'Home · Gaming Hub',
    tabs: [
      { id: 'overview', label: 'Panoramica', href: '/dashboard' },
    ],
    activeTabId: 'overview',
    primaryAction: {
      label: 'Nuova partita',
      icon: '＋',
      onClick: () => router.push('/sessions/new'),
    },
  });

  // Draw this page as a hand card for cross-page context memory
  useEffect(() => {
    drawCard({
      id: 'section-dashboard',
      entity: 'game',
      title: 'Home',
      href: '/dashboard',
    });
  }, [drawCard]);

  const handleContinueSession = () => {
    if (store.activeLiveSession) {
      router.push(`/sessions/live/${store.activeLiveSession.id}`);
    } else {
      router.push('/sessions/new');
    }
  };

  return (
    <div className="mx-auto max-w-[1440px] p-7 pb-12">
      <GreetingRow
        displayName={user?.displayName ?? 'giocatore'}
        subtitle="Ecco cosa succede nella tua tavola oggi"
        stats={[
          { label: 'Partite mese', value: String(store.totalSessions ?? 0) },
          { label: 'Giochi', value: String(store.totalGames ?? 0) },
        ]}
      />

      <HeroLiveSession
        session={store.activeLiveSession ?? null}
        onContinue={handleContinueSession}
      />

      <KpiStrip
        kpis={{
          games: store.totalGames ?? 0,
          sessions: store.totalSessions ?? 0,
          friends: store.friendsCount ?? 0,
          chats: store.chatCount ?? 0,
        }}
      />

      <ContinueCarousel games={store.continuePlayingGames ?? []} />
      <ChatRecentCards chats={store.recentChats ?? []} />
      <FriendsRow friends={store.activeFriends ?? []} />
    </div>
  );
}
```

**Note on `useDashboardStore` shape:** The new fields used here (`friendsCount`, `chatCount`, `activeLiveSession`, `continuePlayingGames`, `recentChats`, `activeFriends`) may not match the existing store shape. When running Task 10, if the store doesn't expose these fields, the implementer must either:
- (a) Extend `useDashboardStore` to compute them from the existing data, OR
- (b) Stub them with hardcoded empty arrays / zeros for Phase 2 (mark as TODO for Phase 3)
If the divergence is too wide, STOP and report NEEDS_CONTEXT.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard/v2/__tests__/DashboardClientV2.test.tsx`
Expected: PASS — 4 tests passing.

- [ ] **Step 6: Create the barrel**

Create `apps/web/src/app/(authenticated)/dashboard/v2/index.ts`:

```typescript
export { DashboardClientV2 } from './DashboardClientV2';
```

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/v2/
git commit -m "feat(web): add Phase 2 DashboardClientV2 composition"
```

---

## Task 11: Wire DashboardClientV2 into dashboard-client.tsx behind flag

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

- [ ] **Step 1: Read the current file**

Run: `cat apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx | head -40`

- [ ] **Step 2: Add feature flag branch at the top of the exported function**

At the top of the `DashboardClient` function component, add:

```typescript
import { isUxRedesignEnabled } from '@/lib/feature-flags';
import { DashboardClientV2 } from './v2';
```

And inside the component, as the first statement:

```typescript
if (isUxRedesignEnabled()) {
  return <DashboardClientV2 />;
}
```

The rest of the legacy bento grid stays untouched after this early return.

- [ ] **Step 3: Write an integration test for the branch**

Create (or extend) `apps/web/src/app/(authenticated)/dashboard/__tests__/dashboard-client.flag.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';

// Minimal mocks — the real DashboardClientV2 has its own mocks in its test file
vi.mock('./v2', () => ({
  DashboardClientV2: () => <div data-testid="dashboard-v2">V2 Dashboard</div>,
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({ user: { id: 'u1', displayName: 'Marco' } }),
}));

vi.mock('@/lib/stores/dashboard-store', () => ({
  useDashboardStore: () => ({
    totalGames: 0,
    totalSessions: 0,
    loading: false,
  }),
}));

// ... add any other mocks required by the legacy DashboardClient

const originalEnv = process.env.NEXT_PUBLIC_UX_REDESIGN;

describe('DashboardClient feature flag branch', () => {
  afterEach(() => {
    if (originalEnv === undefined) delete process.env.NEXT_PUBLIC_UX_REDESIGN;
    else process.env.NEXT_PUBLIC_UX_REDESIGN = originalEnv;
    vi.resetModules();
  });

  it('renders DashboardClientV2 when flag is on', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
    vi.resetModules();
    const mod = await import('../dashboard-client');
    const DashboardClient = mod.DashboardClient;
    render(<DashboardClient />);
    expect(screen.getByTestId('dashboard-v2')).toBeInTheDocument();
  });

  it('renders legacy bento grid when flag is off', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'false';
    vi.resetModules();
    const mod = await import('../dashboard-client');
    const DashboardClient = mod.DashboardClient;
    render(<DashboardClient />);
    expect(screen.queryByTestId('dashboard-v2')).not.toBeInTheDocument();
    // Legacy path should have rendered something (first render = empty/loading OK)
  });
});
```

**Note:** the legacy `DashboardClient` has many dependencies (many store fields, providers, etc.). If adding mocks for all of them is too fragile, the legacy-off test can simply assert that `data-testid="dashboard-v2"` is NOT present and leave the full legacy render exercise to its existing tests. Don't duplicate the legacy test coverage.

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/dashboard`
Expected: all tests pass (new flag test + existing dashboard widget tests unchanged).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx apps/web/src/app/\(authenticated\)/dashboard/__tests__/dashboard-client.flag.test.tsx
git commit -m "feat(web): wire DashboardClientV2 into DashboardClient behind feature flag"
```

---

## Task 12: Phase 2 quality gate

**Files:** none (just commands)

- [ ] **Step 1: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors.

- [ ] **Step 2: Lint**

Run: `cd apps/web && pnpm lint`
Expected: 0 errors. Pre-existing warnings OK. ZERO new warnings in `dashboard/v2/` files.

- [ ] **Step 3: Full unit test suite**

Run: `cd apps/web && pnpm test`
Expected: all tests pass (Phase 1 tests + new Phase 2 tests).

- [ ] **Step 4: Production build (flag ON)**

```bash
cd apps/web
NEXT_PUBLIC_UX_REDESIGN=true pnpm build
```
Expected: build succeeds.

- [ ] **Step 5: Production build (flag OFF)**

```bash
cd apps/web
pnpm build
```
Expected: build succeeds with the same output as Phase 1 baseline.

- [ ] **Step 6: Final Phase 2 marker commit**

```bash
git commit --allow-empty -m "chore(web): phase 2 dashboard gaming hub complete

New dashboard layout (GreetingRow + HeroLiveSession + KpiStrip +
ContinueCarousel + ChatRecentCards + FriendsRow) behind the
NEXT_PUBLIC_UX_REDESIGN flag. Phase 1 shell foundation extended
with: mini-nav registration via useMiniNavConfig, hand rail expand
wiring (M6), stable hook ref (M3), icon type (M4), font utilities (M1).
Legacy bento grid unchanged. Phase 3 (Library Hub) to follow."
```

---

## Self-Review Checklist (run before marking Phase 2 complete)

- [ ] All 12 tasks marked complete
- [ ] M1 font utility cleanup applied (`grep -r "font-\[var" apps/web/src/components/layout/UserShell/v2` returns nothing)
- [ ] M3 useMiniNavConfig stable — regression test asserts no re-registration on equivalent rerenders
- [ ] M4 `MiniNavPrimaryAction.icon` documented with JSDoc, rendered in `MiniNavSlot`
- [ ] M6 DesktopHandRail expand transition wired, test covers expanded state
- [ ] All new v2 dashboard sections have ≥3 tests each
- [ ] DashboardClientV2 integration test passes flag-on branch
- [ ] Legacy bento grid renders untouched when flag is off
- [ ] `pnpm typecheck`, `pnpm lint`, `pnpm test`, both `pnpm build` flag states succeed
- [ ] Manual smoke: with `NEXT_PUBLIC_UX_REDESIGN=true`, `/dashboard` shows the new layout with mini-nav "Home · Gaming Hub" + "＋ Nuova partita" action visible in shell

---

## Next Phases (future plans)

- **Phase 3** — Library Hub (carousels for Continua / Personal / Catalogo / Wishlist + filter bar)
- **Phase 4** — Chat slide-over panel (global panel wired to `TopBarChatButton`)
- **Phase 5** — Cleanup (remove feature flag, delete legacy bento/AppNavbar/CardRack, add Playwright visual regression, deprecate old routes)

---

## References

- Spec: `docs/superpowers/specs/2026-04-08-desktop-ux-redesign-design.md` §5.1
- Phase 1 plan: `docs/superpowers/plans/2026-04-08-desktop-ux-redesign-phase-1-shell.md`
- Phase 1 PR: meepleAi-app/meepleai-monorepo#296
- Mockup (committed in `.superpowers/brainstorm/`): `dashboard-gaming-hub.html`
- Design tokens: `apps/web/src/styles/design-tokens.css`
- Existing dashboard data layer: `apps/web/src/lib/stores/dashboard-store.ts`
- Existing dashboard page: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`
