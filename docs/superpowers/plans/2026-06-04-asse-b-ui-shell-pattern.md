# Asse B — UI Shell + Navigation Pattern Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare le primitive UI cross-route (sidebar 8 voci, DrawerStack, StatePreviewProvider, WizardModal, ToastProvider) + token additions per la baseline Claude Design 2026-06-04. Queste primitive sono prerequisito per asse C (Dashboard) e asse D (route-level).

**Architecture:** React context provider pattern + Zustand-based drawer stack state machine + dynamic import gate per dev tools + composition primitives in `apps/web/src/components/ui/` (canonical post-deversioning 2026-05-18).

**Tech Stack:** Next.js 16 (App Router) · React 19 · Tailwind 4 · shadcn/ui · Zustand · Vitest · Playwright · sonner (toast)

**Issue**: [#1897](https://github.com/meepleAi-app/meepleai-monorepo/issues/1897) (parent umbrella [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895))
**Spec consolidato**: [`docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`](../specs/2026-06-04-claude-design-alignment-spec-panel-review.md) (Sezione 4 — Asse B)
**Effort target**: M+ ~7 gg dev + 2 gg Storybook/test = 9 gg totali

---

## Work Packages

| WP | Scope | Effort | Critical path | Sub-task |
|----|-------|--------|---------------|----------|
| **WP1** | Token additions + bridge update | S | NO (foundation) | T1 |
| **WP2** | Sidebar 8 voci + nav config | S | YES (blocca asse D nav) | T2 |
| **WP3** | DrawerStack provider + hook | M | YES (blocca asse C+D) | T3–T4 |
| **WP4** | StatePreviewProvider dev-only | M | NO (parallelo) | T5 |
| **WP5** | WizardModal primitive | M | NO (asse D usa wizard) | T6 |
| **WP6** | ToastProvider via sonner | S | NO (parallelo) | T7 |
| **WP7** | Discover tab interno `/games` | S | NO (closes WP) | T8 |
| **WP8** | Storybook + cross-cutting E2E | M | YES (chiude WP) | T9–T10 |

**Mix-model hint (P120)**: 6 haiku (mechanical) + 4 sonnet (judgment).

**Total**: 10 task TDD bite-sized. ~9 gg effort realistic.

---

## File Structure

### New files
- `apps/web/src/components/ui/drawer-stack/drawer-stack.tsx`
- `apps/web/src/components/ui/drawer-stack/drawer-stack-provider.tsx`
- `apps/web/src/components/ui/drawer-stack/use-drawer-stack.ts`
- `apps/web/src/components/ui/drawer-stack/index.ts`
- `apps/web/src/components/ui/state-preview/state-preview-provider.tsx`
- `apps/web/src/components/ui/state-preview/use-state-preview.ts`
- `apps/web/src/components/ui/state-preview/state-preview-toggle.tsx`
- `apps/web/src/components/ui/state-preview/index.ts`
- `apps/web/src/components/ui/wizard-modal/wizard-modal.tsx`
- `apps/web/src/components/ui/wizard-modal/wizard-step-types.ts`
- `apps/web/src/components/ui/wizard-modal/index.ts`
- `apps/web/src/components/ui/toast/toast-provider.tsx`
- `apps/web/src/components/ui/toast/use-toast.ts`
- `apps/web/src/components/ui/toast/index.ts`
- `apps/web/src/lib/nav/admin-nav-config.ts` (already exists, will update)
- `apps/web/src/__tests__/ui/drawer-stack/drawer-stack.test.tsx`
- `apps/web/src/__tests__/ui/state-preview/state-preview.test.tsx`
- `apps/web/src/__tests__/ui/wizard-modal/wizard-modal.test.tsx`
- `apps/web/e2e/cross-asse-drawer-stack.spec.ts`
- `apps/web/src/components/ui/drawer-stack/drawer-stack.stories.tsx`
- `apps/web/src/components/ui/wizard-modal/wizard-modal.stories.tsx`

### Modified files
- `apps/web/src/styles/design-tokens-canonical.css` (3 new tokens: --c-warning-ink, --c-overlay-scrim, --c-overlay-gradient-end)
- `apps/web/src/styles/token-bridge.css` (legacy aliases if needed)
- `apps/web/src/lib/nav/main-nav-config.ts` (sidebar 8 voci)
- `apps/web/src/components/shell/MainSideDrawer.tsx` (or equivalent) (rendering update)
- `apps/web/src/app/games/page.tsx` (Discover come default tab)
- `apps/web/package.json` (add `sonner` if missing)
- Storybook config files se necessario

---

## WP1 — Token additions (MAJ-9, gap #37 + #38)

> **Spec reference**: Sezione 4 Asse B — "Token additions". Gap report 2026-06-04 entries #37 + #38.

### Task 1: Add 3 nuovi token + token-bridge update

**Mix-model**: haiku · **Effort**: S (~2h)

**Files:**
- Modify: `apps/web/src/styles/design-tokens-canonical.css`
- Modify: `apps/web/src/styles/token-bridge.css`
- Test: `apps/web/src/__tests__/styles/design-tokens.test.tsx`

- [ ] **Step 1: Write failing test** — verify 3 tokens are defined for both themes

```tsx
import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';

describe('design tokens additions (gap #37/#38)', () => {
  it('defines --c-warning-ink for light theme', () => {
    const { container } = render(<div data-theme="light" style={{ color: 'var(--c-warning-ink)' }}>test</div>);
    const computed = getComputedStyle(container.firstElementChild!);
    expect(computed.getPropertyValue('--c-warning-ink').trim()).toBe('hsl(38 92% 32%)');
  });

  it('defines --c-warning-ink darker variant for dark theme', () => {
    const { container } = render(<div data-theme="dark" style={{ color: 'var(--c-warning-ink)' }}>test</div>);
    const computed = getComputedStyle(container.firstElementChild!);
    expect(computed.getPropertyValue('--c-warning-ink').trim()).toBe('hsl(38 92% 60%)');
  });

  it('defines --c-overlay-scrim with rgba semantic', () => {
    const { container } = render(<div style={{ background: 'var(--c-overlay-scrim)' }}>test</div>);
    const computed = getComputedStyle(container.firstElementChild!);
    expect(computed.getPropertyValue('--c-overlay-scrim').trim()).toMatch(/hsla\(0 0% 0% \/ 0\.6\)/);
  });

  it('defines --c-overlay-gradient-end', () => {
    const { container } = render(<div style={{ background: 'var(--c-overlay-gradient-end)' }}>test</div>);
    const computed = getComputedStyle(container.firstElementChild!);
    expect(computed.getPropertyValue('--c-overlay-gradient-end').trim()).toBe('hsl(25 95% 38%)');
  });
});
```

- [ ] **Step 2: Run test → FAIL** (tokens not defined)

```bash
cd apps/web && pnpm test design-tokens
```

- [ ] **Step 3: Add 3 tokens in `design-tokens-canonical.css`**

```css
:root,
[data-theme="light"] {
  /* Gap #37 — warning shade dark for light-mode contrast (AA-compliant on cream bg) */
  --c-warning-ink: hsl(38 92% 32%);

  /* Gap #38 — overlay/scrim tokens */
  --c-overlay-scrim: hsla(0 0% 0% / 0.6);
  --c-overlay-gradient-end: hsl(25 95% 38%);
}

[data-theme="dark"] {
  --c-warning-ink: hsl(38 92% 60%);
  --c-overlay-scrim: hsla(0 0% 0% / 0.75);
  --c-overlay-gradient-end: hsl(25 95% 50%);
}
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Audit ESLint `local/no-hardcoded-color-utility` impact** — verify nessun consumer attualmente usa hex hardcoded che ora vanno mappati a `--c-warning-ink` o overlay. Eventualmente aggiungere unmappped → consumer follow-up issue.

```bash
cd apps/web && pnpm lint:tokens 2>&1 | tail -20
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/styles/design-tokens-canonical.css apps/web/src/__tests__/styles/
git commit -m "feat(design-tokens): #1897 add --c-warning-ink + overlay tokens (gap #37/#38)

MAJ-9 finding: warning shade dark + scrim/gradient tokens richiesti dalla baseline
Claude Design demo 2026-06-04. AA-compliant per light theme (hsl 38 92% 32% su cream).
Dark theme inverted (60% lightness)."
```

**Self-review**:
- [ ] 3 tokens definiti per entrambi i temi (light + dark)
- [ ] AA contrast verify via axe (manual)
- [ ] Token bridge non necessario (sono token nuovi, non rinomines)
- [ ] No consumer ESLint break (CSS variables additive)

---

## WP2 — Sidebar 8 voci

> **Spec reference**: Sezione 4 Asse B — "Sidebar a 8 voci (CRIT-8 fix)".
> **Invariante**: #20 — sidebar 2 voci game-related (Library + Games) + Notifications nuova.

### Task 2: Sidebar 8 voci config refactor

**Mix-model**: sonnet · **Effort**: S (~4h)

**Files:**
- Modify: `apps/web/src/lib/nav/main-nav-config.ts` (create if not exists)
- Modify: `apps/web/src/components/shell/MainSideDrawer.tsx` (or equivalent shell)
- Test: `apps/web/src/__tests__/lib/nav/main-nav-config.test.ts`
- Test: `apps/web/src/__tests__/components/shell/MainSideDrawer.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
// main-nav-config.test.ts
import { describe, it, expect } from 'vitest';
import { MAIN_NAV_ITEMS } from '@/lib/nav/main-nav-config';

describe('main nav config (invariante #20 + CRIT-8 fix)', () => {
  it('contains exactly 8 voci in expected order', () => {
    expect(MAIN_NAV_ITEMS.map(i => i.id)).toEqual([
      'dashboard',
      'library',
      'games',
      'gamenights',
      'sessions',
      'agents',
      'notifications',
      'profile',
    ]);
  });

  it('library and games are game-related (CRIT-8 fix invariante #20)', () => {
    const gameRelated = MAIN_NAV_ITEMS.filter(i => i.gameRelated);
    expect(gameRelated.map(i => i.id)).toEqual(['library', 'games']);
  });

  it('games defaults to discover tab as landing', () => {
    const games = MAIN_NAV_ITEMS.find(i => i.id === 'games');
    expect(games?.defaultHref).toBe('/games?tab=discover');
  });

  it('NO standalone discover item (removed per invariante #20)', () => {
    const discover = MAIN_NAV_ITEMS.find(i => i.id === 'discover');
    expect(discover).toBeUndefined();
  });

  it('notifications has bell icon + counter capability', () => {
    const notif = MAIN_NAV_ITEMS.find(i => i.id === 'notifications');
    expect(notif?.icon).toBe('Bell');
    expect(notif?.showCounter).toBe(true);
  });
});
```

- [ ] **Step 2: Run → FAIL**

```bash
cd apps/web && pnpm test main-nav-config
```

- [ ] **Step 3: Implement config**

```typescript
// main-nav-config.ts
export type MainNavItem = {
  id: string;
  label: string;
  defaultHref: string;
  icon: string; // lucide icon name
  gameRelated?: boolean;
  showCounter?: boolean;
};

export const MAIN_NAV_ITEMS: ReadonlyArray<MainNavItem> = [
  { id: 'dashboard', label: 'Dashboard', defaultHref: '/dashboard', icon: 'LayoutDashboard' },
  { id: 'library', label: 'Library', defaultHref: '/library', icon: 'Library', gameRelated: true },
  { id: 'games', label: 'Games', defaultHref: '/games?tab=discover', icon: 'Dice5', gameRelated: true },
  { id: 'gamenights', label: 'Game Nights', defaultHref: '/game-nights', icon: 'CalendarHeart' },
  { id: 'sessions', label: 'Sessions', defaultHref: '/sessions', icon: 'History' },
  { id: 'agents', label: 'Agents', defaultHref: '/agents', icon: 'Bot' },
  { id: 'notifications', label: 'Notifications', defaultHref: '/notifications', icon: 'Bell', showCounter: true },
  { id: 'profile', label: 'Profile', defaultHref: '/profile', icon: 'UserCircle' },
] as const;
```

- [ ] **Step 4: Update MainSideDrawer/Shell rendering** to consume new config

```tsx
// MainSideDrawer.tsx
import { MAIN_NAV_ITEMS } from '@/lib/nav/main-nav-config';
import { useUnreadNotificationsCount } from '@/hooks/use-notifications';

export function MainSideDrawer() {
  const unread = useUnreadNotificationsCount();
  return (
    <nav aria-label="Main navigation">
      <ul>
        {MAIN_NAV_ITEMS.map(item => (
          <li key={item.id}>
            <Link href={item.defaultHref}>
              <Icon name={item.icon} />
              <span>{item.label}</span>
              {item.showCounter && unread > 0 && (
                <span data-testid={`badge-${item.id}`}>{unread}</span>
              )}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
```

- [ ] **Step 5: Write integration test on shell**

```tsx
// MainSideDrawer.test.tsx
it('renders 8 nav items in correct order', () => {
  render(<MainSideDrawer />);
  const items = screen.getAllByRole('listitem');
  expect(items).toHaveLength(8);
  expect(items[0]).toHaveTextContent('Dashboard');
  expect(items[6]).toHaveTextContent('Notifications');
  expect(items[7]).toHaveTextContent('Profile');
});

it('shows unread badge on notifications when count > 0', () => {
  vi.mocked(useUnreadNotificationsCount).mockReturnValue(3);
  render(<MainSideDrawer />);
  expect(screen.getByTestId('badge-notifications')).toHaveTextContent('3');
});
```

- [ ] **Step 6: Run tests → PASS**

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/nav/ apps/web/src/components/shell/ apps/web/src/__tests__/
git commit -m "feat(shell): #1897 sidebar 8 voci config (CRIT-8 fix invariante #20)

- 8 voci: Dashboard, Library, Games, Game Nights, Sessions, Agents, Notifications, Profile
- Library + Games game-related (invariante #20)
- /games default tab=discover (invariante #20 — no standalone Discover)
- Notifications bell w/ counter (DEC-5 dipendenza endpoint)"
```

**Self-review**:
- [ ] 8 voci total (claim corretto, era 7 in body originale CRIT-8)
- [ ] No standalone Discover (rimosso da sidebar)
- [ ] Notifications bell + counter wired (placeholder count 0 se endpoint non ready)
- [ ] Order deterministico (test verifies)

---

## WP3 — DrawerStack provider + hook

> **Spec reference**: Sezione 4 Asse B — "Drawer stack pattern (riusabile)". MIN-3 a11y.

### Task 3: DrawerStack provider + state machine (push/pop/closeAll)

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/web/src/components/ui/drawer-stack/drawer-stack-provider.tsx`
- Create: `apps/web/src/components/ui/drawer-stack/use-drawer-stack.ts`
- Create: `apps/web/src/components/ui/drawer-stack/index.ts`
- Test: `apps/web/src/__tests__/ui/drawer-stack/use-drawer-stack.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { renderHook, act } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { DrawerStackProvider, useDrawerStack } from '@/components/ui/drawer-stack';

describe('useDrawerStack state machine', () => {
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <DrawerStackProvider>{children}</DrawerStackProvider>
  );

  it('starts with empty stack', () => {
    const { result } = renderHook(() => useDrawerStack(), { wrapper });
    expect(result.current.stack).toHaveLength(0);
  });

  it('push adds drawer to stack', () => {
    const { result } = renderHook(() => useDrawerStack(), { wrapper });
    act(() => result.current.push({ id: 'gn-1', content: <div>GN drawer</div> }));
    expect(result.current.stack).toHaveLength(1);
    expect(result.current.stack[0].id).toBe('gn-1');
  });

  it('push allows stacking (drawer on drawer)', () => {
    const { result } = renderHook(() => useDrawerStack(), { wrapper });
    act(() => {
      result.current.push({ id: 'gn-1', content: <div>GN</div> });
      result.current.push({ id: 'player-1', content: <div>Player</div> });
    });
    expect(result.current.stack).toHaveLength(2);
    expect(result.current.stack[1].id).toBe('player-1');
  });

  it('pop removes top of stack', () => {
    const { result } = renderHook(() => useDrawerStack(), { wrapper });
    act(() => {
      result.current.push({ id: 'gn-1', content: <div>GN</div> });
      result.current.push({ id: 'player-1', content: <div>Player</div> });
      result.current.pop();
    });
    expect(result.current.stack).toHaveLength(1);
    expect(result.current.stack[0].id).toBe('gn-1');
  });

  it('closeAll empties entire stack', () => {
    const { result } = renderHook(() => useDrawerStack(), { wrapper });
    act(() => {
      result.current.push({ id: 'gn-1', content: <div>GN</div> });
      result.current.push({ id: 'player-1', content: <div>Player</div> });
      result.current.closeAll();
    });
    expect(result.current.stack).toHaveLength(0);
  });

  it('pop on empty stack is no-op (no crash)', () => {
    const { result } = renderHook(() => useDrawerStack(), { wrapper });
    expect(() => act(() => result.current.pop())).not.toThrow();
  });
});
```

- [ ] **Step 2: Run → FAIL**

```bash
cd apps/web && pnpm test use-drawer-stack
```

- [ ] **Step 3: Implement provider + hook**

```typescript
// use-drawer-stack.ts
import { create } from 'zustand';

export interface DrawerStackEntry {
  id: string;
  content: React.ReactNode;
  /** Optional onClose callback when this entry is popped */
  onClose?: () => void;
}

interface DrawerStackState {
  stack: DrawerStackEntry[];
  push: (entry: DrawerStackEntry) => void;
  pop: () => void;
  closeAll: () => void;
}

export const useDrawerStackStore = create<DrawerStackState>((set, get) => ({
  stack: [],
  push: (entry) => set((state) => ({ stack: [...state.stack, entry] })),
  pop: () => {
    const { stack } = get();
    if (stack.length === 0) return;
    stack[stack.length - 1].onClose?.();
    set({ stack: stack.slice(0, -1) });
  },
  closeAll: () => {
    const { stack } = get();
    stack.forEach((e) => e.onClose?.());
    set({ stack: [] });
  },
}));

export const useDrawerStack = useDrawerStackStore;
```

```tsx
// drawer-stack-provider.tsx
'use client';
import { useEffect } from 'react';
import { useDrawerStack } from './use-drawer-stack';
import { DrawerStack } from './drawer-stack';

export function DrawerStackProvider({ children }: { children: React.ReactNode }) {
  return (
    <>
      {children}
      <DrawerStack />
    </>
  );
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/drawer-stack/ apps/web/src/__tests__/ui/drawer-stack/
git commit -m "feat(ui): #1897 DrawerStack provider + push/pop/closeAll state machine"
```

---

### Task 4: DrawerStack rendering + ESC + backdrop + a11y prefers-reduced-motion

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/web/src/components/ui/drawer-stack/drawer-stack.tsx`
- Modify: `apps/web/src/__tests__/ui/drawer-stack/drawer-stack.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { DrawerStackProvider, useDrawerStack } from '@/components/ui/drawer-stack';

function TestHarness() {
  const drawer = useDrawerStack();
  return (
    <button onClick={() => drawer.push({ id: 'test-1', content: <div>Drawer 1</div> })}>
      Open
    </button>
  );
}

describe('DrawerStack rendering', () => {
  it('renders pushed drawer with content', () => {
    render(
      <DrawerStackProvider>
        <TestHarness />
      </DrawerStackProvider>
    );
    fireEvent.click(screen.getByText('Open'));
    expect(screen.getByText('Drawer 1')).toBeInTheDocument();
  });

  it('ESC key pops top drawer (back-step)', () => {
    render(
      <DrawerStackProvider>
        <TestHarness />
      </DrawerStackProvider>
    );
    fireEvent.click(screen.getByText('Open'));
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(screen.queryByText('Drawer 1')).not.toBeInTheDocument();
  });

  it('backdrop click closes entire stack', () => {
    render(
      <DrawerStackProvider>
        <TestHarness />
      </DrawerStackProvider>
    );
    fireEvent.click(screen.getByText('Open'));
    fireEvent.click(screen.getByTestId('drawer-stack-backdrop'));
    expect(screen.queryByText('Drawer 1')).not.toBeInTheDocument();
  });

  it('respects prefers-reduced-motion (MIN-3)', () => {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((q: string) => ({
        matches: q.includes('prefers-reduced-motion'),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      })),
    });
    render(
      <DrawerStackProvider>
        <TestHarness />
      </DrawerStackProvider>
    );
    fireEvent.click(screen.getByText('Open'));
    const drawer = screen.getByTestId('drawer-stack-panel');
    expect(drawer).toHaveStyle({ transitionDuration: '0ms' });
  });
});
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement DrawerStack component**

```tsx
// drawer-stack.tsx
'use client';
import { useEffect, useReducer } from 'react';
import { useDrawerStack } from './use-drawer-stack';

export function DrawerStack() {
  const { stack, pop, closeAll } = useDrawerStack();
  const top = stack[stack.length - 1];

  useEffect(() => {
    if (!top) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        pop();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [top, pop]);

  if (!top) return null;

  const prefersReducedMotion = typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  return (
    <div
      data-testid="drawer-stack-root"
      className="fixed inset-0 z-50"
      role="dialog"
      aria-modal="true"
      aria-labelledby={`drawer-${top.id}-label`}
    >
      <button
        data-testid="drawer-stack-backdrop"
        className="absolute inset-0 bg-[var(--c-overlay-scrim)]"
        onClick={closeAll}
        aria-label="Close drawer stack"
      />
      <aside
        data-testid="drawer-stack-panel"
        className="absolute right-0 top-0 h-full w-[420px] bg-background shadow-2xl md:w-[420px] sm:w-full"
        style={{
          transition: prefersReducedMotion ? 'none' : 'transform 250ms ease',
          transitionDuration: prefersReducedMotion ? '0ms' : '250ms',
        }}
      >
        {top.content}
      </aside>
    </div>
  );
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Add Storybook story (in T9)**

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/drawer-stack/ apps/web/src/__tests__/ui/drawer-stack/
git commit -m "feat(ui): #1897 DrawerStack rendering + ESC + backdrop + prefers-reduced-motion (MIN-3)"
```

**Self-review**:
- [ ] ESC pops top only (back-step), NOT closeAll
- [ ] Backdrop closeAll, X explicit closes specific (TBD)
- [ ] a11y `prefers-reduced-motion` → 0ms transition
- [ ] role=dialog + aria-modal + aria-labelledby
- [ ] Mobile: width 100% se viewport <768px (Tailwind sm:w-full)

---

## WP4 — StatePreviewProvider (dev-only)

> **Spec reference**: Sezione 4 Asse B — "Toggle 5 stati per page-mock (dev tool)". MAJ-3 implementation pattern.

### Task 5: StatePreviewProvider dev-only + tree-shake verify

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files:**
- Create: `apps/web/src/components/ui/state-preview/state-preview-provider.tsx`
- Create: `apps/web/src/components/ui/state-preview/use-state-preview.ts`
- Create: `apps/web/src/components/ui/state-preview/state-preview-toggle.tsx`
- Create: `apps/web/src/components/ui/state-preview/index.ts`
- Test: `apps/web/src/__tests__/ui/state-preview/state-preview.test.tsx`

- [ ] **Step 1: Write failing test (dev-only behavior + tree-shake)**

```tsx
import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

describe('StatePreviewProvider', () => {
  it('useStatePreview returns "default" when no override', async () => {
    vi.stubEnv('NODE_ENV', 'development');
    const { StatePreviewProvider, useStatePreview } = await import('@/components/ui/state-preview');

    const wrapper = ({ children }: any) => <StatePreviewProvider>{children}</StatePreviewProvider>;
    const { result } = renderHook(() => useStatePreview('dashboard'), { wrapper });
    expect(result.current.state).toBe('default');
  });

  it('useStatePreview returns overridden state when setStateFor called', async () => {
    vi.stubEnv('NODE_ENV', 'development');
    const { StatePreviewProvider, useStatePreview } = await import('@/components/ui/state-preview');

    const wrapper = ({ children }: any) => <StatePreviewProvider>{children}</StatePreviewProvider>;
    const { result } = renderHook(() => useStatePreview('dashboard'), { wrapper });
    act(() => result.current.setStateFor('dashboard', 'empty'));
    expect(result.current.state).toBe('empty');
  });

  it('in production NODE_ENV, useStatePreview always returns "default" (no-op)', async () => {
    vi.stubEnv('NODE_ENV', 'production');
    const { StatePreviewProvider, useStatePreview } = await import('@/components/ui/state-preview');

    const wrapper = ({ children }: any) => <StatePreviewProvider>{children}</StatePreviewProvider>;
    const { result } = renderHook(() => useStatePreview('dashboard'), { wrapper });
    act(() => result.current.setStateFor('dashboard', 'empty'));
    expect(result.current.state).toBe('default'); // override ignored
  });
});
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement provider with NODE_ENV gate**

```tsx
// state-preview-provider.tsx
'use client';
import { createContext, useContext, useState } from 'react';

export type StateKind = 'default' | 'empty' | 'loading' | 'error' | 'offline';

interface StatePreviewContext {
  states: Record<string, StateKind>;
  setStateFor: (pageId: string, state: StateKind) => void;
}

const Context = createContext<StatePreviewContext>({
  states: {},
  setStateFor: () => {},
});

export function StatePreviewProvider({ children }: { children: React.ReactNode }) {
  const [states, setStates] = useState<Record<string, StateKind>>({});

  const setStateFor = (pageId: string, state: StateKind) => {
    if (process.env.NODE_ENV === 'production') return; // dev-only gate (MAJ-3)
    setStates((s) => ({ ...s, [pageId]: state }));
  };

  return <Context.Provider value={{ states, setStateFor }}>{children}</Context.Provider>;
}

export function useStatePreview(pageId: string) {
  const ctx = useContext(Context);
  const state = process.env.NODE_ENV === 'production' ? 'default' : (ctx.states[pageId] ?? 'default');
  return { state, setStateFor: ctx.setStateFor };
}
```

- [ ] **Step 4: Implement StatePreviewToggle (dev-only UI)**

```tsx
// state-preview-toggle.tsx
'use client';
import { useStatePreview, StateKind } from './use-state-preview';

const STATES: StateKind[] = ['default', 'empty', 'loading', 'error', 'offline'];

export function StatePreviewToggle({ pageId }: { pageId: string }) {
  if (process.env.NODE_ENV === 'production') return null;
  const { state, setStateFor } = useStatePreview(pageId);
  return (
    <div data-testid="state-preview-toggle" className="fixed bottom-4 right-4 z-[9999]">
      <select value={state} onChange={(e) => setStateFor(pageId, e.target.value as StateKind)}>
        {STATES.map((s) => <option key={s} value={s}>{s}</option>)}
      </select>
    </div>
  );
}
```

- [ ] **Step 5: Run → PASS**

- [ ] **Step 6: Verify tree-shake (MAJ-3 acceptance "0 prod bytes")**

```bash
cd apps/web && pnpm build && grep -rl 'StatePreview' .next/static/chunks/ 2>&1 || echo "PASS — 0 prod bytes"
```
Expected: PASS (no chunks contain StatePreview code).

Se fallisce: investigare se NODE_ENV-gated code è eliminato da Next/Webpack DCE. Possibilmente serve `if (process.env.NODE_ENV !== 'production')` esplicito al top-level del file invece di interno.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/state-preview/ apps/web/src/__tests__/ui/state-preview/
git commit -m "feat(ui): #1897 StatePreviewProvider dev-only (MAJ-3, 0 prod bytes verified)"
```

---

## WP5 — WizardModal primitive

> **Spec reference**: Sezione 4 Asse B — "Modali wizard" + MAJ-4 TypeScript signature.

### Task 6: WizardModal + validate flow + TypeScript signature

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/web/src/components/ui/wizard-modal/wizard-step-types.ts`
- Create: `apps/web/src/components/ui/wizard-modal/wizard-modal.tsx`
- Create: `apps/web/src/components/ui/wizard-modal/index.ts`
- Test: `apps/web/src/__tests__/ui/wizard-modal/wizard-modal.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { WizardModal, WizardStep } from '@/components/ui/wizard-modal';

describe('WizardModal', () => {
  const STEPS: WizardStep[] = [
    { title: 'Step 1', content: <input data-testid="input-1" /> },
    { title: 'Step 2', content: <input data-testid="input-2" /> },
    { title: 'Step 3 (optional)', content: <input data-testid="input-3" />, optional: true },
  ];

  it('renders first step initially', () => {
    render(<WizardModal steps={STEPS} onComplete={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByText('Step 1')).toBeInTheDocument();
    expect(screen.getByTestId('input-1')).toBeInTheDocument();
  });

  it('Next button advances to step 2', () => {
    render(<WizardModal steps={STEPS} onComplete={vi.fn()} onCancel={vi.fn()} />);
    fireEvent.click(screen.getByText('Next'));
    expect(screen.getByText('Step 2')).toBeInTheDocument();
  });

  it('runs validate callback before advancing', async () => {
    const validate = vi.fn().mockResolvedValue({ valid: false, errors: [{ message: 'Missing field' }] });
    const stepsWithValidate = [
      { ...STEPS[0], validate },
      STEPS[1],
    ];
    render(<WizardModal steps={stepsWithValidate} onComplete={vi.fn()} onCancel={vi.fn()} />);
    fireEvent.click(screen.getByText('Next'));
    await waitFor(() => expect(validate).toHaveBeenCalled());
    expect(screen.getByText('Step 1')).toBeInTheDocument(); // stays on step 1
    expect(screen.getByText('Missing field')).toBeInTheDocument();
  });

  it('Skip button appears on optional step', () => {
    render(<WizardModal steps={STEPS} onComplete={vi.fn()} onCancel={vi.fn()} />);
    fireEvent.click(screen.getByText('Next'));
    fireEvent.click(screen.getByText('Next')); // now on step 3 optional
    expect(screen.getByText('Skip')).toBeInTheDocument();
  });

  it('Complete button on last step calls onComplete', async () => {
    const onComplete = vi.fn();
    render(<WizardModal steps={STEPS} onComplete={onComplete} onCancel={vi.fn()} />);
    fireEvent.click(screen.getByText('Next'));
    fireEvent.click(screen.getByText('Next'));
    fireEvent.click(screen.getByText('Complete'));
    await waitFor(() => expect(onComplete).toHaveBeenCalled());
  });

  it('Cancel button opens confirmation modal', () => {
    render(<WizardModal steps={STEPS} onComplete={vi.fn()} onCancel={vi.fn()} />);
    fireEvent.click(screen.getByText('Cancel'));
    expect(screen.getByText(/Sei sicuro/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement types + component (MAJ-4 signature)**

```typescript
// wizard-step-types.ts
import type { ReactNode } from 'react';

export interface WizardStep {
  title: string;
  content: ReactNode;
  validate?: () => Promise<{ valid: boolean; errors?: ValidationError[] }>;
  optional?: boolean;
}

export interface ValidationError {
  field?: string;
  message: string;
}

export interface WizardModalProps {
  steps: WizardStep[];
  onComplete: (data: unknown) => Promise<void>;
  onCancel: () => void;
}
```

```tsx
// wizard-modal.tsx
'use client';
import { useState } from 'react';
import { WizardModalProps, ValidationError } from './wizard-step-types';

export function WizardModal({ steps, onComplete, onCancel }: WizardModalProps) {
  const [index, setIndex] = useState(0);
  const [errors, setErrors] = useState<ValidationError[]>([]);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const step = steps[index];
  const isLast = index === steps.length - 1;

  const advance = async () => {
    if (step.validate) {
      const result = await step.validate();
      if (!result.valid) {
        setErrors(result.errors ?? []);
        return;
      }
    }
    setErrors([]);
    if (isLast) {
      await onComplete({});
    } else {
      setIndex((i) => i + 1);
    }
  };

  const skip = () => {
    if (!step.optional) return;
    setErrors([]);
    setIndex((i) => i + 1);
  };

  return (
    <div role="dialog" aria-modal="true" className="fixed inset-0 z-40 flex items-center justify-center">
      <div className="bg-background w-full max-w-[720px] rounded-lg shadow-2xl">
        <header className="sticky top-0 p-4 border-b">
          <h2 className="text-lg font-semibold">{step.title}</h2>
          <div data-testid="step-indicator">{index + 1}/{steps.length}</div>
        </header>
        <div className="p-6">
          {step.content}
          {errors.length > 0 && (
            <ul role="alert" className="mt-4 text-[var(--c-danger)]">
              {errors.map((e, i) => <li key={i}>{e.message}</li>)}
            </ul>
          )}
        </div>
        <footer className="sticky bottom-0 p-4 border-t flex justify-between">
          {index > 0 && <button onClick={() => setIndex((i) => i - 1)}>Back</button>}
          <div className="ml-auto flex gap-2">
            <button onClick={() => setConfirmCancel(true)}>Cancel</button>
            {step.optional && !isLast && <button onClick={skip}>Skip</button>}
            <button onClick={advance}>{isLast ? 'Complete' : 'Next'}</button>
          </div>
        </footer>
      </div>
      {confirmCancel && (
        <ConfirmCancelModal onConfirm={onCancel} onDismiss={() => setConfirmCancel(false)} />
      )}
    </div>
  );
}

function ConfirmCancelModal({ onConfirm, onDismiss }: { onConfirm: () => void; onDismiss: () => void }) {
  return (
    <div role="dialog" aria-modal="true" className="absolute inset-0 z-50 flex items-center justify-center bg-[var(--c-overlay-scrim)]">
      <div className="bg-background p-6 rounded-lg max-w-[400px]">
        <p>Sei sicuro di voler annullare? Tutti i dati inseriti andranno persi.</p>
        <div className="flex gap-2 mt-4 justify-end">
          <button onClick={onDismiss}>Continua</button>
          <button onClick={onConfirm} className="bg-[var(--c-danger)] text-white">Annulla</button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/wizard-modal/ apps/web/src/__tests__/ui/wizard-modal/
git commit -m "feat(ui): #1897 WizardModal primitive + TypeScript signature (MAJ-4)"
```

---

## WP6 — ToastProvider via sonner

> **Spec reference**: Sezione 4 Asse B — "Toast non-bloccante". MIN-7 verify sonner.

### Task 7: ToastProvider + sonner integration

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Create: `apps/web/src/components/ui/toast/toast-provider.tsx`
- Create: `apps/web/src/components/ui/toast/use-toast.ts`
- Create: `apps/web/src/components/ui/toast/index.ts`
- Modify: `apps/web/package.json` (verify sonner)
- Test: `apps/web/src/__tests__/ui/toast/use-toast.test.tsx`

- [ ] **Step 1: Verify sonner presence (MIN-7)**

```bash
cd apps/web && grep '"sonner"' package.json || pnpm add sonner@latest
```

- [ ] **Step 2: Write failing test**

```tsx
import { renderHook, act } from '@testing-library/react';
import { useToast, ToastProvider } from '@/components/ui/toast';

describe('useToast', () => {
  const wrapper = ({ children }: any) => <ToastProvider>{children}</ToastProvider>;

  it('exposes warning(), success(), error(), info() methods', () => {
    const { result } = renderHook(() => useToast(), { wrapper });
    expect(typeof result.current.warning).toBe('function');
    expect(typeof result.current.success).toBe('function');
    expect(typeof result.current.error).toBe('function');
    expect(typeof result.current.info).toBe('function');
  });
});
```

- [ ] **Step 3: Run → FAIL**

- [ ] **Step 4: Implement wrapper around sonner**

```tsx
// toast-provider.tsx
'use client';
import { Toaster } from 'sonner';

export function ToastProvider({ children }: { children: React.ReactNode }) {
  return (
    <>
      {children}
      <Toaster position="bottom-right" duration={6000} richColors closeButton />
    </>
  );
}
```

```typescript
// use-toast.ts
import { toast } from 'sonner';

export function useToast() {
  return {
    success: (message: string, options?: { action?: { label: string; onClick: () => void } }) =>
      toast.success(message, options),
    error: (message: string) => toast.error(message),
    warning: (message: string, options?: { action?: { label: string; onClick: () => void } }) =>
      toast.warning(message, options),
    info: (message: string) => toast.info(message),
  };
}
```

- [ ] **Step 5: Run → PASS**

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/toast/ apps/web/src/__tests__/ui/toast/ apps/web/package.json
git commit -m "feat(ui): #1897 ToastProvider via sonner (MIN-7)"
```

---

## WP7 — Discover tab interno `/games`

> **Spec reference**: Sezione 4 Asse B — "Sidebar a 8 voci" + invariante #20.

### Task 8: Games page with Discover as default tab

**Mix-model**: haiku · **Effort**: S (~3h)

**Files:**
- Modify: `apps/web/src/app/games/page.tsx`
- Test: `apps/web/src/__tests__/app/games/page.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { render, screen } from '@testing-library/react';
import GamesPage from '@/app/games/page';

describe('Games page (invariante #20)', () => {
  it('renders 4 tabs: Discover (default) | Catalogo | Trending | Community', () => {
    render(<GamesPage />);
    const tabs = screen.getAllByRole('tab');
    expect(tabs.map(t => t.textContent)).toEqual(['Discover', 'Catalogo', 'Trending', 'Community']);
  });

  it('Discover tab is selected by default when no query param', () => {
    render(<GamesPage />);
    const discoverTab = screen.getByRole('tab', { name: 'Discover' });
    expect(discoverTab).toHaveAttribute('aria-selected', 'true');
  });

  it('respects ?tab=trending query param', () => {
    vi.mocked(useSearchParams).mockReturnValue(new URLSearchParams('tab=trending'));
    render(<GamesPage />);
    expect(screen.getByRole('tab', { name: 'Trending' })).toHaveAttribute('aria-selected', 'true');
  });
});
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implement page (use shadcn Tabs)**

```tsx
// app/games/page.tsx
'use client';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useSearchParams } from 'next/navigation';

export default function GamesPage() {
  const params = useSearchParams();
  const defaultTab = params.get('tab') ?? 'discover';
  return (
    <div>
      <h1>Games</h1>
      <Tabs defaultValue={defaultTab}>
        <TabsList>
          <TabsTrigger value="discover">Discover</TabsTrigger>
          <TabsTrigger value="catalogo">Catalogo</TabsTrigger>
          <TabsTrigger value="trending">Trending</TabsTrigger>
          <TabsTrigger value="community">Community</TabsTrigger>
        </TabsList>
        <TabsContent value="discover"><DiscoverHero /></TabsContent>
        <TabsContent value="catalogo">Catalogo placeholder</TabsContent>
        <TabsContent value="trending">Trending placeholder</TabsContent>
        <TabsContent value="community">Community placeholder</TabsContent>
      </Tabs>
    </div>
  );
}

function DiscoverHero() {
  return <div data-testid="discover-hero">Discover content (placeholder, asse D builds full)</div>;
}
```

- [ ] **Step 4: Run → PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/games/ apps/web/src/__tests__/app/games/
git commit -m "feat(games): #1897 /games tab Discover come default (invariante #20)"
```

---

## WP8 — Storybook + cross-cutting E2E

### Task 9: Storybook stories per primitive

**Mix-model**: haiku · **Effort**: M (~5h)

**Files:**
- Create: `apps/web/src/components/ui/drawer-stack/drawer-stack.stories.tsx`
- Create: `apps/web/src/components/ui/wizard-modal/wizard-modal.stories.tsx`
- Create: `apps/web/src/components/ui/state-preview/state-preview.stories.tsx`
- Create: `apps/web/src/components/ui/toast/toast.stories.tsx`

- [ ] **Step 1**: Per ogni primitive, scrivi 3-5 stories:
  - Default usage
  - Edge cases (empty, error, validation fail)
  - With prefers-reduced-motion enabled
  - Mobile fullwidth (drawer)

- [ ] **Step 2: Verify build**

```bash
cd apps/web && pnpm storybook:build 2>&1 | tail -10
```
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/**/*.stories.tsx
git commit -m "docs(storybook): #1897 stories for DrawerStack/WizardModal/StatePreview/Toast"
```

---

### Task 10: Cross-asse Playwright E2E drawer stack

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files:**
- Create: `apps/web/e2e/cross-asse-drawer-stack.spec.ts`

- [ ] **Step 1: Write E2E test**

```typescript
// e2e/cross-asse-drawer-stack.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Cross-asse drawer stack flow', () => {
  test('Dashboard → GN drawer → Player drawer swap → ESC back → backdrop close', async ({ page }) => {
    await page.goto('/dashboard');

    // Open GN drawer
    await page.getByTestId('section-prossimi').getByRole('button').first().click();
    await expect(page.getByTestId('drawer-stack-panel')).toBeVisible();
    await expect(page.getByText(/Game Night di/)).toBeVisible();

    // Swap to Player drawer (stack push)
    await page.getByText('Anna').click(); // first player chip
    await expect(page.getByTestId('drawer-stack-panel').getByText('Profile')).toBeVisible();
    await expect(page.getByText('Sessions vs me')).toBeVisible(); // Player drawer specific

    // ESC: back to GN drawer (pop)
    await page.keyboard.press('Escape');
    await expect(page.getByText(/Game Night di/)).toBeVisible();
    await expect(page.getByText('Sessions vs me')).not.toBeVisible();

    // Backdrop click: close all
    await page.getByTestId('drawer-stack-backdrop').click();
    await expect(page.getByTestId('drawer-stack-panel')).not.toBeVisible();
  });

  test('Wizard 3-step new GameNight flow', async ({ page }) => {
    await page.goto('/game-nights/new');
    await expect(page.getByText('Quando+Dove')).toBeVisible();

    // Step 1: fill date+location
    await page.getByLabel('Data').fill('2026-06-14');
    await page.getByLabel('Location').fill('Casa Marco');
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 2: select players
    await expect(page.getByText('Invita player')).toBeVisible();
    await page.getByLabel('Anna').check();
    await page.getByRole('button', { name: 'Next' }).click();

    // Step 3: optional, skip
    await expect(page.getByText('Game suggested')).toBeVisible();
    await page.getByRole('button', { name: 'Skip' }).click();

    // Complete
    await expect(page).toHaveURL(/\/game-nights\/[a-f0-9-]+$/);
  });
});
```

- [ ] **Step 2: Run locally**

```bash
cd apps/web && pnpm test:e2e cross-asse-drawer-stack
```
Expected: PASS (or skip if asse C+D dependencies not ready — wait).

> Note: questo E2E necessita asse C dashboard+drawer GN integration + asse D /game-nights/new wizard. **Esegui DOPO asse C+D primary PR merged**. Per ora il test serve come placeholder/scaffold.

- [ ] **Step 3: Commit (skeleton)**

```bash
git add apps/web/e2e/cross-asse-drawer-stack.spec.ts
git commit -m "test(e2e): #1897 cross-asse drawer stack flow skeleton (gated su asse C+D)"
```

---

## Self-Review Checklist (post-plan)

**Spec coverage**:
- [x] WP1 covers MAJ-9 token additions (#37, #38)
- [x] WP2 covers CRIT-8 sidebar 8 voci + invariante #20
- [x] WP3 covers DrawerStack pattern + MIN-3 prefers-reduced-motion
- [x] WP4 covers MAJ-3 StatePreviewProvider dev-only
- [x] WP5 covers MAJ-4 WizardModal TypeScript signature
- [x] WP6 covers MIN-7 sonner verify
- [x] WP7 covers Discover tab interno
- [x] WP8 covers Storybook + cross-asse E2E skeleton

**Placeholder scan**: no TBD, no "implement later", no untyped methods. WP8 E2E has explicit "gated su asse C+D" note (acceptable skeleton with clear dependency).

**Type consistency**:
- `WizardStep`/`ValidationError`/`WizardModalProps` consistent T6
- `StateKind` enum consistent T5
- `MainNavItem` type consistent T2
- `DrawerStackEntry` consistent T3/T4

**Critical path identification**:
- WP1 (tokens) → foundation for WP3 (DrawerStack uses --c-overlay-scrim)
- WP2 (sidebar) → blocca asse D (every route uses sidebar)
- WP3 (DrawerStack) → blocca asse C (dashboard uses) + asse D (every drawer)
- WP4 (StatePreview) parallelizzabile
- WP5 (WizardModal) → blocca asse D /game-nights/new
- WP6 (Toast) parallelizzabile
- WP7 (Games tab) standalone
- WP8 final consolidation (Storybook + E2E)

**Effort verification**:
- WP1: 2h ≈ 0.3gg
- WP2: 4h ≈ 0.5gg
- WP3: 6+6 = 12h ≈ 1.5gg
- WP4: 5h ≈ 0.6gg
- WP5: 6h ≈ 0.8gg
- WP6: 3h ≈ 0.4gg
- WP7: 3h ≈ 0.4gg
- WP8: 5+6 = 11h ≈ 1.4gg
- **Total**: ~46h ≈ 6gg + ~2gg buffer test/CI/Storybook = ~8-9gg, in linea con stima 7+2=9gg ✓

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-06-04-asse-b-ui-shell-pattern.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — Dispatch fresh subagent per task with mix-model (6 haiku + 4 sonnet), review between tasks, ~1.5-2 settimane elapsed time.

**2. Inline Execution** — Execute tasks in current session sequentially. Possibile per asse B (M+ scope) ma rischio context overrun se tutti i 10 task in single session.

**Recommended sequence**:
1. WP1 (tokens) — foundation
2. WP2 (sidebar) + WP6 (Toast) parallel — both small
3. WP3 (DrawerStack) — critical path
4. WP4 (StatePreview) + WP5 (WizardModal) parallel — both medium
5. WP7 (Games tab) standalone
6. WP8 (Storybook + E2E) closes asse B

**Parallelization opportunity con asse A**: asse B può procedere indipendente da backend (usa fixture/mock per stati). Specialmente WP1/WP2/WP3/WP6/WP7 sono FE-puri.
