# Card Drawer + Navigation Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the shell layout and drawer system for a Page-first navigation flow: remove CardHand + MyHand sidebars, add MiniNav recents, ConnectionBar, drawer tabs for all 9 entity types, and mobile Session Mode.

**Architecture:** Remove 3 navigation layers (CardHand rail, MyHand sidebar, MyHand bottom bar) and replace with 2 simpler primitives: RecentsBar in MiniNav + ConnectionBar on entity pages. The existing ExtraMeepleCardDrawer pattern is extended (not replaced) — each entity gets a DrawerContent component with 2-3 tabs and a conditional ActionFooter. A drawer stack (max 3) enables cross-entity navigation. Session Mode toggles the mobile bottom bar during active game sessions.

**Tech Stack:** Next.js 16 App Router, React 19, Zustand (stores), Radix UI Sheet (drawer), Tailwind 4, Lucide icons, Vitest (tests)

**Spec:** `docs/superpowers/specs/2026-04-10-card-drawer-navigation-redesign-design.md`

---

## Milestone Overview

| M | Milestone | Tasks | Outcome |
|---|-----------|-------|---------|
| **M0** | Recents store + RecentsBar | 1-3 | MiniNav shows recent entity pills, sessionStorage persisted |
| **M1** | Shell cleanup | 4-6 | CardHand + MyHand removed, DesktopShell simplified, consumers migrated |
| **M2** | ConnectionBar | 7-9 | Interactive ManaPip bar on entity pages, wired to cascade navigation |
| **M3** | DrawerActionFooter + drawer stack | 10-12 | Conditional action footer, push/pop drawer stack |
| **M4** | Entity drawer content (5 new) | 13-17 | Player, Session, Event, Toolkit, Tool drawer content components |
| **M5** | Session Mode | 18-20 | MobileBottomBar dual-state, SessionBanner desktop |
| **M6** | Integration + cleanup | 21-22 | Wire everything, final test pass, dead code removal |

---

## M0 — Recents Store + RecentsBar

### Task 1: RecentsStore (Zustand)

**Files:**
- Create: `apps/web/src/stores/use-recents.ts`
- Test: `apps/web/src/stores/__tests__/use-recents.test.ts`

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/stores/__tests__/use-recents.test.ts
import { act } from 'react';
import { describe, it, expect, beforeEach } from 'vitest';
import { useRecentsStore } from '../use-recents';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

function makeRecent(id: string, entity: MeepleEntityType = 'game') {
  return { id, entity, title: `Title ${id}`, href: `/games/${id}` };
}

