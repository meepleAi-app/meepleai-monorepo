# Dashboard Restyle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the authenticated Gaming Hub dashboard (`/dashboard`) to maximum mock fidelity (Approach 3 — Token-First Totale) per spec `docs/for-developers/specs/2026-05-12-dashboard-restyle-design.md`.

**Architecture:** Atomic single PR on branch `feature/dashboard-restyle-mock-fidelity` targeting `main-dev`. Introduce 4 new components (`DashboardHero`, `DashboardStatsRow`, `DashboardStatCard`, `DiscoverCarousel`), extract `EmptyCTA` from inline, refactor `EntityZone` + `DashboardClient`, fully rewrite obsolete e2e suite, delete `GreetingStrip`. All under canonical paths (FREEZE-compliant), zero hardcoded color utilities (DS-15 lint at `error`).

**Tech Stack:**
- Next.js 16 App Router, React 19, TypeScript strict
- Tailwind 4 with semantic tokens + entity utility classes (`e-game`, `e-session`, …)
- Vitest + React Testing Library + jest-axe (unit)
- Playwright + `@axe-core/playwright` (e2e + a11y)
- React Query (TanStack), Zustand, next-themes (theme), sonner (toast)
- Existing hooks: `useAuth`, `useLibraryStats`, `useLibrary`, `useActiveSessions`, `useAgents`, `useUpcomingGameNights`

---

## File Map

### Created
- `apps/web/src/components/dashboard/DashboardHero.tsx`
- `apps/web/src/components/dashboard/DashboardStatsRow.tsx`
- `apps/web/src/components/dashboard/DashboardStatCard.tsx`
- `apps/web/src/components/dashboard/EmptyCTA.tsx`
- `apps/web/src/components/ui/data-display/discover-carousel/DiscoverCarousel.tsx`
- `apps/web/src/components/ui/data-display/discover-carousel/index.ts`
- `apps/web/src/__tests__/components/dashboard/DashboardHero.test.tsx`
- `apps/web/src/__tests__/components/dashboard/DashboardStatsRow.test.tsx`
- `apps/web/src/__tests__/components/dashboard/DashboardStatCard.test.tsx`
- `apps/web/src/__tests__/components/dashboard/EmptyCTA.test.tsx`
- `apps/web/src/__tests__/components/dashboard/a11y.test.tsx`
- `apps/web/src/components/ui/data-display/discover-carousel/__tests__/DiscoverCarousel.test.tsx`

### Modified
- `apps/web/src/hooks/queries/useGameNights.ts` (extend `useUpcomingGameNights` with options)
- `apps/web/src/components/dashboard/EntityZone.tsx` (badge swap, useId, aria-labelledby)
- `apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx` (selectors)
- `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` (compose new components)
- `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx` (assert new tree)
- `apps/web/tests/e2e/dashboard.spec.ts` (full rewrite — obsolete suite)

### Deleted
- `apps/web/src/components/dashboard/GreetingStrip.tsx`
- `apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx`

---

## Task 1: Setup branch and verify baseline

**Files:** none (environment setup)

- [ ] **Step 1.1: Switch to `main-dev` and pull latest**

```bash
git checkout main-dev
git pull --ff-only
```

Expected: clean working tree on `main-dev`, no divergence.

- [ ] **Step 1.2: Verify HEAD is on main-dev (CLAUDE.md branch hygiene)**

```bash
git branch --show-current
git status
```

Expected: `main-dev`, clean tree.

- [ ] **Step 1.3: Create feature branch**

```bash
git checkout -b feature/dashboard-restyle-mock-fidelity
git config branch.feature/dashboard-restyle-mock-fidelity.parent main-dev
```

Expected: new branch, parent registered.

- [ ] **Step 1.4: Verify baseline tests pass**

```bash
cd apps/web
pnpm typecheck
pnpm test src/__tests__/components/dashboard src/components/dashboard/__tests__
```

Expected: no TypeScript errors. Baseline contains ~17 dashboard tests under `components/dashboard/__tests__/` (StatsRow, HeroBanner, QuickActionsRow, etc.) plus 4 under `src/__tests__/components/dashboard/` (GreetingStrip, EntityZone, AddToLibraryModal, SearchExpander). Record total counts in a scratch note (do not commit) to compare with post-implementation count. **Critical**: do NOT delete the existing admin-scoped `StatsRow.tsx` or `StatsRow.test.tsx` — they belong to a different feature (admin overview).

- [ ] **Step 1.5: Verify baseline e2e dashboard file exists (will be rewritten)**

```bash
ls apps/web/tests/e2e/dashboard.spec.ts
```

Expected: file exists. Note: tests inside reference obsolete UI ("Cronologia Chat", "Apri Chat") and will be fully replaced.

---

## Task 2: Extend `useUpcomingGameNights` with `retry` option

**Files:**
- Modify: `apps/web/src/hooks/queries/useGameNights.ts`

- [ ] **Step 2.1: Open the hook file**

Read: `apps/web/src/hooks/queries/useGameNights.ts` (lines 23-29).

Current:
```ts
export function useUpcomingGameNights(enabled: boolean = true) {
  return useQuery({
    queryKey: gameNightKeys.upcoming(),
    queryFn: () => api.gameNights.getUpcoming(),
    enabled,
  });
}
```

- [ ] **Step 2.2: Replace with options-accepting signature**

```ts
export function useUpcomingGameNights(
  options: { enabled?: boolean; retry?: number | boolean } = {}
) {
  const { enabled = true, retry } = options;
  return useQuery({
    queryKey: gameNightKeys.upcoming(),
    queryFn: () => api.gameNights.getUpcoming(),
    enabled,
    ...(retry !== undefined ? { retry } : {}),
  });
}
```

- [ ] **Step 2.3: Search for existing callers and migrate if needed**

```bash
cd apps/web
grep -rn "useUpcomingGameNights" src/ --include="*.tsx" --include="*.ts"
```

Expected: all callers pass `useUpcomingGameNights()` or `useUpcomingGameNights(true)`. If any passes a boolean positional argument, update to `useUpcomingGameNights({ enabled: true })`.

- [ ] **Step 2.4: Update callers (if any positional `true`/`false` found)**

For each match, change `useUpcomingGameNights(true)` → `useUpcomingGameNights({ enabled: true })`. If no callers exist with positional arg, skip.

- [ ] **Step 2.5: Run typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: no errors.

- [ ] **Step 2.6: Commit**

```bash
git add apps/web/src/hooks/queries/useGameNights.ts
git commit -m "refactor(dashboard): extend useUpcomingGameNights with retry option"
```

---

## Task 3: Extract `EmptyCTA` with entity tokens (TDD)

**Files:**
- Create: `apps/web/src/components/dashboard/EmptyCTA.tsx`
- Create: `apps/web/src/__tests__/components/dashboard/EmptyCTA.test.tsx`

- [ ] **Step 3.1: Write the failing test**

Create `apps/web/src/__tests__/components/dashboard/EmptyCTA.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { EmptyCTA } from '@/components/dashboard/EmptyCTA';

describe('EmptyCTA', () => {
  it('renders icon, title, sub, primary action and secondary action', () => {
    render(
      <EmptyCTA
        entity="session"
        icon="🎯"
        title="Nessuna sessione"
        sub="Crea una nuova partita per iniziare."
        actions={[
          { label: 'Crea sessione', href: '/sessions/new', primary: true },
          { label: 'Sfoglia', href: '/sessions' },
        ]}
      />
    );
    expect(screen.getByText('Nessuna sessione')).toBeInTheDocument();
    expect(screen.getByText('Crea una nuova partita per iniziare.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Crea sessione/i })).toHaveAttribute('href', '/sessions/new');
    expect(screen.getByRole('link', { name: /Sfoglia/i })).toHaveAttribute('href', '/sessions');
  });

  it('icon has aria-hidden=true', () => {
    render(
      <EmptyCTA entity="game" icon="🎲" title="Empty" sub="x" actions={[]} />
    );
    const icon = screen.getByText('🎲');
    expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('wrapper has role=status', () => {
    render(
      <EmptyCTA entity="game" icon="🎲" title="Empty" sub="x" actions={[]} />
    );
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('does not contain hardcoded amber color classes (DS-15 regression guard)', () => {
    const { container } = render(
      <EmptyCTA
        entity="agent"
        icon="🤖"
        title="No agents"
        sub="x"
        actions={[{ label: 'Chat', href: '/chat', primary: true }]}
      />
    );
    const html = container.innerHTML;
    expect(html).not.toMatch(/bg-amber-\d/);
    expect(html).not.toMatch(/border-amber-\d/);
    expect(html).not.toMatch(/text-amber-\d/);
    expect(html).not.toMatch(/rgba\(180,130,80/);
  });

  it('applies entity data attribute on wrapper', () => {
    const { container } = render(
      <EmptyCTA entity="session" icon="🎯" title="x" sub="x" actions={[]} />
    );
    expect(container.querySelector('[data-entity="session"]')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3.2: Run test, expect failure**

```bash
cd apps/web
pnpm test src/__tests__/components/dashboard/EmptyCTA.test.tsx
```

Expected: FAIL — module not found.

- [ ] **Step 3.3: Implement `EmptyCTA`**

Create `apps/web/src/components/dashboard/EmptyCTA.tsx`:

```tsx
'use client';

import type { ReactElement } from 'react';

import Link from 'next/link';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface EmptyCTAAction {
  label: string;
  href: string;
  primary?: boolean;
}

export interface EmptyCTAProps {
  entity: MeepleEntityType;
  icon: string;
  title: string;
  sub: string;
  actions: EmptyCTAAction[];
}

export function EmptyCTA({ entity, icon, title, sub, actions }: EmptyCTAProps): ReactElement {
  return (
    <div
      data-entity={entity}
      role="status"
      className={`e-${entity} flex flex-col items-center gap-3 rounded-xl border border-dashed
                  border-[hsl(var(--e)/0.25)] bg-[hsl(var(--e)/0.04)] p-8 text-center`}
    >
      <span className="text-[32px]" aria-hidden="true">
        {icon}
      </span>
      <div>
        <p className="font-quicksand text-base font-bold text-foreground">{title}</p>
        <p className="mt-1 max-w-sm text-sm leading-relaxed text-muted-foreground">{sub}</p>
      </div>
      {actions.length > 0 && (
        <div className="flex flex-wrap justify-center gap-2">
          {actions.map(action => (
            <Link
              key={action.href}
              href={action.href}
              className={
                action.primary
                  ? 'inline-flex items-center gap-1 rounded-full bg-[hsl(var(--e))] px-4 py-2 font-quicksand text-xs font-bold text-white shadow-sm transition-transform hover:scale-[1.03]'
                  : 'inline-flex items-center gap-1 rounded-full border border-[hsl(var(--e)/0.6)] bg-transparent px-4 py-2 font-quicksand text-xs font-bold text-[hsl(var(--e))]'
              }
            >
              {action.label}
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 3.4: Run test, expect pass**

```bash
pnpm test src/__tests__/components/dashboard/EmptyCTA.test.tsx
```

Expected: all 5 tests PASS.

- [ ] **Step 3.5: Run token lint**

```bash
pnpm lint:tokens -- src/components/dashboard/EmptyCTA.tsx
```

Expected: 0 violations.

- [ ] **Step 3.6: Commit**

```bash
git add apps/web/src/components/dashboard/EmptyCTA.tsx apps/web/src/__tests__/components/dashboard/EmptyCTA.test.tsx
git commit -m "feat(dashboard): extract EmptyCTA with entity-token contract"
```

---

## Task 4: Build `DiscoverCarousel` primitive (TDD)

**Files:**
- Create: `apps/web/src/components/ui/data-display/discover-carousel/DiscoverCarousel.tsx`
- Create: `apps/web/src/components/ui/data-display/discover-carousel/index.ts`
- Create: `apps/web/src/components/ui/data-display/discover-carousel/__tests__/DiscoverCarousel.test.tsx`

- [ ] **Step 4.1: Write the failing test**

Create `apps/web/src/components/ui/data-display/discover-carousel/__tests__/DiscoverCarousel.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { DiscoverCarousel } from '../DiscoverCarousel';

describe('DiscoverCarousel', () => {
  beforeEach(() => {
    // Mock scrollBy / scrollLeft (JSDOM does not implement scroll)
    Element.prototype.scrollBy = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'scrollWidth', {
      configurable: true,
      get: () => 1000,
    });
    Object.defineProperty(HTMLElement.prototype, 'clientWidth', {
      configurable: true,
      get: () => 400,
    });
  });

  it('renders children in a role=region with ariaLabel', () => {
    render(
      <DiscoverCarousel ariaLabel="Test carousel">
        <div data-testid="child-1">A</div>
        <div data-testid="child-2">B</div>
      </DiscoverCarousel>
    );
    const region = screen.getByRole('region', { name: 'Test carousel' });
    expect(region).toBeInTheDocument();
    expect(screen.getByTestId('child-1')).toBeInTheDocument();
    expect(screen.getByTestId('child-2')).toBeInTheDocument();
  });

  it('track is focusable (tabIndex=0)', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    expect(track).toHaveAttribute('tabindex', '0');
  });

  it('arrow buttons exist and have aria-labels', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    expect(screen.getByRole('button', { name: /Scorri verso sinistra/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Scorri verso destra/i })).toBeInTheDocument();
  });

  it('arrows have aria-controls pointing to track id', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    const trackId = track.id;
    expect(trackId).toBeTruthy();
    const leftArrow = screen.getByRole('button', { name: /sinistra/i });
    const rightArrow = screen.getByRole('button', { name: /destra/i });
    expect(leftArrow).toHaveAttribute('aria-controls', trackId);
    expect(rightArrow).toHaveAttribute('aria-controls', trackId);
  });

  it('right arrow click calls scrollBy on track', () => {
    render(
      <DiscoverCarousel ariaLabel="x" itemWidth={260} gap={12}>
        <div>A</div>
        <div>B</div>
      </DiscoverCarousel>
    );
    const rightArrow = screen.getByRole('button', { name: /destra/i });
    fireEvent.click(rightArrow);
    expect(Element.prototype.scrollBy).toHaveBeenCalledWith({
      left: 272,
      behavior: 'smooth',
    });
  });

  it('left arrow click calls scrollBy with negative offset', () => {
    render(
      <DiscoverCarousel ariaLabel="x" itemWidth={260} gap={12}>
        <div>A</div>
      </DiscoverCarousel>
    );
    const leftArrow = screen.getByRole('button', { name: /sinistra/i });
    fireEvent.click(leftArrow);
    expect(Element.prototype.scrollBy).toHaveBeenCalledWith({
      left: -272,
      behavior: 'smooth',
    });
  });

  it('ArrowRight keydown on track scrolls right', () => {
    render(
      <DiscoverCarousel ariaLabel="x" itemWidth={200} gap={8}>
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    fireEvent.keyDown(track, { key: 'ArrowRight' });
    expect(Element.prototype.scrollBy).toHaveBeenCalledWith({
      left: 208,
      behavior: 'smooth',
    });
  });

  it('ArrowLeft keydown on track scrolls left', () => {
    render(
      <DiscoverCarousel ariaLabel="x" itemWidth={200} gap={8}>
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    fireEvent.keyDown(track, { key: 'ArrowLeft' });
    expect(Element.prototype.scrollBy).toHaveBeenCalledWith({
      left: -208,
      behavior: 'smooth',
    });
  });

  it('Home key scrolls to start, End key scrolls to end', () => {
    const scrollToMock = vi.fn();
    Element.prototype.scrollTo = scrollToMock;
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    const track = screen.getByTestId('discover-track');
    fireEvent.keyDown(track, { key: 'Home' });
    expect(scrollToMock).toHaveBeenCalledWith({ left: 0, behavior: 'smooth' });
    fireEvent.keyDown(track, { key: 'End' });
    expect(scrollToMock).toHaveBeenCalledWith({ left: 1000, behavior: 'smooth' });
  });

  it('does NOT inject role=list or role=listitem (preserves consumer semantics)', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div data-testid="c1">A</div>
      </DiscoverCarousel>
    );
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });

  it('renders fade gradients on both sides', () => {
    render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    expect(screen.getByTestId('discover-fade-left')).toBeInTheDocument();
    expect(screen.getByTestId('discover-fade-right')).toBeInTheDocument();
  });

  it('wrapper carries the "group" Tailwind class (required for hover-revealed arrows)', () => {
    const { container } = render(
      <DiscoverCarousel ariaLabel="x">
        <div>A</div>
      </DiscoverCarousel>
    );
    const wrapper = container.querySelector('.discover-carousel');
    expect(wrapper).toBeInTheDocument();
    expect(wrapper?.classList.contains('group')).toBe(true);
  });
});
```

- [ ] **Step 4.2: Run test, expect failure**

```bash
pnpm test src/components/ui/data-display/discover-carousel
```

Expected: FAIL — module not found.

- [ ] **Step 4.3: Implement `DiscoverCarousel`**

Create `apps/web/src/components/ui/data-display/discover-carousel/DiscoverCarousel.tsx`:

```tsx
'use client';

import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react';

export interface DiscoverCarouselProps {
  children: ReactNode;
  ariaLabel: string;
  itemWidth?: number; // pixel width of one item; used for arrow + keyboard scroll step
  gap?: number;       // pixel gap between items
}

const DEFAULT_ITEM_WIDTH = 260;
const DEFAULT_GAP = 12;

export function DiscoverCarousel({
  children,
  ariaLabel,
  itemWidth = DEFAULT_ITEM_WIDTH,
  gap = DEFAULT_GAP,
}: DiscoverCarouselProps) {
  const trackId = useId();
  const trackRef = useRef<HTMLDivElement | null>(null);
  const step = itemWidth + gap;

  const scrollByStep = (delta: number) => {
    trackRef.current?.scrollBy({ left: delta, behavior: 'smooth' });
  };

  const scrollToEdge = (edge: 'start' | 'end') => {
    if (!trackRef.current) return;
    const left = edge === 'start' ? 0 : trackRef.current.scrollWidth;
    trackRef.current.scrollTo({ left, behavior: 'smooth' });
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    switch (event.key) {
      case 'ArrowRight':
        event.preventDefault();
        scrollByStep(step);
        break;
      case 'ArrowLeft':
        event.preventDefault();
        scrollByStep(-step);
        break;
      case 'Home':
        event.preventDefault();
        scrollToEdge('start');
        break;
      case 'End':
        event.preventDefault();
        scrollToEdge('end');
        break;
    }
  };

  return (
    <div className="discover-carousel group relative" role="region" aria-label={ariaLabel}>
      <div
        data-testid="discover-fade-left"
        aria-hidden="true"
        className="pointer-events-none absolute bottom-0 left-0 top-0 z-[1] w-12 bg-gradient-to-r from-background to-transparent"
      />
      <div
        data-testid="discover-fade-right"
        aria-hidden="true"
        className="pointer-events-none absolute bottom-0 right-0 top-0 z-[1] w-12 bg-gradient-to-l from-background to-transparent"
      />

      <button
        type="button"
        aria-label="Scorri verso sinistra"
        aria-controls={trackId}
        onClick={() => scrollByStep(-step)}
        className="absolute left-2 top-1/2 z-[2] grid h-10 w-10 -translate-y-1/2 place-items-center rounded-full border border-border bg-card opacity-0 shadow-md transition-opacity duration-200 focus-visible:opacity-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--c-game))] group-hover:opacity-100"
      >
        ‹
      </button>

      <div
        ref={trackRef}
        id={trackId}
        data-testid="discover-track"
        tabIndex={0}
        onKeyDown={handleKeyDown}
        className="flex gap-3 overflow-x-auto pb-2 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--c-game))]"
        style={{ scrollSnapType: 'x mandatory' }}
      >
        {children}
      </div>

      <button
        type="button"
        aria-label="Scorri verso destra"
        aria-controls={trackId}
        onClick={() => scrollByStep(step)}
        className="absolute right-2 top-1/2 z-[2] grid h-10 w-10 -translate-y-1/2 place-items-center rounded-full border border-border bg-card opacity-0 shadow-md transition-opacity duration-200 focus-visible:opacity-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--c-game))] group-hover:opacity-100"
      >
        ›
      </button>
    </div>
  );
}
```

> Note: arrows fade in on hover via parent `group-hover`. The wrapper class already includes `group` (applied in Step 4.3 above). The test at Step 4.1 includes an explicit assertion that the wrapper has `class="group"`, locking the requirement.

- [ ] **Step 4.4: (intentionally left as no-op — `group` already present in 4.3)**

The previous version of this plan required a separate "correction" step. The `group` class is now baked into Step 4.3's code and asserted by Step 4.1's test. Skip this step.

- [ ] **Step 4.5: Create the barrel export**

Create `apps/web/src/components/ui/data-display/discover-carousel/index.ts`:

```ts
export { DiscoverCarousel } from './DiscoverCarousel';
export type { DiscoverCarouselProps } from './DiscoverCarousel';
```

- [ ] **Step 4.6: Run test, expect pass**

```bash
pnpm test src/components/ui/data-display/discover-carousel
```

Expected: all 11 tests PASS.

- [ ] **Step 4.7: Run token lint + typecheck**

```bash
pnpm lint:tokens -- src/components/ui/data-display/discover-carousel
pnpm typecheck
```

Expected: 0 violations, 0 type errors.

- [ ] **Step 4.8: Commit**

```bash
git add apps/web/src/components/ui/data-display/discover-carousel/
git commit -m "feat(ui): add DiscoverCarousel primitive (scroll-snap + keyboard + a11y)"
```

---

## Task 5: Build `DashboardStatCard` (TDD)

**Files:**
- Create: `apps/web/src/components/dashboard/DashboardStatCard.tsx`
- Create: `apps/web/src/__tests__/components/dashboard/DashboardStatCard.test.tsx`

- [ ] **Step 5.1: Write the failing test**

Create `apps/web/src/__tests__/components/dashboard/DashboardStatCard.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { DashboardStatCard } from '@/components/dashboard/DashboardStatCard';

describe('DashboardStatCard', () => {
  it('renders value, label, and entity data attribute', () => {
    render(
      <DashboardStatCard entity="game" value={42} label="Giochi" href="/library" />
    );
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('Giochi')).toBeInTheDocument();
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('data-entity', 'game');
    expect(link).toHaveAttribute('href', '/library');
  });

  it('aria-label has count + destination format', () => {
    render(
      <DashboardStatCard entity="session" value={3} label="Sessioni" href="/sessions" />
    );
    expect(screen.getByLabelText('Sessioni: 3 elementi. Vai a /sessions')).toBeInTheDocument();
  });

  it('renders skeleton placeholder when isLoading=true', () => {
    render(
      <DashboardStatCard
        entity="agent"
        value={0}
        label="Agenti"
        href="/agents"
        isLoading
      />
    );
    expect(screen.queryByText('0')).not.toBeInTheDocument();
    expect(screen.getByTestId('stat-card-skeleton')).toBeInTheDocument();
  });

  it('renders "—" and retry button when isError=true', () => {
    const onRetry = vi.fn();
    render(
      <DashboardStatCard
        entity="event"
        value={0}
        label="Eventi"
        href="/game-nights"
        isError
        onRetry={onRetry}
      />
    );
    expect(screen.getByText('—')).toBeInTheDocument();
    const retry = screen.getByRole('button', {
      name: /Errore caricamento Eventi\. Premi Invio per riprovare/i,
    });
    expect(retry).toBeInTheDocument();
    fireEvent.click(retry);
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('retry button is disabled when isFetching=true', () => {
    const onRetry = vi.fn();
    render(
      <DashboardStatCard
        entity="event"
        value={0}
        label="Eventi"
        href="/game-nights"
        isError
        isFetching
        onRetry={onRetry}
      />
    );
    const retry = screen.getByRole('button');
    expect(retry).toBeDisabled();
    fireEvent.click(retry);
    expect(onRetry).not.toHaveBeenCalled();
  });

  it('value uses tabular-nums utility', () => {
    const { container } = render(
      <DashboardStatCard entity="game" value={42} label="x" href="/x" />
    );
    expect(container.querySelector('.tabular-nums')).toBeInTheDocument();
  });
});
```

- [ ] **Step 5.2: Run test, expect failure**

```bash
pnpm test src/__tests__/components/dashboard/DashboardStatCard.test.tsx
```

Expected: FAIL — module not found.

- [ ] **Step 5.3: Implement `DashboardStatCard`**

Create `apps/web/src/components/dashboard/DashboardStatCard.tsx`:

```tsx
'use client';

import Link from 'next/link';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface DashboardStatCardProps {
  entity: MeepleEntityType;
  value: number;
  label: string;
  href: string;
  isLoading?: boolean;
  isError?: boolean;
  isFetching?: boolean;
  onRetry?: () => void;
}

export function DashboardStatCard({
  entity,
  value,
  label,
  href,
  isLoading,
  isError,
  isFetching,
  onRetry,
}: DashboardStatCardProps) {
  if (isError) {
    return (
      <button
        type="button"
        data-entity={entity}
        onClick={onRetry}
        disabled={isFetching || !onRetry}
        aria-label={`Errore caricamento ${label}. Premi Invio per riprovare`}
        className={`e-${entity} relative w-full overflow-hidden rounded-xl border border-border bg-card p-5
                    text-left transition-all duration-280 ease-out
                    motion-safe:hover:-translate-y-[3px] motion-safe:hover:scale-[1.02]
                    hover:border-[hsl(var(--e)/0.4)] hover:shadow-md
                    focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--e))]
                    disabled:cursor-not-allowed disabled:opacity-60`}
      >
        <div className="font-quicksand text-[32px] font-extrabold leading-none tracking-tight tabular-nums text-[hsl(var(--e))]">
          —
        </div>
        <div className="mt-1 text-xs font-bold uppercase tracking-wider text-muted-foreground">
          {label}
        </div>
      </button>
    );
  }

  return (
    <Link
      href={href}
      data-entity={entity}
      aria-label={`${label}: ${value} elementi. Vai a ${href}`}
      className={`e-${entity} relative block overflow-hidden rounded-xl border border-border bg-card p-5
                  transition-all duration-280
                  motion-safe:hover:-translate-y-[3px] motion-safe:hover:scale-[1.02]
                  hover:border-[hsl(var(--e)/0.4)] hover:shadow-md
                  focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--e))]`}
    >
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 opacity-70 transition-opacity duration-280
                   bg-[radial-gradient(circle_at_top_right,hsl(var(--e)/0.10),transparent_60%)]
                   dark:bg-[radial-gradient(circle_at_top_right,hsl(var(--e)/0.18),transparent_60%)]
                   hover:opacity-100"
      />
      {isLoading ? (
        <div
          data-testid="stat-card-skeleton"
          className="h-8 w-12 animate-pulse rounded bg-muted"
        />
      ) : (
        <div className="font-quicksand text-[32px] font-extrabold leading-none tracking-tight tabular-nums text-[hsl(var(--e))]">
          {value}
        </div>
      )}
      <div className="mt-1 text-xs font-bold uppercase tracking-wider text-muted-foreground">
        {label}
      </div>
    </Link>
  );
}
```

- [ ] **Step 5.4: Run test, expect pass**

```bash
pnpm test src/__tests__/components/dashboard/DashboardStatCard.test.tsx
```

Expected: all 6 tests PASS.

- [ ] **Step 5.5: Run token lint**

```bash
pnpm lint:tokens -- src/components/dashboard/DashboardStatCard.tsx
```

Expected: 0 violations. `text-white` not present; entity utilities allowed; `bg-card`, `bg-muted`, `border-border`, `text-muted-foreground` are semantic.

- [ ] **Step 5.6: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardStatCard.tsx apps/web/src/__tests__/components/dashboard/DashboardStatCard.test.tsx
git commit -m "feat(dashboard): add DashboardStatCard atom (entity-tinted, retry-aware)"
```

---

## Task 6: Build `DashboardStatsRow` (TDD)

**Files:**
- Create: `apps/web/src/components/dashboard/DashboardStatsRow.tsx`
- Create: `apps/web/src/__tests__/components/dashboard/DashboardStatsRow.test.tsx`

- [ ] **Step 6.1: Write the failing test**

Create `apps/web/src/__tests__/components/dashboard/DashboardStatsRow.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { DashboardStatsRow } from '@/components/dashboard/DashboardStatsRow';

const baseStats = {
  games: { value: 5, isLoading: false, isError: false, isFetching: false },
  sessions: { value: 2, isLoading: false, isError: false, isFetching: false },
  agents: { value: 1, isLoading: false, isError: false, isFetching: false },
  events: { value: 3, isLoading: false, isError: false, isFetching: false },
};

describe('DashboardStatsRow', () => {
  it('renders 4 stat cards in correct order: game, session, agent, event', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    const links = screen.getAllByRole('link');
    expect(links).toHaveLength(4);
    expect(links[0]).toHaveAttribute('data-entity', 'game');
    expect(links[1]).toHaveAttribute('data-entity', 'session');
    expect(links[2]).toHaveAttribute('data-entity', 'agent');
    expect(links[3]).toHaveAttribute('data-entity', 'event');
  });

  it('wrapper has nav role with aria-label', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    expect(screen.getByRole('navigation', { name: 'Statistiche personali' })).toBeInTheDocument();
  });

  it('renders the Italian labels', () => {
    render(<DashboardStatsRow stats={baseStats} onRetry={{}} />);
    expect(screen.getByText('Giochi')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('Agenti')).toBeInTheDocument();
    expect(screen.getByText('Eventi')).toBeInTheDocument();
  });

  it('per-key isError shows "—" only for that card', () => {
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ sessions: vi.fn() }}
      />
    );
    expect(screen.getByText('—')).toBeInTheDocument();
    // games still shows 5
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('does NOT show "Riprova tutto" banner when only 1 card errored', () => {
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ sessions: vi.fn() }}
      />
    );
    expect(screen.queryByRole('button', { name: /Riprova tutto/i })).not.toBeInTheDocument();
  });

  it('does NOT show "Riprova tutto" banner when 2 cards errored', () => {
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          games: { value: 0, isLoading: false, isError: true, isFetching: false },
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ games: vi.fn(), sessions: vi.fn() }}
      />
    );
    expect(screen.queryByText(/Connessione instabile/i)).not.toBeInTheDocument();
  });

  it('shows "Riprova tutto" banner when 3 or more cards errored', () => {
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          games: { value: 0, isLoading: false, isError: true, isFetching: false },
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
          agents: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ games: vi.fn(), sessions: vi.fn(), agents: vi.fn() }}
      />
    );
    expect(screen.getByText(/Connessione instabile/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Riprova tutto/i })).toBeInTheDocument();
  });

  it('"Riprova tutto" calls all error onRetry callbacks', () => {
    const gameRetry = vi.fn();
    const sessionRetry = vi.fn();
    const agentRetry = vi.fn();
    render(
      <DashboardStatsRow
        stats={{
          ...baseStats,
          games: { value: 0, isLoading: false, isError: true, isFetching: false },
          sessions: { value: 0, isLoading: false, isError: true, isFetching: false },
          agents: { value: 0, isLoading: false, isError: true, isFetching: false },
        }}
        onRetry={{ games: gameRetry, sessions: sessionRetry, agents: agentRetry }}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Riprova tutto/i }));
    expect(gameRetry).toHaveBeenCalledOnce();
    expect(sessionRetry).toHaveBeenCalledOnce();
    expect(agentRetry).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 6.2: Run test, expect failure**