describe('useRecentsStore', () => {
  beforeEach(() => {
    act(() => useRecentsStore.getState().clear());
    sessionStorage.clear();
  });

  it('starts empty', () => {
    expect(useRecentsStore.getState().items).toEqual([]);
  });

  it('push adds an item with visitedAt timestamp', () => {
    act(() => useRecentsStore.getState().push(makeRecent('g1')));
    const items = useRecentsStore.getState().items;
    expect(items).toHaveLength(1);
    expect(items[0].id).toBe('g1');
    expect(items[0].visitedAt).toBeGreaterThan(0);
  });

  it('push promotes existing item to front', () => {
    act(() => {
      useRecentsStore.getState().push(makeRecent('g1'));
      useRecentsStore.getState().push(makeRecent('g2'));
      useRecentsStore.getState().push(makeRecent('g1')); // re-visit
    });
    const items = useRecentsStore.getState().items;
    expect(items).toHaveLength(2);
    expect(items[0].id).toBe('g1'); // most recent first
  });

  it('evicts oldest when exceeding max 4', () => {
    act(() => {
      useRecentsStore.getState().push(makeRecent('g1'));
      useRecentsStore.getState().push(makeRecent('g2'));
      useRecentsStore.getState().push(makeRecent('g3'));
      useRecentsStore.getState().push(makeRecent('g4'));
      useRecentsStore.getState().push(makeRecent('g5'));
    });
    const items = useRecentsStore.getState().items;
    expect(items).toHaveLength(4);
    expect(items.find(i => i.id === 'g1')).toBeUndefined(); // evicted
  });

  it('remove deletes by id', () => {
    act(() => {
      useRecentsStore.getState().push(makeRecent('g1'));
      useRecentsStore.getState().push(makeRecent('g2'));
      useRecentsStore.getState().remove('g1');
    });
    expect(useRecentsStore.getState().items).toHaveLength(1);
  });

  it('clear empties the list', () => {
    act(() => {
      useRecentsStore.getState().push(makeRecent('g1'));
      useRecentsStore.getState().clear();
    });
    expect(useRecentsStore.getState().items).toEqual([]);
  });

  it('persists to sessionStorage', () => {
    act(() => useRecentsStore.getState().push(makeRecent('g1')));
    const stored = sessionStorage.getItem('meeple-recents');
    expect(stored).toBeTruthy();
    expect(JSON.parse(stored!)).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Note: the `stores/__tests__/` directory is new — it will be created when writing the test file. Vitest's `include` glob covers `src/**/*.test.ts` so the path is valid.

Run: `cd apps/web && pnpm vitest run src/stores/__tests__/use-recents.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement store**

```typescript
// apps/web/src/stores/use-recents.ts
'use client';

import { create } from 'zustand';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

const MAX_RECENTS = 4;
const STORAGE_KEY = 'meeple-recents';

export interface RecentItem {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  visitedAt: number;
}

interface RecentsState {
  items: RecentItem[];
  push: (item: Omit<RecentItem, 'visitedAt'>) => void;
  remove: (id: string) => void;
  clear: () => void;
}

function readStorage(): RecentItem[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as RecentItem[]) : [];
  } catch {
    return [];
  }
}

function writeStorage(items: RecentItem[]): void {
  if (typeof window === 'undefined') return;
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch {
    /* full or unavailable */
  }
}

export const useRecentsStore = create<RecentsState>((set, get) => ({
  items: readStorage(),

  push: (item) => {
    const now = Date.now();
    const current = get().items.filter(i => i.id !== item.id);
    const next = [{ ...item, visitedAt: now }, ...current].slice(0, MAX_RECENTS);
    writeStorage(next);
    set({ items: next });
  },

  remove: (id) => {
    const next = get().items.filter(i => i.id !== id);
    writeStorage(next);
    set({ items: next });
  },

  clear: () => {
    writeStorage([]);
    set({ items: [] });
  },
}));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/stores/__tests__/use-recents.test.ts`
Expected: all 7 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/stores/use-recents.ts apps/web/src/stores/__tests__/use-recents.test.ts
git commit -m "feat(nav): add RecentsStore with sessionStorage persistence"
```

---

### Task 2: RecentsBar component

**Files:**
- Create: `apps/web/src/components/layout/UserShell/RecentsBar.tsx`
- Test: `apps/web/src/components/layout/UserShell/__tests__/RecentsBar.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/layout/UserShell/__tests__/RecentsBar.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { act } from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { RecentsBar } from '../RecentsBar';
import { useRecentsStore } from '@/stores/use-recents';

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => '/games/g1',
}));

function seedRecents() {
  act(() => {
    useRecentsStore.getState().push({ id: 'g2', entity: 'game', title: 'Catan', href: '/games/g2' });
    useRecentsStore.getState().push({ id: 'a1', entity: 'agent', title: 'Azul Expert', href: '/agents/a1' });
  });
}

describe('RecentsBar', () => {
  beforeEach(() => {
    act(() => useRecentsStore.getState().clear());
    mockPush.mockClear();
  });

  it('renders nothing when no recents', () => {
    const { container } = render(<RecentsBar />);
    expect(container.firstChild).toBeNull();
  });

  it('renders pills for each recent (excluding current path)', () => {
    seedRecents();
    render(<RecentsBar />);
    // g2 and a1 are visible; current path is /games/g1 so no exclusion happens here
    expect(screen.getAllByTestId(/^recent-pill-/)).toHaveLength(2);
  });

  it('excludes the current pathname from display', () => {
    act(() => {
      useRecentsStore.getState().push({ id: 'g1', entity: 'game', title: 'Azul', href: '/games/g1' });
      useRecentsStore.getState().push({ id: 'g2', entity: 'game', title: 'Catan', href: '/games/g2' });
    });
    render(<RecentsBar />);
    // /games/g1 is current path, should be excluded
    expect(screen.queryByTestId('recent-pill-g1')).not.toBeInTheDocument();
    expect(screen.getByTestId('recent-pill-g2')).toBeInTheDocument();
  });

  it('navigates on click', async () => {
    seedRecents();
    render(<RecentsBar />);
    await userEvent.click(screen.getByTestId('recent-pill-g2'));
    expect(mockPush).toHaveBeenCalledWith('/games/g2');
  });

  it('shows tooltip with title on hover', async () => {
    seedRecents();
    render(<RecentsBar />);
    const pill = screen.getByTestId('recent-pill-g2');
    expect(pill).toHaveAttribute('title', 'Catan');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/__tests__/RecentsBar.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement RecentsBar**

```tsx
// apps/web/src/components/layout/UserShell/RecentsBar.tsx
'use client';

import { usePathname, useRouter } from 'next/navigation';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import { useRecentsStore } from '@/stores/use-recents';

/**
 * RecentsBar — shows recent entity pills in the MiniNav right area.
 * Max 3 pills visible (4 on large screens via CSS).
 * Current page is excluded from display.
 * Hidden on mobile (< md).
 */
export function RecentsBar() {
  const items = useRecentsStore(s => s.items);
  const pathname = usePathname();
  const router = useRouter();

  const visible = items.filter(i => i.href !== pathname).slice(0, 3);

  if (visible.length === 0) return null;

  return (
    <div className="hidden md:flex items-center gap-1.5" data-testid="recents-bar">
      {visible.map(item => {
        const color = entityHsl(item.entity);
        return (
          <button
            key={item.id}
            type="button"
            data-testid={`recent-pill-${item.id}`}
            title={item.title}
            onClick={() => router.push(item.href)}
            className="flex h-7 w-7 items-center justify-center rounded-full text-[11px] font-bold transition-all duration-200 hover:scale-110 hover:shadow-md"
            style={{
              backgroundColor: `hsla(${color}, 0.1)`,
              color: `hsl(${color})`,
              outline: '1.5px solid transparent',
            }}
            onMouseEnter={e => {
              (e.currentTarget as HTMLElement).style.outlineColor = `hsla(${color}, 0.4)`;
            }}
            onMouseLeave={e => {
              (e.currentTarget as HTMLElement).style.outlineColor = 'transparent';
            }}
          >
            {item.title.charAt(0).toUpperCase()}
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/__tests__/RecentsBar.test.tsx`
Expected: all 5 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/RecentsBar.tsx apps/web/src/components/layout/UserShell/__tests__/RecentsBar.test.tsx
git commit -m "feat(nav): add RecentsBar component for MiniNav"
```

---

### Task 3: Wire RecentsBar into MiniNavSlot

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/MiniNavSlot.tsx`

- [ ] **Step 1: Add RecentsBar import and render it between flex-1 spacer and primaryAction**

In `MiniNavSlot.tsx`, add after `<div className="flex-1" />` and before the `primaryAction` button:

```tsx
// Add import at top
import { RecentsBar } from './RecentsBar';

// In the JSX, after <div className="flex-1" /> :
<RecentsBar />
```

The full modified section (lines 54-69 of MiniNavSlot.tsx):

```tsx
      <div className="flex-1" />
      <RecentsBar />
      {config.primaryAction && (
        <button
          type="button"
          onClick={config.primaryAction.onClick}
          // ... rest unchanged
```

- [ ] **Step 2: Run existing MiniNavSlot tests + RecentsBar tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/`
Expected: all PASS

- [ ] **Step 3: Export RecentsBar from barrel**

Add to `apps/web/src/components/layout/UserShell/index.ts`:

```typescript
export { RecentsBar } from './RecentsBar';
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/UserShell/MiniNavSlot.tsx apps/web/src/components/layout/UserShell/index.ts apps/web/src/components/layout/UserShell/RecentsBar.tsx
git commit -m "feat(nav): wire RecentsBar into MiniNavSlot"
```

---

## M1 — Shell Cleanup (Remove CardHand + MyHand)

### Task 4: Migrate use-card-hand consumers to use-recents

**Files:**
- Modify: ~31 files importing `useCardHand` or `use-card-hand`
- Remove: `apps/web/src/stores/use-card-hand.ts`

This is a bulk migration. The pattern for each consumer:

- [ ] **Step 1: Generate full consumer list**

Run: `cd apps/web && grep -rl "useCardHand\|use-card-hand" src/ --include="*.ts" --include="*.tsx" | grep -v __tests__ | grep -v node_modules | sort`

For each file in the list:

- If the file calls `drawCard(...)`: replace with `useRecentsStore.getState().push({ id, entity, title, href })`
- If the file reads `cards[]` for display (HandRail, DeckTrackerSync): mark for deletion in Task 5
- If the file reads `focusedIdx`, `pinnedIds`, `expandedStack`, `isHandCollapsed`: remove those usages

- [ ] **Step 2: Perform the migration file by file**

For **page-level drawCard calls** (e.g., `DashboardClient.tsx`, `LibraryHub.tsx`, etc.), the pattern is:

```typescript
// BEFORE
import { useCardHand } from '@/stores/use-card-hand';
// in component:
const drawCard = useCardHand(s => s.drawCard);
useEffect(() => {
  drawCard({ id: game.id, entity: 'game', title: game.title, href: `/games/${game.id}` });
}, [game.id]);

// AFTER
import { useRecentsStore } from '@/stores/use-recents';
// in component:
useEffect(() => {
  useRecentsStore.getState().push({ id: game.id, entity: 'game', title: game.title, href: `/games/${game.id}` });
}, [game.id]);
```

For **components that render hand cards** (DesktopHandRail, HandRailItem, DeckTrackerSync, quick-cards-carousel): these are deleted in Task 5, so skip them here.

- [ ] **Step 3: Migrate test files**

Run: `cd apps/web && grep -rl "useCardHand\|use-card-hand" src/ --include="*.test.*" | sort`

For each test file: replace `useCardHand` mocks with `useRecentsStore` mocks.

- [ ] **Step 4: Delete store file**

Delete `apps/web/src/stores/use-card-hand.ts`.

- [ ] **Step 5: Verify no broken imports**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors referencing `use-card-hand`

- [ ] **Step 6: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: all PASS (some tests may need mock updates)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor(nav): migrate use-card-hand consumers to use-recents"
```

---

### Task 5: Remove CardHand + MyHand components from shell

**Files:**
- Remove: `apps/web/src/components/layout/UserShell/DesktopHandRail.tsx`
- Remove: `apps/web/src/components/layout/UserShell/HandRailItem.tsx`
- Remove: `apps/web/src/components/layout/UserShell/HandRailToolbar.tsx`
- Remove: `apps/web/src/components/layout/MyHand/MyHandSidebar.tsx`
- Remove: `apps/web/src/components/layout/MyHand/MyHandBottomBar.tsx`
- Remove: `apps/web/src/components/layout/MyHand/MyHandSlot.tsx`
- Remove: `apps/web/src/components/layout/MyHand/MyHandSlotPicker.tsx`
- Remove: `apps/web/src/components/layout/MyHand/MyHandProvider.tsx`
- Modify: `apps/web/src/components/layout/UserShell/index.ts`
- Modify: `apps/web/src/components/layout/MyHand/index.ts`

- [ ] **Step 1: Update barrel exports**

`apps/web/src/components/layout/UserShell/index.ts` — remove lines:
```typescript
export { DesktopHandRail } from './DesktopHandRail';
export { HandRailItem } from './HandRailItem';
export { HandRailToolbar } from './HandRailToolbar';
```

`apps/web/src/components/layout/MyHand/index.ts` — remove all exports (file becomes empty or delete entirely).

- [ ] **Step 2: Delete component files**

Delete all 8 files listed above.

- [ ] **Step 3: Delete related test files**

Search and delete any test files in `__tests__/` directories for the removed components.

- [ ] **Step 4: Verify typecheck**

Run: `cd apps/web && pnpm typecheck`
Fix any remaining imports of removed components.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(nav): remove CardHand rail + MyHand sidebar components"
```

---

### Task 6: Simplify DesktopShell layout

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/DesktopShell.tsx`

- [ ] **Step 1: Rewrite DesktopShell**

```tsx
// apps/web/src/components/layout/UserShell/DesktopShell.tsx
'use client';

import type { ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';

import { MiniNavSlot } from './MiniNavSlot';
import { TopBar } from './TopBar';

interface DesktopShellProps {
  children: ReactNode;
}

/**
 * DesktopShell — Page-first layout.
 *
 * Layout:
 *   ┌───────────────────────────────┐
 *   │ TopBar (64px sticky)          │
 *   ├───────────────────────────────┤
 *   │ MiniNavSlot (48px, optional)  │
 *   ├───────────────────────────────┤
 *   │ main content (full width)     │
 *   └───────────────────────────────┘
 *
 * SessionBanner and MobileBottomBar added in M5.
 */
export function DesktopShell({ children }: DesktopShellProps) {
  return (
    <div className="min-h-dvh flex flex-col bg-[var(--nh-bg-base)]">
      <TopBar />
      <MiniNavSlot />
      <main className="flex-1 min-w-0 overflow-y-auto">{children}</main>
      <ChatSlideOverPanel />
    </div>
  );
}
```

- [ ] **Step 2: Verify build**

Run: `cd apps/web && pnpm build`
Expected: compiles without errors (some pages may show warnings if they still reference MyHand — fix in Task 4)

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm vitest run`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/UserShell/DesktopShell.tsx
git commit -m "refactor(nav): simplify DesktopShell — full-width, no sidebars"
```

---

## M2 — ConnectionBar

### Task 7: ConnectionBar types + component

**Files:**
- Create: `apps/web/src/components/ui/data-display/connection-bar/types.ts`
- Create: `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx`
- Create: `apps/web/src/components/ui/data-display/connection-bar/index.ts`
- Test: `apps/web/src/components/ui/data-display/connection-bar/__tests__/ConnectionBar.test.tsx`

- [ ] **Step 1: Create types**

```typescript
// apps/web/src/components/ui/data-display/connection-bar/types.ts
import type { LucideIcon } from 'lucide-react';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface ConnectionPip {
  entityType: MeepleEntityType;
  count: number;
  label: string;
  icon: LucideIcon;
  /** True when count === 0 or connection is unavailable — shows "+" create indicator */
  isEmpty: boolean;
}

export interface ConnectionBarProps {
  connections: ConnectionPip[];
  onPipClick: (pip: ConnectionPip, anchorRect: DOMRect) => void;
  className?: string;
  'data-testid'?: string;
}
```

- [ ] **Step 2: Write failing tests**

```tsx
// apps/web/src/components/ui/data-display/connection-bar/__tests__/ConnectionBar.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { Bot, FileText, MessageCircle, Dices } from 'lucide-react';
import { ConnectionBar } from '../ConnectionBar';
import type { ConnectionPip } from '../types';

const mockPips: ConnectionPip[] = [
  { entityType: 'agent', count: 1, label: 'Agent', icon: Bot },
  { entityType: 'kb', count: 3, label: 'KB', icon: FileText },
  { entityType: 'chat', count: 0, label: 'Chat', icon: MessageCircle },
  { entityType: 'session', count: 23, label: 'Sessioni', icon: Dices },
];

describe('ConnectionBar', () => {
  it('renders a pill for each connection', () => {
    render(<ConnectionBar connections={mockPips} onPipClick={vi.fn()} />);
    expect(screen.getByText('Agent')).toBeInTheDocument();
    expect(screen.getByText('KB')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
  });

  it('shows count badge when count > 0', () => {
    render(<ConnectionBar connections={mockPips} onPipClick={vi.fn()} />);
    expect(screen.getByText('3')).toBeInTheDocument();  // KB count
    expect(screen.getByText('23')).toBeInTheDocument(); // Session count
  });

  it('shows "+" indicator when count is 0', () => {
    render(<ConnectionBar connections={mockPips} onPipClick={vi.fn()} />);
    const chatPip = screen.getByTestId('connection-pip-chat');
    expect(chatPip).toHaveTextContent('+');
  });

  it('calls onPipClick with pip and anchorRect', async () => {
    const handler = vi.fn();
    render(<ConnectionBar connections={mockPips} onPipClick={handler} />);
    await userEvent.click(screen.getByTestId('connection-pip-kb'));
    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler.mock.calls[0][0].entityType).toBe('kb');
    expect(handler.mock.calls[0][1]).toBeInstanceOf(DOMRect);
  });

  it('renders nothing when connections array is empty', () => {
    const { container } = render(<ConnectionBar connections={[]} onPipClick={vi.fn()} />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 3: Implement ConnectionBar**

```tsx
// apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx
'use client';

import { useRef } from 'react';

import { Plus } from 'lucide-react';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';

import type { ConnectionBarProps, ConnectionPip } from './types';

export function ConnectionBar({
  connections,
  onPipClick,
  className,
  'data-testid': testId,
}: ConnectionBarProps) {
  if (connections.length === 0) return null;

  return (
    <div
      className={cn('flex items-center gap-2 overflow-x-auto py-2', className)}
      data-testid={testId ?? 'connection-bar'}
    >
      {connections.map(pip => (
        <ConnectionPipButton key={pip.entityType} pip={pip} onClick={onPipClick} />
      ))}
    </div>
  );
}

function ConnectionPipButton({
  pip,
  onClick,
}: {
  pip: ConnectionPip;
  onClick: ConnectionBarProps['onPipClick'];
}) {
  const ref = useRef<HTMLButtonElement>(null);
  const color = entityHsl(pip.entityType);
  const Icon = pip.icon;
  const isEmpty = pip.isEmpty;

  const handleClick = () => {
    if (!ref.current) return;
    onClick(pip, ref.current.getBoundingClientRect());
  };

  return (
    <button
      ref={ref}
      type="button"
      data-testid={`connection-pip-${pip.entityType}`}
      onClick={handleClick}
      className={cn(
        'flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-bold transition-all duration-200',
        'hover:scale-[1.03] hover:shadow-md',
        isEmpty && 'border border-dashed opacity-60'
      )}
      style={{
        backgroundColor: isEmpty ? 'transparent' : `hsla(${color}, 0.1)`,
        color: `hsl(${color})`,
        borderColor: isEmpty ? `hsla(${color}, 0.3)` : 'transparent',
      }}
    >
      <Icon className="h-3.5 w-3.5" strokeWidth={2} />
      {isEmpty ? (
        <Plus className="h-3 w-3" strokeWidth={2.5} />
      ) : (
        <span className="tabular-nums">{pip.count}</span>
      )}
      <span>{pip.label}</span>
    </button>
  );
}
```

- [ ] **Step 4: Create barrel export**

```typescript
// apps/web/src/components/ui/data-display/connection-bar/index.ts
export { ConnectionBar } from './ConnectionBar';
export type { ConnectionPip, ConnectionBarProps } from './types';
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/connection-bar/`
Expected: all 5 tests PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/connection-bar/
git commit -m "feat(nav): add ConnectionBar component with entity-colored pills"
```

---

### Task 8: ConnectionBar config builders per entity type

**Files:**
- Create: `apps/web/src/components/ui/data-display/connection-bar/build-connections.ts`
- Test: `apps/web/src/components/ui/data-display/connection-bar/__tests__/build-connections.test.ts`

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/ui/data-display/connection-bar/__tests__/build-connections.test.ts
import { describe, it, expect } from 'vitest';
import { buildGameConnections, buildSessionConnections } from '../build-connections';

describe('buildGameConnections', () => {
  it('returns 4 pips: agent, kb, chat, session', () => {
    const pips = buildGameConnections({ agentCount: 1, kbCount: 3, chatCount: 5, sessionCount: 23 });
    expect(pips).toHaveLength(4);
    expect(pips.map(p => p.entityType)).toEqual(['agent', 'kb', 'chat', 'session']);
  });

  it('sets count correctly', () => {
    const pips = buildGameConnections({ agentCount: 0, kbCount: 2, chatCount: 0, sessionCount: 1 });
    expect(pips[0].count).toBe(0); // agent
    expect(pips[1].count).toBe(2); // kb
  });
});

describe('buildSessionConnections', () => {
  it('returns 4 pips: game, player, tool, agent', () => {
    const pips = buildSessionConnections({ gameCount: 1, playerCount: 4, toolCount: 3, agentCount: 1 });
    expect(pips).toHaveLength(4);
    expect(pips.map(p => p.entityType)).toEqual(['game', 'player', 'tool', 'agent']);
  });
});
```

- [ ] **Step 2: Implement builders for all 9 entity types**

```typescript
// apps/web/src/components/ui/data-display/connection-bar/build-connections.ts
import {
  Bot, BookOpen, Calendar, Dices, FileText, Gamepad2,
  MessageCircle, Star, Users, Wrench,
} from 'lucide-react';

import type { ConnectionPip } from './types';

export function buildGameConnections(counts: {
  agentCount: number; kbCount: number; chatCount: number; sessionCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'agent', count: counts.agentCount, label: 'Agent', icon: Bot, isEmpty: counts.agentCount === 0 },
    { entityType: 'kb', count: counts.kbCount, label: 'KB', icon: FileText, isEmpty: counts.kbCount === 0 },
    { entityType: 'chat', count: counts.chatCount, label: 'Chat', icon: MessageCircle, isEmpty: counts.chatCount === 0 },
    { entityType: 'session', count: counts.sessionCount, label: 'Sessioni', icon: Dices, isEmpty: counts.sessionCount === 0 },
  ];
}

export function buildPlayerConnections(counts: {
  sessionCount: number; favoriteGameCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'session', count: counts.sessionCount, label: 'Sessioni', icon: Dices },
    { entityType: 'game', count: counts.favoriteGameCount, label: 'Preferiti', icon: Star },
  ];
}

export function buildSessionConnections(counts: {
  gameCount: number; playerCount: number; toolCount: number; agentCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'game', count: counts.gameCount, label: 'Gioco', icon: Gamepad2 },
    { entityType: 'player', count: counts.playerCount, label: 'Giocatori', icon: Users },
    { entityType: 'tool', count: counts.toolCount, label: 'Tools', icon: Wrench },
    { entityType: 'agent', count: counts.agentCount, label: 'Agente', icon: Bot },
  ];
}

export function buildAgentConnections(counts: {
  gameCount: number; kbCount: number; chatCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'game', count: counts.gameCount, label: 'Gioco', icon: Gamepad2 },
    { entityType: 'kb', count: counts.kbCount, label: 'KB', icon: FileText },
    { entityType: 'chat', count: counts.chatCount, label: 'Chat', icon: MessageCircle },
  ];
}

export function buildKbConnections(counts: {
  gameCount: number; agentCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'game', count: counts.gameCount, label: 'Gioco', icon: Gamepad2 },
    { entityType: 'agent', count: counts.agentCount, label: 'Agente', icon: Bot },
  ];
}

export function buildChatConnections(counts: {
  agentCount: number; gameCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'agent', count: counts.agentCount, label: 'Agente', icon: Bot },
    { entityType: 'game', count: counts.gameCount, label: 'Gioco', icon: Gamepad2 },
  ];
}

export function buildEventConnections(counts: {
  participantCount: number; gameCount: number; sessionCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'player', count: counts.participantCount, label: 'Partecipanti', icon: Users },
    { entityType: 'game', count: counts.gameCount, label: 'Giochi', icon: Dices },
    { entityType: 'session', count: counts.sessionCount, label: 'Sessioni', icon: Dices },
  ];
}

export function buildToolkitConnections(counts: {
  gameCount: number; toolCount: number; sessionCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'game', count: counts.gameCount, label: 'Gioco', icon: Gamepad2 },
    { entityType: 'tool', count: counts.toolCount, label: 'Tools', icon: Wrench },
    { entityType: 'session', count: counts.sessionCount, label: 'Sessioni', icon: Dices },
  ];
}

export function buildToolConnections(counts: {
  toolkitCount: number;
}): ConnectionPip[] {
  return [
    { entityType: 'toolkit', count: counts.toolkitCount, label: 'Toolkit', icon: Wrench },
  ];
}
```

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/connection-bar/`
Expected: all PASS

- [ ] **Step 4: Export builders from barrel**

Add to `connection-bar/index.ts`:
```typescript
export {
  buildGameConnections,
  buildPlayerConnections,
  buildSessionConnections,
  buildAgentConnections,
  buildKbConnections,
  buildChatConnections,
  buildEventConnections,
  buildToolkitConnections,
  buildToolConnections,
} from './build-connections';
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/connection-bar/
git commit -m "feat(nav): add ConnectionBar config builders for all 9 entity types"
```

---

### Task 9: Wire ConnectionBar to cascade navigation

**Files:**
- Create: `apps/web/src/hooks/useConnectionBarNav.ts`
- Test: `apps/web/src/hooks/__tests__/useConnectionBarNav.test.ts`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/hooks/__tests__/useConnectionBarNav.test.ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useConnectionBarNav } from '../useConnectionBarNav';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

describe('useConnectionBarNav', () => {
  beforeEach(() => {
    act(() => useCascadeNavigationStore.getState().closeCascade());
  });

  it('opens DeckStack when count === 1 (DeckStack auto-resolves single item to drawer)', () => {
    const { result } = renderHook(() => useConnectionBarNav('game-123'));
    const pip = { entityType: 'agent' as const, count: 1, label: 'Agent', icon: vi.fn(), isEmpty: false };
    const rect = new DOMRect(100, 200, 50, 30);

    act(() => result.current.handlePipClick(pip, rect));

    const state = useCascadeNavigationStore.getState();
    // Hook opens DeckStack; the DeckStack component auto-resolves single-item to drawer.
    // The hook can't call openDrawer directly because it doesn't know the target entityId.
    expect(state.state).toBe('deckStack');
    expect(state.activeEntityType).toBe('agent');
    expect(state.sourceEntityId).toBe('game-123');
  });

  it('opens DeckStack when count >= 2', () => {
    const { result } = renderHook(() => useConnectionBarNav('game-123'));
    const pip = { entityType: 'kb' as const, count: 3, label: 'KB', icon: vi.fn() };
    const rect = new DOMRect(100, 200, 50, 30);

    act(() => result.current.handlePipClick(pip, rect));

    const state = useCascadeNavigationStore.getState();
    expect(state.state).toBe('deckStack');
    expect(state.activeEntityType).toBe('kb');
    expect(state.sourceEntityId).toBe('game-123');
  });
});
```

- [ ] **Step 2: Implement hook**

```typescript
// apps/web/src/hooks/useConnectionBarNav.ts
'use client';

import { useCallback } from 'react';

import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

import type { ConnectionPip } from '@/components/ui/data-display/connection-bar';

/**
 * Hook that wires ConnectionBar pip clicks to the cascade navigation store.
 * Rules (from spec):
 *   count === 0 → create action (handled by caller via onCreateEntity)
 *   count === 1 → drawer opens directly
 *   count >= 2  → DeckStack fan → click → drawer
 */
export function useConnectionBarNav(sourceEntityId: string) {
  const openDeckStack = useCascadeNavigationStore(s => s.openDeckStack);
  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  const handlePipClick = useCallback(
    (pip: ConnectionPip, anchorRect: DOMRect) => {
      if (pip.isEmpty) {
        // count === 0: create action — caller handles via onCreateEntity callback
        return;
      }
      if (pip.count === 1) {
        // Single entity: open drawer directly (no DeckStack intermediate)
        // The actual entityId must be resolved by the page — this opens the DeckStack
        // which auto-resolves to drawer when it has exactly 1 item.
        openDeckStack(pip.entityType, sourceEntityId, anchorRect);
        return;
      }
      // count >= 2: show DeckStack fan
      openDeckStack(pip.entityType, sourceEntityId, anchorRect);
    },
    [sourceEntityId, openDeckStack, openDrawer]
  );

  return { handlePipClick };
}
```

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useConnectionBarNav.test.ts`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/hooks/useConnectionBarNav.ts apps/web/src/hooks/__tests__/useConnectionBarNav.test.ts
git commit -m "feat(nav): add useConnectionBarNav hook wiring pips to cascade store"
```

---

## M3 — DrawerActionFooter + Drawer Stack

### Task 10: DrawerActionFooter component

**Files:**
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/DrawerActionFooter.tsx`
- Test: `apps/web/src/components/ui/data-display/extra-meeple-card/__tests__/DrawerActionFooter.test.tsx`

- [ ] **Step 1: Write failing tests**

```tsx
// apps/web/src/components/ui/data-display/extra-meeple-card/__tests__/DrawerActionFooter.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { Play, ExternalLink } from 'lucide-react';
import { DrawerActionFooter } from '../DrawerActionFooter';

describe('DrawerActionFooter', () => {
  it('renders enabled actions', () => {
    render(
      <DrawerActionFooter actions={[
        { icon: Play, label: 'Gioca', onClick: vi.fn(), variant: 'primary', enabled: true },
        { icon: ExternalLink, label: 'Apri', onClick: vi.fn(), variant: 'secondary', enabled: true },
      ]} />
    );
    expect(screen.getByText('Gioca')).toBeInTheDocument();
    expect(screen.getByText('Apri')).toBeInTheDocument();
  });

  it('hides disabled actions', () => {
    render(
      <DrawerActionFooter actions={[
        { icon: Play, label: 'Gioca', onClick: vi.fn(), variant: 'primary', enabled: false },
        { icon: ExternalLink, label: 'Apri', onClick: vi.fn(), variant: 'secondary', enabled: true },
      ]} />
    );
    expect(screen.queryByText('Gioca')).not.toBeInTheDocument();
    expect(screen.getByText('Apri')).toBeInTheDocument();
  });

  it('calls onClick when action clicked', async () => {
    const handler = vi.fn();
    render(
      <DrawerActionFooter actions={[
        { icon: Play, label: 'Gioca', onClick: handler, variant: 'primary', enabled: true },
      ]} />
    );
    await userEvent.click(screen.getByText('Gioca'));
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('renders nothing when all actions disabled', () => {
    const { container } = render(
      <DrawerActionFooter actions={[
        { icon: Play, label: 'Gioca', onClick: vi.fn(), variant: 'primary', enabled: false },
      ]} />
    );
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Implement DrawerActionFooter**

```tsx
// apps/web/src/components/ui/data-display/extra-meeple-card/DrawerActionFooter.tsx
'use client';

import type { LucideIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface DrawerAction {
  icon: LucideIcon;
  label: string;
  onClick: () => void;
  variant: 'primary' | 'secondary' | 'danger';
  enabled: boolean;
}

interface DrawerActionFooterProps {
  actions: DrawerAction[];
  className?: string;
}

const variantStyles: Record<DrawerAction['variant'], string> = {
  primary:
    'bg-[var(--nh-bg-surface)] text-[var(--nh-text-primary)] font-bold hover:bg-[var(--nh-bg-surface-hover)]',
  secondary:
    'bg-transparent text-[var(--nh-text-secondary)] hover:bg-[var(--nh-bg-surface)]',
  danger:
    'bg-transparent text-red-600 hover:bg-red-50 dark:hover:bg-red-950/20',
};

export function DrawerActionFooter({ actions, className }: DrawerActionFooterProps) {
  const visible = actions.filter(a => a.enabled);
  if (visible.length === 0) return null;

  return (
    <div
      className={cn(
        'flex items-center gap-2 border-t border-[var(--nh-border-default)] px-4 py-3',
        className
      )}
      data-testid="drawer-action-footer"
    >
      {visible.map(action => {
        const Icon = action.icon;
        return (
          <button
            key={action.label}
            type="button"
            onClick={action.onClick}
            className={cn(
              'flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-semibold transition-colors',
              variantStyles[action.variant]
            )}
          >
            <Icon className="h-3.5 w-3.5" />
            {action.label}
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 3: Run tests, verify PASS**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/extra-meeple-card/__tests__/DrawerActionFooter.test.tsx`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/extra-meeple-card/DrawerActionFooter.tsx apps/web/src/components/ui/data-display/extra-meeple-card/__tests__/DrawerActionFooter.test.tsx
git commit -m "feat(drawer): add DrawerActionFooter with conditional actions"
```

---

### Task 11: Update cascade-navigation-store with drawer stack

**Files:**
- Modify: `apps/web/src/lib/stores/cascade-navigation-store.ts`
- Test: `apps/web/src/lib/stores/__tests__/cascade-navigation-store.test.ts`

- [ ] **Step 1: Write failing tests for new actions**

```typescript
// apps/web/src/lib/stores/__tests__/cascade-navigation-store.test.ts
// Add to existing test file or create new:
import { act } from 'react';
import { describe, it, expect, beforeEach } from 'vitest';
import { useCascadeNavigationStore } from '../cascade-navigation-store';

describe('drawer stack', () => {
  beforeEach(() => {
    act(() => useCascadeNavigationStore.getState().closeCascade());
  });

  it('pushDrawer pushes current onto stack and opens new', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().pushDrawer('agent', 'a1'));

    const state = store.getState();
    expect(state.state).toBe('drawer');
    expect(state.activeEntityType).toBe('agent');
    expect(state.activeEntityId).toBe('a1');
    expect(state.drawerStack).toHaveLength(1);
    expect(state.drawerStack[0].entityType).toBe('game');
    expect(state.drawerStack[0].entityId).toBe('g1');
  });

  it('popDrawer restores previous drawer from stack', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().pushDrawer('agent', 'a1'));
    act(() => store.getState().popDrawer());

    const state = store.getState();
    expect(state.state).toBe('drawer');
    expect(state.activeEntityType).toBe('game');
    expect(state.activeEntityId).toBe('g1');
    expect(state.drawerStack).toHaveLength(0);
  });

  it('popDrawer closes drawer when stack is empty', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().popDrawer());

    expect(store.getState().state).toBe('closed');
  });

  it('drawer stack max depth is 3', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().pushDrawer('agent', 'a1'));
    act(() => store.getState().pushDrawer('kb', 'k1'));
    act(() => store.getState().pushDrawer('chat', 'c1'));
    act(() => store.getState().pushDrawer('session', 's1')); // exceeds 3

    const state = store.getState();
    expect(state.drawerStack).toHaveLength(3);
    // oldest (game g1) was evicted
    expect(state.drawerStack[0].entityType).toBe('agent');
  });

  it('openDrawer accepts optional tabId', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('session', 's1', 'toolkit'));

    expect(store.getState().activeTabId).toBe('toolkit');
  });

  it('closeCascade clears the stack', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().pushDrawer('agent', 'a1'));
    act(() => store.getState().closeCascade());

    expect(store.getState().drawerStack).toHaveLength(0);
    expect(store.getState().state).toBe('closed');
  });
});
```

- [ ] **Step 2: Update store implementation**

Add to `CascadeNavigationState` interface:
```typescript
activeTabId: string | null;
drawerStack: DrawerStackEntry[];
pushDrawer: (entityType: MeepleEntityType, entityId: string) => void;
popDrawer: () => void;
```

Add `DrawerStackEntry` type:
```typescript
interface DrawerStackEntry {
  entityType: MeepleEntityType;
  entityId: string;
  activeTabId?: string;
}
```

Update `initialState`:
```typescript
activeTabId: null as string | null,
drawerStack: [] as DrawerStackEntry[],
```

Update `openDrawer` to accept optional `tabId`:
```typescript
openDrawer: (entityType: MeepleEntityType, entityId: string, tabId?: string) => {
  const current = get();
  const skipped = current.state !== 'deckStack';
  set({
    state: 'drawer',
    activeEntityType: entityType,
    activeEntityId: entityId,
    activeTabId: tabId ?? null,
    deckStackSkipped: skipped,
  }, false, 'openDrawer');
},
```

Add `pushDrawer`:
```typescript
pushDrawer: (entityType: MeepleEntityType, entityId: string) => {
  const current = get();
  const entry: DrawerStackEntry = {
    entityType: current.activeEntityType!,
    entityId: current.activeEntityId!,
    activeTabId: current.activeTabId ?? undefined,
  };
  const stack = [...current.drawerStack, entry].slice(-3); // max 3
  set({
    state: 'drawer',
    activeEntityType: entityType,
    activeEntityId: entityId,
    activeTabId: null,
    drawerStack: stack,
  }, false, 'pushDrawer');
},
```

Add `popDrawer`:
```typescript
popDrawer: () => {
  const current = get();
  if (current.drawerStack.length === 0) {
    get().closeDrawer();
    return;
  }
  const stack = [...current.drawerStack];
  const prev = stack.pop()!;
  set({
    state: 'drawer',
    activeEntityType: prev.entityType,
    activeEntityId: prev.entityId,
    activeTabId: prev.activeTabId ?? null,
    drawerStack: stack,
  }, false, 'popDrawer');
},
```

Update `closeCascade` to clear stack:
```typescript
closeCascade: () => {
  set({ ...initialState }, false, 'closeCascade');
},
```

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm vitest run src/lib/stores/__tests__/cascade-navigation-store.test.ts`
Expected: all PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/stores/cascade-navigation-store.ts apps/web/src/lib/stores/__tests__/cascade-navigation-store.test.ts
git commit -m "feat(nav): add drawer stack (push/pop) to cascade navigation store"
```

---

### Task 12: Update ExtraMeepleCardDrawer with stack nav + action footer slot

**Files:**
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx`
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/drawer-test-ids.ts`

- [ ] **Step 1: Add back button to drawer header when stack has entries**

In `ExtraMeepleCardDrawer.tsx`, import and use the cascade store:

```typescript
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
```

In the header section, before the Icon:
```tsx
const drawerStack = useCascadeNavigationStore(s => s.drawerStack);
const popDrawer = useCascadeNavigationStore(s => s.popDrawer);
const hasStack = drawerStack.length > 0;

// In JSX, before <Icon>:
{hasStack && (
  <button
    onClick={popDrawer}
    className="flex h-7 w-7 items-center justify-center rounded-full bg-white/20 text-white transition-colors hover:bg-white/30"
    aria-label="Indietro"
    data-testid="drawer-back-button"
  >
    <ChevronLeft className="h-3.5 w-3.5" />
  </button>
)}
```

- [ ] **Step 2: Update drawer-test-ids.ts**

```typescript
// apps/web/src/components/ui/data-display/extra-meeple-card/drawer-test-ids.ts
export const DRAWER_TEST_IDS = {
  ENTITY_LABEL: 'drawer-entity-label',
  LOADING_SKELETON: 'drawer-loading-skeleton',
  ERROR_STATE: 'drawer-error-state',
  BACK_BUTTON: 'drawer-back-button',
  ACTION_FOOTER: 'drawer-action-footer',
} as const;
```

- [ ] **Step 3: Verify build + existing tests**

Run: `cd apps/web && pnpm typecheck && pnpm vitest run src/components/ui/data-display/extra-meeple-card/`
Expected: PASS (some tests may need COMING_SOON removal — update those tests to expect new behavior)

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx apps/web/src/components/ui/data-display/extra-meeple-card/drawer-test-ids.ts
git commit -m "feat(drawer): add stack nav back button + update test IDs"
```

---

## M4 — Entity Drawer Content (5 new components)

### Task 13: PlayerDrawerContent

**Files:**
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/PlayerDrawerContent.tsx`
- Test: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/PlayerDrawerContent.test.tsx`

Implement the 3-tab player drawer (Profilo, Stats, Storico) with DrawerActionFooter. Follow the pattern of existing `GameExtraMeepleCard.tsx` — use `ENTITY_COLORS`, `EntityHeader`, `EntityTabTrigger`, `StatCard` from `shared.tsx`. Use Tabs/TabsList/TabsContent from `@/components/ui/navigation/tabs`.

Footer actions: `[📊 Confronta]` (enabled conditionally) + `[↗ Apri]` (always).

- [ ] **Step 1: Write failing tests** (render, tabs, footer conditions)
- [ ] **Step 2: Implement component**
- [ ] **Step 3: Wire into DrawerEntityRouter** (replace `DrawerComingSoon` for `player` case)
- [ ] **Step 4: Remove old PlayerExtraMeepleCard** — delete `entities/PlayerExtraMeepleCard.tsx` and remove its re-export from `EntityExtraMeepleCard.tsx`
- [ ] **Step 5: Run tests, verify PASS** (fix any imports of old component)
- [ ] **Step 6: Commit**: `feat(drawer): add PlayerDrawerContent, remove old PlayerExtraMeepleCard`

---

### Task 14: SessionDrawerContent

**Files:**
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/SessionDrawerContent.tsx`
- Test: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/SessionDrawerContent.test.tsx`
- Remove: `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCard.tsx` (after wiring)

3-tab session drawer (Live, Toolkit*, Timeline). Toolkit tab hidden if no toolkit.
Footer: `[▶️ Riprendi]` if in-progress · `[📊 Risultati]` if completed · `[↗ Apri]` always.

Player rows in Live tab are clickable → call `pushDrawer('player', playerId)`.

- [ ] **Step 1: Write failing tests**
- [ ] **Step 2: Implement component**
- [ ] **Step 3: Wire into DrawerEntityRouter** (replace `DrawerComingSoon` for `session` case)
- [ ] **Step 4: Remove old `ExtraMeepleCard.tsx`** (session-specific, now superseded)
- [ ] **Step 5: Update any imports of old ExtraMeepleCard** (grep and fix)
- [ ] **Step 6: Run tests, verify PASS**
- [ ] **Step 7: Commit**: `feat(drawer): add SessionDrawerContent, remove old ExtraMeepleCard`

---

### Task 15: EventExtraMeepleCard

**Files:**
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/EventExtraMeepleCard.tsx`
- Test: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/EventExtraMeepleCard.test.tsx`

2-tab event drawer (Overview, Programma).
Footer: `[✅ Conferma]` conditional · `[📤 Invita]` if organizer · `[↗ Apri]` always.

- [ ] **Step 1-5: TDD cycle + wire into DrawerEntityRouter**
- [ ] **Step 6: Commit**: `feat(drawer): add EventExtraMeepleCard with 2 tabs`

---

### Task 16: ToolkitExtraMeepleCard

**Files:**
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/ToolkitExtraMeepleCard.tsx`
- Test: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/ToolkitExtraMeepleCard.test.tsx`

3-tab toolkit drawer (Overview, Template, Storico).
Footer: `[▶️ Usa in sessione]` if published · `[✏️ Modifica]` if owner · `[↗ Apri]` always.

- [ ] **Step 1-5: TDD cycle + wire into DrawerEntityRouter**
- [ ] **Step 6: Commit**: `feat(drawer): add ToolkitExtraMeepleCard with 3 tabs`

---

### Task 17: ToolExtraMeepleCard

**Files:**
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/ToolExtraMeepleCard.tsx`
- Test: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/ToolExtraMeepleCard.test.tsx`

2-tab tool drawer (Dettaglio, Preview).
Footer: `[▶️ Usa]` if active session · `[✏️ Modifica]` if owner · `[↗ Apri]` always.

- [ ] **Step 1-5: TDD cycle + wire into DrawerEntityRouter**
- [ ] **Step 6: Commit**: `feat(drawer): add ToolExtraMeepleCard with 2 tabs`

---

## M5 — Session Mode

### Task 18: MobileBottomBar with Normal/Session dual state

**Files:**
- Create: `apps/web/src/components/layout/MobileBottomBar.tsx`
- Test: `apps/web/src/components/layout/__tests__/MobileBottomBar.test.tsx`

- [ ] **Step 1: Write failing tests**

```tsx
// apps/web/src/components/layout/__tests__/MobileBottomBar.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MobileBottomBar } from '../MobileBottomBar';

// Mock useDashboardMode
vi.mock('@/components/dashboard', () => ({
  useDashboardMode: vi.fn(),
}));

import { useDashboardMode } from '@/components/dashboard';

describe('MobileBottomBar', () => {
  it('renders normal mode tabs when no session active', () => {
    (useDashboardMode as ReturnType<typeof vi.fn>).mockReturnValue({
      isGameMode: false,
      activeSessionId: null,
    });
    render(<MobileBottomBar />);
    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
  });

  it('renders session mode when session is active', () => {
    (useDashboardMode as ReturnType<typeof vi.fn>).mockReturnValue({
      isGameMode: true,
      activeSessionId: 'session-123',
    });
    render(<MobileBottomBar />);
    expect(screen.getByText('Classifica')).toBeInTheDocument();
    expect(screen.queryByText('Libreria')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Implement MobileBottomBar**

```tsx
// apps/web/src/components/layout/MobileBottomBar.tsx
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import {
  BarChart3, ChevronLeft, Home, Library, MessageCircle,
  MoreHorizontal, Search, User, Wrench,
} from 'lucide-react';

import { useDashboardMode } from '@/components/dashboard';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { cn } from '@/lib/utils';

const NORMAL_TABS = [
  { href: '/dashboard', icon: Home, label: 'Home' },
  { href: '/discover', icon: Search, label: 'Cerca' },
  { href: '/library', icon: Library, label: 'Libreria' },
  { href: '/chat', icon: MessageCircle, label: 'Chat' },
  { href: '/profile', icon: User, label: 'Profilo' },
] as const;

export function MobileBottomBar() {
  const pathname = usePathname();
  const { isGameMode, activeSessionId } = useDashboardMode();
  const inSession = isGameMode && !!activeSessionId;

  if (inSession) {
    return <SessionModeBar sessionId={activeSessionId!} />;
  }

  return (
    <nav
      className="fixed bottom-0 left-0 right-0 z-30 flex items-center justify-around border-t border-border bg-background/95 px-2 pb-[env(safe-area-inset-bottom)] pt-1.5 backdrop-blur-md md:hidden"
      data-testid="mobile-bottom-bar"
    >
      {NORMAL_TABS.map(tab => {
        const active = pathname.startsWith(tab.href);
        const Icon = tab.icon;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            className={cn(
              'flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold',
              active ? 'text-[hsl(25,95%,45%)]' : 'text-muted-foreground'
            )}
          >
            <Icon className="h-5 w-5" />
            <span>{tab.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}

function SessionModeBar({ sessionId }: { sessionId: string }) {
  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  return (
    <nav
      className="fixed bottom-0 left-0 right-0 z-30 flex items-center justify-around border-t-2 border-indigo-400/60 bg-background/95 px-2 pb-[env(safe-area-inset-bottom)] pt-1.5 backdrop-blur-md md:hidden"
      data-testid="mobile-bottom-bar-session"
    >
      <button
        type="button"
        onClick={() => window.history.back()}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <ChevronLeft className="h-5 w-5" />
        <span>Back</span>
      </button>
      <button
        type="button"
        onClick={() => openDrawer('session', sessionId, 'live')}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-indigo-600"
      >
        <BarChart3 className="h-5 w-5" />
        <span>Classifica</span>
      </button>
      <button
        type="button"
        onClick={() => openDrawer('session', sessionId, 'toolkit')}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <Wrench className="h-5 w-5" />
        <span>Toolkit</span>
      </button>
      <button
        type="button"
        onClick={() => {/* open agent chat drawer */}}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <MessageCircle className="h-5 w-5" />
        <span>AI</span>
      </button>
      <button
        type="button"
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <MoreHorizontal className="h-5 w-5" />
        <span>Altro</span>
      </button>
    </nav>
  );
}
```

- [ ] **Step 3: Run tests, PASS**
- [ ] **Step 4: Commit**: `feat(nav): add MobileBottomBar with Normal/Session dual state`

---

### Task 19: SessionBanner (desktop)

**Files:**
- Create: `apps/web/src/components/layout/UserShell/SessionBanner.tsx`
- Test: `apps/web/src/components/layout/UserShell/__tests__/SessionBanner.test.tsx`

32px bar between MiniNav and content. Shows session name, turn, quick-access buttons.

- [ ] **Step 1: Write failing tests** (renders when session active, hidden otherwise, buttons open drawers)
- [ ] **Step 2: Implement SessionBanner**
- [ ] **Step 3: Wire into DesktopShell** (add `<SessionBanner />` between `<MiniNavSlot />` and `<main>`)
- [ ] **Step 4: Run tests, PASS**
- [ ] **Step 5: Commit**: `feat(nav): add SessionBanner for desktop session context`

---

### Task 20: Wire MobileBottomBar into DesktopShell

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/DesktopShell.tsx`

- [ ] **Step 1: Add MobileBottomBar import and render**

```tsx
import { MobileBottomBar } from '@/components/layout/MobileBottomBar';

// In JSX, after </main> and before <ChatSlideOverPanel />:
<div className="md:hidden">
  <MobileBottomBar />
</div>
```

- [ ] **Step 2: Verify build + tests**
- [ ] **Step 3: Commit**: `feat(nav): wire MobileBottomBar + SessionBanner into DesktopShell`

---

## Note: KB↔Chat Citation Pills (Deferred)

The spec defines inline citation pills in chat messages (`[📄 p.12 §3]`) that link to KB drawers, and a bidirectional KB.Citazioni ↔ Chat.Fonti tab system. This requires:
- Backend API changes to expose chunk page/paragraph metadata in chat responses
- A `CitationPill` component for inline rendering in chat bubbles
- Aggregation logic for the Fonti and Citazioni tabs

This is **deferred to a follow-up plan** because it depends on backend API changes not in scope for this frontend redesign. The Chat drawer (already implemented) and KB drawer (already implemented) will have placeholder content for these tabs until the API is ready. The `Citazioni` tab in KB and `Fonti` tab in Chat should render empty states with "Disponibile prossimamente" text.

---

## M6 — Integration + Cleanup

### Task 21: Wire ConnectionBar into entity pages

**Files:**
- Modify: game detail page, agent detail page, session detail page (and others as needed)

For each entity page that has a detail view:
1. Import `ConnectionBar` + the appropriate `build{Entity}Connections` builder
2. Fetch connection counts from existing API data
3. Render `<ConnectionBar>` below the title/meta section
4. Use `useConnectionBarNav` to handle pip clicks

- [ ] **Step 1: Wire game detail page** as the reference implementation
- [ ] **Step 2: Wire remaining entity pages** following the same pattern
- [ ] **Step 3: Run full test suite**
- [ ] **Step 4: Commit**: `feat(nav): wire ConnectionBar into entity detail pages`

---

### Task 22: Dead code cleanup + final validation

**Files:**
- Modify: `apps/web/src/config/component-registry.ts` — remove entries for deleted components
- Remove: any remaining dead imports, unused test files
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/EntityExtraMeepleCard.tsx` — update exports

- [ ] **Step 1: Clean component-registry.ts and component-map.ts**

Remove entries referencing `HandRailItem`, `HandRailToolbar`, `DesktopHandRail` from:
- `apps/web/src/config/component-registry.ts`
- `apps/web/src/components/admin/ui-library/component-map.ts`

- [ ] **Step 2: Final grep for dead imports**

Run: `cd apps/web && pnpm typecheck`
Fix any remaining errors.

- [ ] **Step 3: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: all PASS

- [ ] **Step 4: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: no new errors

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore(nav): dead code cleanup after navigation redesign"
```

---

## Dependency Graph

```
M0: Task 1 → Task 2 → Task 3
M1: Task 4 → Task 5 → Task 6  (depends on M0 for recents store)
M2: Task 7 → Task 8 → Task 9  (independent of M1, can run in parallel)
M3: Task 10 → Task 11 → Task 12 (depends on M2 for cascade store updates)
M4: Task 13, 14, 15, 16, 17   (depends on M3, tasks are independent of each other)
M5: Task 18, 19 → Task 20     (depends on M1 for shell cleanup + M3/Task 11 for openDrawer tabId signature)
M6: Task 21 → Task 22         (depends on M2, M3, M4, M5)
```

**Parallelizable**: M0+M2 can run simultaneously. Within M4, all 5 entity tasks are independent.