```bash
pnpm test src/__tests__/components/dashboard/DashboardStatsRow.test.tsx
```

Expected: FAIL — module not found.

- [ ] **Step 6.3: Implement `DashboardStatsRow`**

Create `apps/web/src/components/dashboard/DashboardStatsRow.tsx`:

```tsx
'use client';

import { DashboardStatCard } from './DashboardStatCard';

export interface DashboardStatState {
  value: number;
  isLoading: boolean;
  isError: boolean;
  isFetching: boolean;
}

export interface DashboardStatsRowProps {
  stats: {
    games: DashboardStatState;
    sessions: DashboardStatState;
    agents: DashboardStatState;
    events: DashboardStatState;
  };
  onRetry: Partial<{
    games: () => void;
    sessions: () => void;
    agents: () => void;
    events: () => void;
  }>;
}

const STAT_ENTRIES = [
  { key: 'games' as const, entity: 'game' as const, label: 'Giochi', href: '/library' },
  { key: 'sessions' as const, entity: 'session' as const, label: 'Sessioni', href: '/sessions' },
  { key: 'agents' as const, entity: 'agent' as const, label: 'Agenti', href: '/agents' },
  { key: 'events' as const, entity: 'event' as const, label: 'Eventi', href: '/game-nights' },
];

const ERROR_THRESHOLD = 3;

export function DashboardStatsRow({ stats, onRetry }: DashboardStatsRowProps) {
  const errorCount = STAT_ENTRIES.filter(e => stats[e.key].isError).length;
  const showBanner = errorCount >= ERROR_THRESHOLD;

  const handleRetryAll = () => {
    STAT_ENTRIES.forEach(({ key }) => {
      if (stats[key].isError) {
        onRetry[key]?.();
      }
    });
  };

  return (
    <nav aria-label="Statistiche personali" className="mb-12">
      {showBanner && (
        <div
          role="status"
          className="mb-3 flex items-center justify-between rounded-lg border border-border bg-muted px-4 py-2 text-sm"
        >
          <span className="font-medium text-foreground">
            Connessione instabile — alcuni dati non sono disponibili
          </span>
          <button
            type="button"
            onClick={handleRetryAll}
            className="rounded-full bg-foreground/10 px-3 py-1 text-xs font-bold text-foreground hover:bg-foreground/20"
          >
            Riprova tutto
          </button>
        </div>
      )}
      <div className="grid grid-cols-1 gap-4 min-[480px]:grid-cols-2 min-[800px]:grid-cols-4">
        {STAT_ENTRIES.map(({ key, entity, label, href }) => {
          const state = stats[key];
          return (
            <DashboardStatCard
              key={key}
              entity={entity}
              value={state.value}
              label={label}
              href={href}
              isLoading={state.isLoading}
              isError={state.isError}
              isFetching={state.isFetching}
              onRetry={onRetry[key]}
            />
          );
        })}
      </div>
    </nav>
  );
}
```

- [ ] **Step 6.4: Run test, expect pass**

```bash
pnpm test src/__tests__/components/dashboard/DashboardStatsRow.test.tsx
```

Expected: all 8 tests PASS.

- [ ] **Step 6.5: Run token lint + typecheck**

```bash
pnpm lint:tokens -- src/components/dashboard
pnpm typecheck
```

Expected: 0 violations, 0 type errors.

- [ ] **Step 6.6: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardStatsRow.tsx apps/web/src/__tests__/components/dashboard/DashboardStatsRow.test.tsx
git commit -m "feat(dashboard): add DashboardStatsRow with ≥3 error banner threshold"
```

---

## Task 7: Build `DashboardHero` (TDD)

**Files:**
- Create: `apps/web/src/components/dashboard/DashboardHero.tsx`
- Create: `apps/web/src/__tests__/components/dashboard/DashboardHero.test.tsx`

- [ ] **Step 7.1: Write the failing test**

Create `apps/web/src/__tests__/components/dashboard/DashboardHero.test.tsx`:

```tsx
import { render, screen, act } from '@testing-library/react';
import { describe, it, expect, beforeAll, afterAll, vi } from 'vitest';
import { DashboardHero } from '@/components/dashboard/DashboardHero';

describe('DashboardHero', () => {
  beforeAll(() => {
    // Stabilize the date for kicker assertions: 12 May 2026 = Tuesday
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-05-12T10:00:00Z'));
  });

  afterAll(() => {
    // Critical: restore real timers to avoid leakage into other suites in the same worker.
    vi.useRealTimers();
  });

  it('renders single h1 with displayName', () => {
    render(<DashboardHero displayName="Marco" />);
    const headings = screen.getAllByRole('heading', { level: 1 });
    expect(headings).toHaveLength(1);
    expect(headings[0]).toHaveTextContent('Marco');
  });

  it('gradient mark wraps user name only', () => {
    render(<DashboardHero displayName="Marco" />);
    const mark = screen.getByText('Marco');
    expect(mark.tagName.toLowerCase()).toBe('span');
    expect(mark).toHaveClass('hero-mark');
  });

  it('CTAs link to /sessions/new and /library?action=add', () => {
    render(<DashboardHero displayName="x" />);
    expect(screen.getByRole('link', { name: /Nuova sessione/i })).toHaveAttribute(
      'href',
      '/sessions/new'
    );
    expect(screen.getByRole('link', { name: /Aggiungi gioco/i })).toHaveAttribute(
      'href',
      '/library?action=add'
    );
  });

  it('kicker is empty on first render (SSR-safe), populates after useEffect', async () => {
    const { container } = render(<DashboardHero displayName="x" />);
    const kicker = container.querySelector('[data-testid="hero-kicker"]');
    expect(kicker).toBeInTheDocument();
    // Allow useEffect to flush
    await act(async () => {
      await Promise.resolve();
    });
    // Italian weekday for 2026-05-12 (Tuesday) is "martedì"
    expect(kicker?.textContent?.toLowerCase()).toMatch(/benvenuto/);
    expect(kicker?.textContent?.toLowerCase()).toMatch(/martedì/);
  });

  it('kicker has aria-hidden=true', () => {
    render(<DashboardHero displayName="x" />);
    const kicker = screen.getByTestId('hero-kicker');
    expect(kicker).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders lead text', () => {
    render(<DashboardHero displayName="x" />);
    expect(screen.getByText(/La tua tavola da gioco/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 7.2: Run test, expect failure**

```bash
pnpm test src/__tests__/components/dashboard/DashboardHero.test.tsx
```

Expected: FAIL — module not found.

- [ ] **Step 7.3: Implement `DashboardHero`**

Create `apps/web/src/components/dashboard/DashboardHero.tsx`:

```tsx
'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';

export interface DashboardHeroProps {
  displayName: string;
}

function formatKicker(now: Date): string {
  const weekday = now.toLocaleDateString('it-IT', { weekday: 'long' });
  const day = now.toLocaleDateString('it-IT', { day: 'numeric', month: 'long' });
  return `BENVENUTO · ${weekday.toUpperCase()} ${day.toUpperCase()}`;
}

export function DashboardHero({ displayName }: DashboardHeroProps) {
  // Initialize empty to avoid hydration mismatch (server cannot reliably
  // produce the same locale-formatted string as the client).
  const [kicker, setKicker] = useState('');

  useEffect(() => {
    setKicker(formatKicker(new Date()));
  }, []);

  return (
    <section className="mx-auto w-full max-w-[1200px] px-6 pb-8 pt-16">
      <div
        data-testid="hero-kicker"
        aria-hidden="true"
        className="mb-3 font-mono text-xs font-bold uppercase tracking-[0.12em] text-muted-foreground"
      >
        {kicker || ' '}
      </div>
      <h1 className="mb-4 font-quicksand text-[clamp(32px,5vw,48px)] font-bold leading-tight tracking-tight text-foreground">
        Ciao,{' '}
        <span
          className="hero-mark bg-gradient-to-r from-[hsl(var(--c-game))] via-[hsl(var(--c-event))] to-[hsl(var(--c-player))] bg-clip-text text-transparent"
        >
          {displayName}
        </span>
      </h1>
      <p className="max-w-[680px] text-[17px] leading-snug text-muted-foreground">
        La tua tavola da gioco di oggi — riprendi una sessione, sfoglia la libreria, attiva un agente.
      </p>
      <div className="mt-5 flex gap-2">
        <Link
          href="/sessions/new"
          className="inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-game))] px-5 py-2 font-quicksand text-xs font-bold text-white shadow-sm transition-transform hover:scale-[1.03]"
        >
          + Nuova sessione
        </Link>
        <Link
          href="/library?action=add"
          className="inline-flex items-center gap-1 rounded-full border border-border bg-card px-5 py-2 font-quicksand text-xs font-bold text-foreground transition-colors hover:bg-muted"
        >
          + Aggiungi gioco
        </Link>
      </div>
    </section>
  );
}
```

- [ ] **Step 7.4: Run test, expect pass**

```bash
pnpm test src/__tests__/components/dashboard/DashboardHero.test.tsx
```

Expected: all 6 tests PASS.

- [ ] **Step 7.5: Run token lint**

```bash
pnpm lint:tokens -- src/components/dashboard/DashboardHero.tsx
```

Expected: 0 violations. `text-white` allowed on gradient/entity-colored bg per DS-15 exemption.

- [ ] **Step 7.6: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardHero.tsx apps/web/src/__tests__/components/dashboard/DashboardHero.test.tsx
git commit -m "feat(dashboard): add DashboardHero with gradient mark + SSR-safe kicker"
```

---

## Task 8: A11y test suite (jest-axe)

**Files:**
- Create: `apps/web/src/__tests__/components/dashboard/a11y.test.tsx`

- [ ] **Step 8.1: Verify jest-axe is installed**

```bash
cd apps/web
grep '"jest-axe"' package.json
```

Expected: jest-axe present in devDependencies. If not present, install:

```bash
pnpm add -D jest-axe @types/jest-axe
```

- [ ] **Step 8.2: Write the a11y test**

Create `apps/web/src/__tests__/components/dashboard/a11y.test.tsx`:

```tsx
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, it, expect } from 'vitest';
import { DashboardHero } from '@/components/dashboard/DashboardHero';
import { DashboardStatCard } from '@/components/dashboard/DashboardStatCard';
import { DashboardStatsRow } from '@/components/dashboard/DashboardStatsRow';
import { EmptyCTA } from '@/components/dashboard/EmptyCTA';
import { DiscoverCarousel } from '@/components/ui/data-display/discover-carousel';

expect.extend(toHaveNoViolations);

describe('dashboard a11y', () => {
  it('DashboardHero has no axe violations', async () => {
    const { container } = render(<DashboardHero displayName="Marco" />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DashboardStatCard (loaded) has no axe violations', async () => {
    const { container } = render(
      <DashboardStatCard entity="game" value={5} label="Giochi" href="/library" />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DashboardStatCard (loading) has no axe violations', async () => {
    const { container } = render(
      <DashboardStatCard entity="game" value={0} label="Giochi" href="/library" isLoading />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DashboardStatCard (error) has no axe violations', async () => {
    const { container } = render(
      <DashboardStatCard
        entity="game"
        value={0}
        label="Giochi"
        href="/library"
        isError
        onRetry={() => {}}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DashboardStatsRow has no axe violations', async () => {
    const state = { value: 1, isLoading: false, isError: false, isFetching: false };
    const { container } = render(
      <DashboardStatsRow
        stats={{ games: state, sessions: state, agents: state, events: state }}
        onRetry={{}}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('EmptyCTA has no axe violations', async () => {
    const { container } = render(
      <EmptyCTA
        entity="session"
        icon="🎯"
        title="Nessuna sessione"
        sub="Crea una nuova partita."
        actions={[{ label: 'Crea', href: '/sessions/new', primary: true }]}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('DiscoverCarousel has no axe violations', async () => {
    const { container } = render(
      <DiscoverCarousel ariaLabel="Carosello test">
        <div>Item 1</div>
        <div>Item 2</div>
      </DiscoverCarousel>
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 8.3: Run a11y test**

```bash
pnpm test src/__tests__/components/dashboard/a11y.test.tsx
```

Expected: all 7 tests PASS, 0 violations.

- [ ] **Step 8.4: If violations occur**

For each violation, fix the offending component using the rule guidance. Common fixes:
- Missing label → add `aria-label`
- Heading order → wrap in `<h2>` if rendered as standalone test (not applicable to atoms — re-check test context)
- Color contrast → defer to focus-ring contrast measurement step in Task 14

- [ ] **Step 8.5: Commit**

```bash
git add apps/web/src/__tests__/components/dashboard/a11y.test.tsx
git commit -m "test(dashboard): add jest-axe a11y suite for 5 new dashboard components"
```

---

## Task 9: Refactor `EntityZone` (badge swap + useId)

**Files:**
- Modify: `apps/web/src/components/dashboard/EntityZone.tsx`
- Modify: `apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx`

- [ ] **Step 9.1: Read existing test to understand current assertions**

Read: `apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx`

Note current selectors: `getByTestId('entity-dot')`.

- [ ] **Step 9.2: Update existing test for new badge + a11y semantics**

Open `apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx` and replace `data-testid="entity-dot"` references with `data-testid="entity-badge"`. Add new tests for `aria-labelledby`. The exact diff depends on existing content — apply this pattern:

```tsx
// Replace this:
expect(screen.getByTestId('entity-dot')).toBeInTheDocument();

// With this:
expect(screen.getByTestId('entity-badge')).toBeInTheDocument();
expect(screen.getByTestId('entity-badge')).toHaveAttribute('role', 'presentation');
```

Add these new test cases at the end of the describe block:

```tsx
it('title is h2 with a useId-generated id', () => {
  render(
    <EntityZone entity="game" title="Giochi" count={3}>
      <span>x</span>
    </EntityZone>
  );
  const heading = screen.getByRole('heading', { level: 2, name: 'Giochi' });
  expect(heading).toHaveAttribute('id');
  expect(heading.id).toMatch(/.+/);
});

it('section aria-labelledby points to the title id', () => {
  const { container } = render(
    <EntityZone entity="game" title="Giochi" count={3}>
      <span>x</span>
    </EntityZone>
  );
  const section = container.querySelector('section');
  const heading = screen.getByRole('heading', { level: 2 });
  expect(section).toHaveAttribute('aria-labelledby', heading.id);
});

it('count is tabular-nums', () => {
  const { container } = render(
    <EntityZone entity="game" title="Giochi" count={42}>
      <span>x</span>
    </EntityZone>
  );
  const count = container.querySelector('[data-testid="entity-count"]');
  expect(count).toBeInTheDocument();
  expect(count).toHaveClass('tabular-nums');
});

it('view-all link uses entity color via inline style or class', () => {
  render(
    <EntityZone entity="session" title="Sessioni" count={1} viewAllHref="/sessions">
      <span>x</span>
    </EntityZone>
  );
  const link = screen.getByRole('link', { name: /Vedi tutti/i });
  expect(link).toHaveAttribute('aria-label', expect.stringContaining('Sessioni'));
});
```

- [ ] **Step 9.3: Run test, expect failure**

```bash
pnpm test src/__tests__/components/dashboard/EntityZone.test.tsx
```

Expected: FAIL — current implementation uses `entity-dot` and no `aria-labelledby`.

- [ ] **Step 9.4: Update `EntityZone.tsx`**

Replace the entire content of `apps/web/src/components/dashboard/EntityZone.tsx` with:

```tsx
'use client';

import { useId, type ReactNode } from 'react';
import Link from 'next/link';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

interface EntityZoneProps {
  entity: MeepleEntityType;
  title: string;
  count: number;
  viewAllHref?: string;
  children: ReactNode;
}

export function EntityZone({ entity, title, count, viewAllHref, children }: EntityZoneProps) {
  const titleId = useId();

  return (
    <section className={`e-${entity} space-y-3`} aria-labelledby={titleId}>
      <div className="flex items-center gap-2.5 pb-3">
        <span
          data-testid="entity-badge"
          role="presentation"
          className="inline-flex items-center gap-1 rounded-md bg-[hsl(var(--e)/0.12)] px-2 py-0.5 font-quicksand text-[11px] font-extrabold uppercase tracking-wider text-[hsl(var(--e))]"
        >
          {entity}
        </span>
        <h2
          id={titleId}
          className="font-quicksand text-xl font-bold text-foreground"
        >
          {title}
        </h2>
        <span
          data-testid="entity-count"
          className="font-mono text-xs tabular-nums text-muted-foreground"
        >
          {count}
        </span>
        {viewAllHref && (
          <Link
            href={viewAllHref}
            aria-label={`Vedi tutti i ${title}, vai a ${viewAllHref}`}
            className="ml-auto font-quicksand text-sm font-bold text-[hsl(var(--e))] hover:underline"
          >
            Vedi tutti →
          </Link>
        )}
      </div>
      {children}
    </section>
  );
}
```

- [ ] **Step 9.5: Run test, expect pass**

```bash
pnpm test src/__tests__/components/dashboard/EntityZone.test.tsx
```

Expected: all tests PASS.

- [ ] **Step 9.6: Run typecheck + token lint**

```bash
pnpm typecheck
pnpm lint:tokens -- src/components/dashboard/EntityZone.tsx
```

Expected: 0 errors, 0 violations.

- [ ] **Step 9.7: Commit**

```bash
git add apps/web/src/components/dashboard/EntityZone.tsx apps/web/src/__tests__/components/dashboard/EntityZone.test.tsx
git commit -m "refactor(dashboard): EntityZone uses entity badge + useId aria-labelledby"
```

---

## Task 10: Refactor `DashboardClient` (compose new components)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx`
- Modify: `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx`

- [ ] **Step 10.1: Read current DashboardClient test**

Read: `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx`

Inventory of test cases (lock-in expectations before mutation).

- [ ] **Step 10.2: Update DashboardClient test for new tree**

Open the test file and:

1. Replace any `GreetingStrip` import with `DashboardHero`.
2. Replace any assertion querying `greeting-avatar` testid with assertion for `DashboardHero` h1.
3. Add new tests:

```tsx
it('renders DashboardHero h1 with user name', async () => {
  // (use existing render helper with auth mock)
  expect(await screen.findByRole('heading', { level: 1 })).toBeInTheDocument();
});

it('renders DashboardStatsRow with 4 stat cards', async () => {
  const nav = await screen.findByRole('navigation', { name: 'Statistiche personali' });
  expect(nav).toBeInTheDocument();
  // StatCard renders as <Link> in normal state and as <button> in error state.
  // Count by data-entity attribute (present in both forms) instead of role.
  const cards = nav.querySelectorAll('[data-entity]');
  expect(cards).toHaveLength(4);
});

it('renders Sessions zone wrapped in DiscoverCarousel', async () => {
  expect(await screen.findByRole('region', { name: /Carosello sessioni/i })).toBeInTheDocument();
});

it('Toolkit zone does not render HubLayout search input', async () => {
  await screen.findByRole('heading', { name: 'Strumenti' });
  // ToolkitGrid is rendered without HubLayout's search
  expect(screen.queryByPlaceholderText('Cerca strumenti...')).not.toBeInTheDocument();
});

it('does NOT render any GreetingStrip element (anti-regression)', async () => {
  await screen.findByRole('heading', { level: 1 });
  expect(screen.queryByTestId('greeting-avatar')).not.toBeInTheDocument();
});
```

- [ ] **Step 10.3: Read current `DashboardClient.tsx` and make a plan for edits**

Open `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx`. The refactor involves:

a) Replace imports:
- Remove: `import { GreetingStrip } from '@/components/dashboard/GreetingStrip';`
- Add: `import { DashboardHero } from '@/components/dashboard/DashboardHero';`
- Add: `import { DashboardStatsRow } from '@/components/dashboard/DashboardStatsRow';`
- Add: `import { EmptyCTA } from '@/components/dashboard/EmptyCTA';`
- Add: `import { DiscoverCarousel } from '@/components/ui/data-display/discover-carousel';`
- Add: `import { useLibraryStats } from '@/hooks/queries/useLibrary';`

b) Remove inline `EmptyCTA` function definition (lines 80-120 approximately). It is now imported.

c) Add hook calls inside `DashboardClient`. **All four hooks are standard React Query hooks exposing `isError`/`isFetching`/`refetch`** (verified: `useLibrary`, `useActiveSessions`, `useAgents`, `useUpcomingGameNights` all return `UseQueryResult<…, Error>`):

```ts
const {
  data: libraryStats,
  isLoading: statsLoading,
  isError: statsError,
  isFetching: statsFetching,
  refetch: refetchStats,
} = useLibraryStats();

// useActiveSessions and useAgents are already destructured for { data, isLoading } in the current code.
// Extend the destructuring to also grab isError / isFetching / refetch.
const {
  data: sessionsData,
  isLoading: sessionsLoading,
  isError: sessionsError,
  isFetching: sessionsFetching,
  refetch: refetchSessions,
} = useActiveSessions();

const {
  data: agentsData,
  isLoading: agentsLoading,
  isError: agentsError,
  isFetching: agentsFetching,
  refetch: refetchAgents,
} = useAgents({ activeOnly: false });

const {
  data: upcomingNights,
  isLoading: upcomingLoading,
  isError: upcomingError,
  isFetching: upcomingFetching,
  refetch: refetchUpcoming,
} = useUpcomingGameNights({ retry: 1 });
```

d) Compute `statsRowProps`:

```ts
const statsRowProps = useMemo(() => ({
  stats: {
    games: {
      value: libraryStats?.totalGames ?? 0,
      isLoading: statsLoading,
      isError: statsError,
      isFetching: statsFetching,
    },
    sessions: {
      value: sessionItems.length,
      isLoading: sessionsLoading,
      isError: sessionsError,
      isFetching: sessionsFetching,
    },
    agents: {
      value: agentItems.length,
      isLoading: agentsLoading,
      isError: agentsError,
      isFetching: agentsFetching,
    },
    events: {
      value: upcomingNights?.length ?? 0,
      isLoading: upcomingLoading,
      isError: upcomingError,
      isFetching: upcomingFetching,
    },
  },
  onRetry: {
    games:    () => { refetchStats();    },
    sessions: () => { refetchSessions(); },
    agents:   () => { refetchAgents();   },
    events:   () => { refetchUpcoming(); },
  },
}), [
  libraryStats, statsLoading, statsError, statsFetching,
  sessionItems.length, sessionsLoading, sessionsError, sessionsFetching,
  agentItems.length, agentsLoading, agentsError, agentsFetching,
  upcomingNights, upcomingLoading, upcomingError, upcomingFetching,
  refetchStats, refetchSessions, refetchAgents, refetchUpcoming,
]);
```

> **Special-case `events` failure** per spec D15: `useUpcomingGameNights` errors are NOT shown to the user via toast (events is secondary). The StatCard still renders "—" with retry button (consistent UX), but no global side effect. This is enforced naturally because we never emit a sonner toast on retry — the retry is a quiet `refetch()` call.

e) Replace the JSX `<GreetingStrip displayName={displayName} stats={stats} />` with:

```tsx
<DashboardHero displayName={displayName} />
<DashboardStatsRow {...statsRowProps} />
```

f) Wrap the Sessions zone children in `DiscoverCarousel`:

```tsx
<EntityZone entity="session" title="Sessioni" count={sessionItems.length} viewAllHref="/sessions">
  <HubLayout
    searchPlaceholder="Filtra per stato..."
    searchValue={sessionsSearch}
    onSearchChange={setSessionsSearch}
    filterChips={SESSIONS_FILTERS}
    activeFilterId={sessionsFilter}
    onFilterChange={setSessionsFilter}
  >
    {sessionsLoading ? (
      <LoadingSkeleton count={4} />
    ) : filteredSessionItems.length === 0 ? (
      <EmptyCTA
        entity="session"
        icon="🎯"
        title="Nessuna sessione"
        sub="Inizia una nuova partita e traccia i tuoi progressi in tempo reale."
        actions={[{ label: '+ Crea sessione', href: '/sessions/new', primary: true }]}
      />
    ) : (
      <DiscoverCarousel ariaLabel="Carosello sessioni attive" itemWidth={260} gap={14}>
        {filteredSessionItems.map(item => (
          <div key={item.id ?? item.title} className="w-[260px] shrink-0">
            <MeepleCard {...item} />
          </div>
        ))}
      </DiscoverCarousel>
    )}
  </HubLayout>
</EntityZone>
```

g) Update Agents `EmptyCTA` usage (was inline) to use the new component:

```tsx
emptyNode={
  <EmptyCTA
    entity="agent"
    icon="🤖"
    title="Nessun agente attivo"
    sub="Avvia una chat con un agente AI per ricevere aiuto durante la partita."
    actions={[
      { label: '💬 Avvia chat', href: '/chat', primary: true },
      { label: '+ Crea agente', href: '/agents/new' },
    ]}
  />
}
```

h) Drop HubLayout from Toolkit zone:

```tsx
<EntityZone entity="toolkit" title="Strumenti" count={TOOLKIT_TOOLS.length}>
  <ToolkitGrid />
</EntityZone>
```

(Already was direct, no HubLayout. Verify.)

i) Remove the old `GreetingStrip` import line and the inline `EmptyCTA` function.

- [ ] **Step 10.4: Apply edits step by step**

Use the Edit tool for each transformation listed in Step 10.3. After each set of related edits, run typecheck.

- [ ] **Step 10.5: Run DashboardClient tests**

```powershell
# PowerShell: use single-quotes around the path (parentheses are PS operators)
pnpm test 'src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx'
```

```bash
# bash equivalent (Git Bash, WSL): escape parentheses
pnpm test 'src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx'
```

Expected: all assertions PASS (including new ones).

- [ ] **Step 10.6: Run full unit suite**

```bash
pnpm test
```

Expected: all PASS.

- [ ] **Step 10.7: Run typecheck + token lint**

```bash
pnpm typecheck
pnpm lint:tokens
```

Expected: 0 errors, 0 violations on changed files.

- [ ] **Step 10.8: Commit**

```powershell
# PowerShell-safe (single-quoted path)
git add 'apps/web/src/app/(authenticated)/dashboard/'
git commit -m "refactor(dashboard): compose new Hero/StatsRow/DiscoverCarousel/EmptyCTA"
```

---

## Task 11: Delete `GreetingStrip` (cleanup)

**Files:**
- Delete: `apps/web/src/components/dashboard/GreetingStrip.tsx`
- Delete: `apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx`

- [ ] **Step 11.1: Verify no remaining imports**

```bash
cd apps/web
grep -rn "GreetingStrip" src/ tests/ --include="*.tsx" --include="*.ts"
grep -rn "greeting-avatar" src/ tests/ --include="*.tsx" --include="*.ts"
```

Expected: only the two files-to-delete should match.

- [ ] **Step 11.2: Delete the two files**

```bash
git rm apps/web/src/components/dashboard/GreetingStrip.tsx
git rm apps/web/src/__tests__/components/dashboard/GreetingStrip.test.tsx
```

- [ ] **Step 11.3: Re-run grep to confirm zero hits**

```bash
grep -rn "GreetingStrip" apps/web/src apps/web/tests --include="*.tsx" --include="*.ts" || echo "OK no hits"
grep -rn "greeting-avatar" apps/web/src apps/web/tests --include="*.tsx" --include="*.ts" || echo "OK no hits"
```

Expected: "OK no hits" twice.

- [ ] **Step 11.4: Run full test suite**

```bash
pnpm test
pnpm typecheck
```

Expected: all PASS, 0 type errors.

- [ ] **Step 11.5: Commit**

```bash
git add -A
git commit -m "chore(dashboard): remove obsolete GreetingStrip component and tests"
```

---

## Task 12: Rewrite obsolete `dashboard.spec.ts` (Playwright e2e)

**Files:**
- Modify (full rewrite): `apps/web/tests/e2e/dashboard.spec.ts`

- [ ] **Step 12.0: Verify auth-cookie pattern used by existing e2e suites**

Before rewriting, lock in the working auth-cookie shape used by sibling e2e tests:

```powershell
# Find how other e2e tests set up auth
Select-String -Path "apps/web/tests/e2e/*.spec.ts" -Pattern "addCookies|meepleai_session|next-auth" | Select-Object -First 20
```

Match the **exact** cookie name, value, and `sameSite`/`secure` options used by a passing peer test (e.g., `chat-page.spec.ts`, `admin-reports.spec.ts`). If the project uses a `loginAsTestUser` helper, use that instead of inline `addCookies`. Document the chosen pattern (helper or inline) in a scratch note. If the mock cookie pattern from the **old** `dashboard.spec.ts` differs from peer tests, prefer the peer pattern.

- [ ] **Step 12.1: Delete stale snapshot baselines (if any)**

```bash
ls apps/web/tests/e2e/__screenshots__/dashboard* 2>/dev/null || echo "none"
rm -f apps/web/tests/e2e/__screenshots__/dashboard*
```

- [ ] **Step 12.2: Replace `dashboard.spec.ts` content**

Replace the entire content of `apps/web/tests/e2e/dashboard.spec.ts` with:

```ts
/**
 * Dashboard Page E2E Tests — Gaming Hub restyle
 *
 * Spec: docs/for-developers/specs/2026-05-12-dashboard-restyle-design.md
 *
 * Coverage:
 * - Authentication & middleware
 * - Hero, StatsRow, EntityZones, DiscoverCarousel, ToolkitGrid rendering
 * - Loading + empty + error states
 * - Responsive (mobile/tablet/desktop)
 * - Theming (light/dark)
 * - Accessibility (axe + keyboard navigation)
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

const AUTH_COOKIE = {
  name: 'meepleai_session',
  value: 'mock-session-token',
  domain: 'localhost',
  path: '/',
  httpOnly: true,
  secure: false,
  sameSite: 'Lax' as const,
};

test.describe('Dashboard — Auth & middleware', () => {
  test('redirects unauthenticated users to /login', async ({ page, context }) => {
    await context.clearCookies();
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/login\?from=%2Fdashboard/);
  });

  test('allows authenticated access', async ({ page, context }) => {
    await context.addCookies([AUTH_COOKIE]);
    await page.goto('/dashboard');
    await expect(page).toHaveURL('/dashboard');
  });
});

test.describe('Dashboard — Component rendering', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([AUTH_COOKIE]);
  });

  test('DashboardHero h1 with user name visible', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { level: 1 })).toContainText(/Ciao,/);
  });

  test('DashboardStatsRow renders 4 entity-tagged stat cards', async ({ page }) => {
    await page.goto('/dashboard');
    const nav = page.getByRole('navigation', { name: 'Statistiche personali' });
    await expect(nav).toBeVisible();
    const cards = nav.locator('a[data-entity]');
    await expect(cards).toHaveCount(4);
  });

  test('each EntityZone has aria-labelledby on its section', async ({ page }) => {
    await page.goto('/dashboard');
    const sections = page.locator('section[aria-labelledby]');
    await expect(sections.first()).toBeVisible();
    // at least 4 entity zones
    expect(await sections.count()).toBeGreaterThanOrEqual(4);
  });

  test('Sessions zone renders DiscoverCarousel region', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.getByRole('region', { name: /Carosello sessioni/i })).toBeVisible();
  });

  test('Toolkit zone renders without HubLayout search input', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { level: 2, name: 'Strumenti' })).toBeVisible();
    // Toolkit zone should not contain a search placeholder
    const toolkitSection = page.locator('section', { hasText: 'Strumenti' });
    await expect(toolkitSection.locator('input[placeholder*="Cerca"]')).toHaveCount(0);
  });
});

test.describe('Dashboard — Responsive', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([AUTH_COOKIE]);
  });

  test('mobile 375×667: stat-row collapses', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');
    const nav = page.getByRole('navigation', { name: 'Statistiche personali' });
    await expect(nav).toBeVisible();
  });

  test('desktop 1280×800: stat-row 4-col', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/dashboard');
    const nav = page.getByRole('navigation', { name: 'Statistiche personali' });
    await expect(nav).toBeVisible();
  });
});

test.describe('Dashboard — Accessibility', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([AUTH_COOKIE]);
  });

  test('axe: light theme has no violations', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForSelector('h1');
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();
    expect(results.violations).toEqual([]);
  });

  test('axe: dark theme has no violations', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForSelector('h1');
    // Toggle dark via next-themes data-theme attr
    await page.evaluate(() => {
      document.documentElement.setAttribute('data-theme', 'dark');
      document.documentElement.classList.add('dark');
    });
    await page.waitForTimeout(300);
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();
    expect(results.violations).toEqual([]);
  });

  test('keyboard: Tab order traverses Hero CTAs → StatCards → Zones', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForSelector('h1');
    await page.keyboard.press('Tab');
    const focused1 = await page.evaluate(() => document.activeElement?.tagName);
    expect(['A', 'BUTTON']).toContain(focused1);
    // additional Tab presses should remain on focusable controls
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    const focused3 = await page.evaluate(() => document.activeElement?.tagName);
    expect(['A', 'BUTTON', 'DIV']).toContain(focused3);
  });
});
```

> Note: the test file imports `@axe-core/playwright`. Verify it's installed (it typically is — same project ships axe-core for other suites). If missing:
> ```bash
> pnpm add -D @axe-core/playwright
> ```

- [ ] **Step 12.3: Verify @axe-core/playwright is installed**

```bash
cd apps/web
grep '"@axe-core/playwright"' package.json
```

Expected: present. Install if missing per note.

- [ ] **Step 12.4: Run the e2e suite**

```bash
pnpm test:e2e --grep "Dashboard"
```

Expected: all PASS. If the auth cookie mock no longer works (auth flow changed since the old suite was written), adjust the cookie setup in the test helper or replace with a `loginAsTestUser` page object helper used elsewhere in `tests/e2e/`.

- [ ] **Step 12.5: Update visual baselines if test reports snapshot diffs**

```bash
pnpm test:e2e --grep "Dashboard" --update-snapshots
```

Expected: baselines stored under `apps/web/tests/e2e/__screenshots__/dashboard.spec.ts-snapshots/`.

- [ ] **Step 12.6: Commit**

```bash
git add apps/web/tests/e2e/dashboard.spec.ts apps/web/tests/e2e/__screenshots__/ apps/web/package.json apps/web/pnpm-lock.yaml
git commit -m "test(e2e): rewrite dashboard.spec.ts for Gaming Hub restyle"
```

---

## Task 13: A11y CI workflow audit

**Files:**
- Read: `.github/workflows/*` (audit only — modify only if blocker)

- [ ] **Step 13.1: List frontend-related workflows**

```bash
ls .github/workflows/ | grep -iE "front|web|a11y|axe|test"
```

- [ ] **Step 13.2: For each candidate, grep `continue-on-error`**

```bash
grep -l "continue-on-error" .github/workflows/*.yml .github/workflows/*.yaml 2>/dev/null
```

- [ ] **Step 13.3: For each matching file, inspect**

For each found file, run:

```bash
cat .github/workflows/<filename>
```

Look for the job named like "A11y", "axe", "accessibility", or "Frontend - A11y E2E". If `continue-on-error: true`, document the file path and the job name in a `BLOCKING_NOTES.md` scratch file (do not commit) and propose:

- **Option A** (safe): leave `continue-on-error: true` for this PR; open a follow-up issue to re-enable blocking after a green run on `main-dev`.
- **Option B** (assertive): change to `continue-on-error: false` as part of this PR, accepting that any pre-existing a11y violations outside `/dashboard` will block the merge.

- [ ] **Step 13.4: Decision**

If no A11y workflow with `continue-on-error: true` is found, no action needed — document in PR body "verified A11y CI is already blocking or does not exist as a separate non-blocking job".

If found, default to **Option A** unless the existing pages are known a11y-clean. Document decision in the PR description.

- [ ] **Step 13.5: Commit (only if workflow edited)**

If Option B chosen and a workflow was modified:

```bash
git add .github/workflows/<file>
git commit -m "ci(a11y): restore Frontend A11y E2E job to blocking"
```

Otherwise, no commit.

---

## Task 14: Pre-merge verification + PR prep

**Files:** none (verification only) + PR description in GitHub

- [ ] **Step 14.1: Run full lint suite**

```bash
cd apps/web
pnpm lint --max-warnings 0
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 14.2: Run token lint**

```bash
pnpm lint:tokens
```

Expected: 0 violations on changed files (re-check the report file `audits/2026-05-12-token-violations.md` if any flagged).

- [ ] **Step 14.3: Run typecheck**

```bash
pnpm typecheck
```

Expected: 0 errors.

- [ ] **Step 14.4: Run full test suite with coverage**

```bash
pnpm test --coverage
```

Expected: all PASS, coverage ≥85% on FE (gate).

- [ ] **Step 14.5: Run e2e**

```bash
pnpm test:e2e
```

Expected: all PASS, snapshots stable.

- [ ] **Step 14.6: Build check**

```bash
pnpm build
```

Expected: next build succeeds.

- [ ] **Step 14.7: Focus-ring contrast measurement (manual)**

Start dev server:

```bash
pnpm dev
```

In Chrome DevTools:
1. Navigate to `http://localhost:3000/dashboard` (login first).
2. Inspect a focused `DashboardStatCard` (focus via keyboard Tab).
3. Use Chrome DevTools color picker on the orange focus outline against the cream `#f7f3ee` background.
4. Verify ratio ≥3:1 (WCAG 1.4.11 non-text).
5. Toggle dark theme (next-themes button or `document.documentElement.setAttribute('data-theme','dark')` in console).
6. Repeat measurement on `#14100a` dark background.

Record both measurements in PR description. If either fails, propose a higher-contrast focus token (defer to a follow-up if not blocking).

- [ ] **Step 14.8: Hero gradient mark contrast (manual)**

In the running dev server (light theme):
1. Inspect the H1 user-name span (`.hero-mark`).
2. Use Chrome DevTools to sample the lightest stop color of the gradient (orange `~hsl(25,95%,45%)`).
3. Sample the cream background `#f7f3ee`.
4. Compute contrast ratio. Target: ≥4.5:1 (WCAG 1.4.3 text).
5. If <4.5:1, propose darker entity tint or `text-shadow` fix in follow-up. Document in PR.

- [ ] **Step 14.9: Take before/after screenshots**

1. With the running dev server, capture screenshots in:
   - Light theme, desktop 1280×800
   - Light theme, mobile 375×667
   - Dark theme, desktop 1280×800
   - Dark theme, mobile 375×667

2. Save under `tmp/screenshots/dashboard-{theme}-{viewport}.png` (gitignored). Use these in PR description.

3. Also capture the **before** state by checking out `main-dev` and repeating the captures.

- [ ] **Step 14.10: Push branch**

```bash
git push -u origin feature/dashboard-restyle-mock-fidelity
```

- [ ] **Step 14.11: Create PR with detailed description**

Use `gh pr create` (or web UI). Title:

```
feat(dashboard): restyle Gaming Hub to token-first mock fidelity
```

Body (template):

```markdown
## Summary
Restyles the authenticated user dashboard (`/dashboard`) to maximum visual fidelity with the mock design system (`admin-mockups/design_files/`). Token-first approach (Approach 3 per spec).

**Spec:** `docs/for-developers/specs/2026-05-12-dashboard-restyle-design.md`
**Plan:** `docs/for-developers/plans/2026-05-12-dashboard-restyle.md`

## Changes
- NEW: `DashboardHero` (gradient mark, SSR-safe kicker), `DashboardStatsRow` (4-col with ≥3 error banner threshold), `DashboardStatCard` (entity-tinted, retry-aware), `DiscoverCarousel` (reusable primitive — `components/ui/data-display/`)
- NEW: extracted `EmptyCTA` with entity-token contract (replaces inline `bg-amber-*`)
- MODIFIED: `EntityZone` (entity-tint badge, `useId` aria-labelledby), `DashboardClient` (compose new tree), `useUpcomingGameNights` (accepts retry option)
- DELETED: `GreetingStrip` + test
- REWRITTEN: `tests/e2e/dashboard.spec.ts` (old suite tested obsolete UI)

## Test plan
- [x] `pnpm lint --max-warnings 0`
- [x] `pnpm lint:tokens` (0 violations on changed files)
- [x] `pnpm typecheck`
- [x] `pnpm test --coverage` (FE ≥85%)
- [x] `pnpm test:e2e` (incl. axe light + dark)
- [x] `pnpm build`
- [x] Focus-ring contrast measured (light: __:1, dark: __:1)
- [x] Hero gradient contrast measured (light: __:1)
- [x] Manual smoke test: dev server, all 4 zones, light/dark toggle, mobile/desktop viewports

## Before/After
[Screenshots: light desktop, light mobile, dark desktop, dark mobile]

## Compliance
- ✅ FREEZE (Design System De-versioning #1023): no new files under `components/v2/**` or `components/ui/v2/**`
- ✅ DS-15 token canonicalization: lint passes with 0 violations on changed files
- ✅ A11y AA: jest-axe + Playwright axe pass with 0 violations
- ✅ Reduced motion: tested with `prefers-reduced-motion: reduce`

🤖 Generated with Claude Code
```

- [ ] **Step 14.12: Verify PR is targeting `main-dev`**

```bash
gh pr view --json baseRefName --jq .baseRefName
```

Expected: `main-dev`.

---

## Definition of Done (final check)

Re-confirm all checkpoints from spec §12:

- [ ] 4 new components in canonical paths
- [ ] 1 extracted `EmptyCTA` with entity tokens, no `bg-amber-*` remaining
- [ ] 5 new unit test suites with ≥90% branches
- [ ] 1 new a11y test suite, 0 violations
- [ ] `useUpcomingGameNights` retry option implemented
- [ ] `EntityZone` refactored, `aria-labelledby` on h2 id
- [ ] `DashboardClient` composes new tree
- [ ] `GreetingStrip` deleted; grep returns 0 hits
- [ ] `dashboard.spec.ts` fully rewritten
- [ ] All CI gates green (lint, lint:tokens, typecheck, test, test:coverage, test:e2e, build)
- [ ] Focus-ring contrast ≥3:1 (light + dark) measured
- [ ] Gradient mark contrast ≥4.5:1 (light) measured
- [ ] PR description includes before/after screenshots
- [ ] PR targets `main-dev`
- [ ] A11y CI audit decision documented

---

## Plan self-review log

**Spec coverage check** — every spec section maps to a task:
- §1 Context / §4 Approach → encoded in plan header (Goal, Architecture)
- §5.1 JSX hierarchy → Task 10 (DashboardClient compose)
- §5.2 New files → Tasks 3 (EmptyCTA), 4 (DiscoverCarousel), 5 (StatCard), 6 (StatsRow), 7 (Hero)
- §5.3 Modified files → Task 2 (hook), 9 (EntityZone), 10 (DashboardClient)
- §5.4 Deleted files → Task 11
- §5.5 New tests → integrated into each component task (TDD)
- §6 Visual & token mapping → embedded in implementation steps
- §7 Data flow / error UX → Task 10 (DashboardClient wires hooks); Task 2 (retry option)
- §8 A11y → Task 8 (jest-axe); Task 14.7-14.8 (manual contrast)
- §9 Testing strategy → Tasks 3-8 (unit), Task 12 (e2e)
- §10.1 Implementation order → Tasks 1-14 in order
- §11 CI verifications → Task 14
- §12 Definition of Done → final checklist
- §13 Open questions → Task 13 (A11y CI audit) + Task 14 (contrast checks)

**Placeholder scan**: no TBDs, no TODOs, no "implement later". Each step has either runnable code or runnable command. ✅

**Type consistency check**: 
- `MeepleEntityType` imported consistently from `@/components/ui/data-display/meeple-card` ✅
- `DashboardStatCardProps` (Task 5) → consumed by `DashboardStatsRow` (Task 6) via `STAT_ENTRIES` keying — values match ✅
- `useUpcomingGameNights({ retry: 1 })` (Task 10) → matches signature extended in Task 2 ✅
- `EmptyCTAProps` shape (Task 3) → consumed in Task 10 (Sessions empty + Agents empty) — props match ✅
- `DiscoverCarouselProps` (Task 4) → consumed in Task 10 with `ariaLabel`, `itemWidth`, `gap` ✅

**Fixes applied inline:** none (the plan was written from a reviewed spec).
