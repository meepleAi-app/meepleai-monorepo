# Desktop UX "The Game Table" — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the desktop UX around a "Game Table" metaphor — slim 64px sidebar, breadcrumb top bar, collapsible side panels, inline game picker, and touch-first mobile live session.

**Architecture:** Evolve the existing 3-tier layout (TopNavbar + MiniNav + FloatingActionBar) into a card-centric layout: Card Rack (64px sidebar) + Top Bar (48px with breadcrumb + ⌘K) + Quick View panel (300px collapsible). Live session uses independent collapsible 3-column layout. Mobile uses bottom sheets.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, shadcn/ui, Zustand, React Query, Framer Motion, Radix UI

**Spec:** `docs/superpowers/specs/2026-03-11-desktop-ux-game-table-design.md`

---

## Epic Structure — GitHub Issues

| # | Sprint | Issue Title | Priority |
|---|--------|-------------|----------|
| 1 | S1 | Card Rack: slim 64px sidebar with hover-expand | P0 |
| 2 | S1 | Top Bar: breadcrumb auto-generated from route | P0 |
| 3 | S1 | Top Bar: integrate ⌘K Command Palette | P0 |
| 4 | S1 | Design tokens: update sidebar + layout CSS variables | P0 |
| 5 | S1 | AppShell: wire new Card Rack + Top Bar layout | P0 |
| 6 | S2 | Game Night list page with card events | P1 |
| 7 | S2 | Game Night planning: table cards + inline picker | P1 |
| 8 | S2 | Game Night: AI suggestions + timeline | P1 |
| 9 | S3 | Live Session: scoreboard + game toolkit | P1 |
| 10 | S3 | Live Session: activity feed chronology | P1 |
| 11 | S3 | Live Session: AI chat side panel | P1 |
| 12 | S3 | Live Session: media capture + pause/save | P1 |
| 13 | S4 | Collapsible panels: independent left/right | P2 |
| 14 | S4 | Quick View side panel (rules/FAQ/AI/stats) | P2 |
| 15 | S4 | Keyboard shortcuts + state persistence | P2 |
| 16 | S5 | Mobile live session layout + scorebar | P2 |
| 17 | S5 | Mobile bottom sheets (dice, AI, score) | P2 |
| 18 | S5 | Mobile safe area + reduced motion | P2 |

---

## Chunk 1: Sprint 1 — Desktop Navigation Foundation (P0)

### File Structure

**New files:**
- `apps/web/src/components/layout/CardRack/CardRack.tsx` — 64px slim sidebar, hover-expand to 240px
- `apps/web/src/components/layout/CardRack/CardRackItem.tsx` — Single nav item (icon + hover label)
- `apps/web/src/components/layout/CardRack/index.ts` — Barrel export
- `apps/web/src/components/layout/CardRack/__tests__/CardRackItem.test.tsx` — CardRackItem unit tests
- `apps/web/src/components/layout/CardRack/__tests__/CardRack.test.tsx` — CardRack unit tests
- `apps/web/src/components/layout/TopBar/TopBar.tsx` — 48px top bar (breadcrumb + ⌘K + notifications + UserMenu)
- `apps/web/src/components/layout/TopBar/index.ts` — Barrel export
- `apps/web/src/components/layout/TopBar/__tests__/TopBar.test.tsx` — Unit tests
- `apps/web/src/hooks/useCardRackState.ts` — Hover-expand state management
- `apps/web/src/hooks/__tests__/useCardRackState.test.ts` — Hook tests
- `apps/web/src/lib/breadcrumb-utils.ts` — Extracted from existing DesktopBreadcrumb.tsx (shared utility)
- `apps/web/src/lib/__tests__/breadcrumb-utils.test.ts` — Utility tests

**Modified files:**
- `apps/web/src/styles/design-tokens.css` — Add Card Rack CSS variables, update sidebar vars in Task 7
- `apps/web/src/components/layout/Breadcrumb/DesktopBreadcrumb.tsx` — Refactored to import from shared utility
- `apps/web/src/components/layout/AppShell/AppShellClient.tsx` — Replace Sidebar with CardRack, TopNavbar with TopBar
- `apps/web/src/components/layout/MiniNav/MiniNav.tsx` — Update sticky offset from top-14 to top-12
- `apps/web/src/components/layout/__tests__/AppShellClient.test.tsx` — Update test assertions for new components

---

### Task 1: Design Tokens — Add New CSS Variables

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css:67-69`

> **Important:** Do NOT modify `--sidebar-width-expanded` or `--sidebar-width-collapsed` yet — the existing AppShell still consumes them. These are removed in Task 7 when the AppShell is rewired.

- [ ] **Step 1: Add new Card Rack CSS variables (keep existing sidebar vars unchanged)**

In `apps/web/src/styles/design-tokens.css`, AFTER the existing sidebar variables (line ~69), ADD:

```css
/* Card Rack (replaces sidebar in Task 7) */
--card-rack-width: 64px;
--card-rack-hover-width: 240px;
--top-bar-height: 48px;
--quick-view-width: 300px;
--quick-view-collapsed-width: 44px;
```

- [ ] **Step 2: Verify existing token consumers don't break**

Run: `cd apps/web && npx tsc --noEmit`
Expected: PASS (adding new variables, not modifying existing ones)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/styles/design-tokens.css
git commit -m "feat(tokens): add Card Rack CSS variables for new layout"
```

---

### Task 2: Refactor Existing Breadcrumb — Extract Utility

> **Note:** An existing `DesktopBreadcrumb` component exists at `apps/web/src/components/layout/Breadcrumb/DesktopBreadcrumb.tsx` with inline `SEGMENT_LABELS`, `buildBreadcrumbs()`, `isLikelyId()`, and `formatFallbackLabel()`. We extract these into a shared utility to reuse in the TopBar. Do NOT create a duplicate — reuse the existing labels and UUID detection logic.

**Files:**
- Create: `apps/web/src/lib/breadcrumb-utils.ts` — Extract from existing `DesktopBreadcrumb.tsx`
- Create: `apps/web/src/lib/__tests__/breadcrumb-utils.test.ts`
- Modify: `apps/web/src/components/layout/Breadcrumb/DesktopBreadcrumb.tsx` — Import from new utility

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/lib/__tests__/breadcrumb-utils.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';

import { buildBreadcrumbs, isLikelyId, SEGMENT_LABELS } from '../breadcrumb-utils';

describe('SEGMENT_LABELS', () => {
  it('contains expected Italian labels', () => {
    expect(SEGMENT_LABELS.library).toBe('Libreria');
    expect(SEGMENT_LABELS.games).toBe('Catalogo');
    expect(SEGMENT_LABELS.sessions).toBe('Sessioni');
    expect(SEGMENT_LABELS['game-nights']).toBe('Serate di Gioco');
  });
});

describe('isLikelyId', () => {
  it('detects UUID format', () => {
    expect(isLikelyId('550e8400-e29b-41d4-a716-446655440000')).toBe(true);
  });

  it('detects numeric IDs', () => {
    expect(isLikelyId('12345')).toBe(true);
  });

  it('rejects named segments', () => {
    expect(isLikelyId('library')).toBe(false);
    expect(isLikelyId('game-nights')).toBe(false);
  });
});

describe('buildBreadcrumbs', () => {
  it('returns Dashboard-only for /dashboard', () => {
    const result = buildBreadcrumbs('/dashboard');
    expect(result).toEqual([
      { label: 'Dashboard', href: '/dashboard', isCurrent: true },
    ]);
  });

  it('converts simple path with Dashboard root', () => {
    const result = buildBreadcrumbs('/library');
    expect(result).toEqual([
      { label: 'Dashboard', href: '/dashboard', isCurrent: false },
      { label: 'Libreria', href: null, isCurrent: true },
    ]);
  });

  it('handles UUID segments as "Dettaglio"', () => {
    const result = buildBreadcrumbs('/game-nights/550e8400-e29b-41d4-a716-446655440000');
    expect(result[2]).toEqual({
      label: 'Dettaglio',
      href: null,
      isCurrent: true,
    });
  });

  it('handles nested path with multiple segments', () => {
    const result = buildBreadcrumbs('/library/favorites');
    expect(result).toHaveLength(3);
    expect(result[1].label).toBe('Libreria');
    expect(result[2].label).toBe('Preferiti');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/__tests__/breadcrumb-utils.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Extract utility from existing DesktopBreadcrumb**

Create `apps/web/src/lib/breadcrumb-utils.ts` by extracting the `SEGMENT_LABELS`, `isLikelyId`, `formatFallbackLabel`, and `buildBreadcrumbs` from `apps/web/src/components/layout/Breadcrumb/DesktopBreadcrumb.tsx`. Add `'game-nights': 'Serate di Gioco'` to the labels:

```typescript
export interface BreadcrumbItem {
  label: string;
  href: string | null;
  isCurrent: boolean;
}

/** Human-readable labels for known route segments (Italian) */
export const SEGMENT_LABELS: Record<string, string> = {
  dashboard: 'Dashboard',
  library: 'Libreria',
  favorites: 'Preferiti',
  wishlist: 'Wishlist',
  archived: 'Archiviati',
  private: 'Giochi privati',
  proposals: 'Proposte',
  games: 'Catalogo',
  chat: 'Chat',
  'play-records': 'Sessioni recenti',
  players: 'Giocatori',
  profile: 'Profilo',
  settings: 'Impostazioni',
  agents: 'Agenti',
  sessions: 'Sessioni',
  'knowledge-base': 'Knowledge Base',
  'game-nights': 'Serate di Gioco',
  admin: 'Admin',
  overview: 'Panoramica',
  analytics: 'Analisi',
  'ai-usage': 'Uso AI',
  'api-keys': 'Chiavi API',
  'audit-log': 'Registro audit',
  'feature-flags': 'Feature flags',
  'agent-definitions': 'Definizioni agenti',
  'agent-typologies': 'Tipologie agenti',
  'ai-lab': 'AI Lab',
  alerts: 'Avvisi',
  cache: 'Cache',
  configuration: 'Configurazione',
  faqs: 'FAQ',
  editor: 'Editor',
  new: 'Nuovo',
  edit: 'Modifica',
  toolkit: 'Toolkit',
  badges: 'Badge',
};

/** Detects whether a URL segment is likely a dynamic ID (UUID or numeric). */
export function isLikelyId(segment: string): boolean {
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(segment)) return true;
  if (/^\d+$/.test(segment)) return true;
  return false;
}

/** Converts an unrecognised segment to a human-readable label. */
export function formatFallbackLabel(segment: string): string {
  return segment
    .split('-')
    .map(word => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}

/**
 * Builds a breadcrumb trail from the current pathname.
 * Always prepends Dashboard as the root for authenticated pages.
 */
export function buildBreadcrumbs(pathname: string): BreadcrumbItem[] {
  if (pathname === '/dashboard') {
    return [{ label: 'Dashboard', href: '/dashboard', isCurrent: true }];
  }

  const crumbs: BreadcrumbItem[] = [
    { label: 'Dashboard', href: '/dashboard', isCurrent: false },
  ];

  const segments = pathname.split('/').filter(Boolean);
  let currentPath = '';

  for (let i = 0; i < segments.length; i++) {
    const segment = segments[i];
    currentPath += `/${segment}`;
    const isLast = i === segments.length - 1;

    if (isLikelyId(segment)) {
      crumbs.push({ label: 'Dettaglio', href: isLast ? null : currentPath, isCurrent: isLast });
    } else {
      const label = SEGMENT_LABELS[segment] ?? formatFallbackLabel(segment);
      crumbs.push({ label, href: isLast ? null : currentPath, isCurrent: isLast });
    }
  }

  return crumbs;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/lib/__tests__/breadcrumb-utils.test.ts`
Expected: PASS (all tests)

- [ ] **Step 5: Refactor existing DesktopBreadcrumb to import from utility**

In `apps/web/src/components/layout/Breadcrumb/DesktopBreadcrumb.tsx`, replace the inline `SEGMENT_LABELS`, `isLikelyId`, `formatFallbackLabel`, `buildBreadcrumbs`, and `BreadcrumbItem` type with imports from `@/lib/breadcrumb-utils`:

```typescript
import { buildBreadcrumbs, type BreadcrumbItem } from '@/lib/breadcrumb-utils';
```

Remove the inline implementations (lines 20-118) and keep only the component (lines 120+).

- [ ] **Step 6: Run existing breadcrumb tests to verify no regression**

Run: `cd apps/web && pnpm vitest run src/components/layout/Breadcrumb/`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/breadcrumb-utils.ts apps/web/src/lib/__tests__/breadcrumb-utils.test.ts apps/web/src/components/layout/Breadcrumb/DesktopBreadcrumb.tsx
git commit -m "refactor(breadcrumb): extract buildBreadcrumbs utility for reuse in TopBar"
```

---

### Task 3: CardRackItem — Single Navigation Icon

**Files:**
- Create: `apps/web/src/components/layout/CardRack/CardRackItem.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/CardRack/__tests__/CardRackItem.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { LayoutDashboard } from 'lucide-react';
import { describe, expect, it, vi } from 'vitest';

import { CardRackItem } from '../CardRackItem';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

describe('CardRackItem', () => {
  it('renders icon with aria-label', () => {
    render(
      <CardRackItem href="/dashboard" icon={LayoutDashboard} label="Dashboard" />
    );
    const link = screen.getByRole('link', { name: 'Dashboard' });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/dashboard');
  });

  it('shows label when isExpanded is true', () => {
    render(
      <CardRackItem
        href="/dashboard"
        icon={LayoutDashboard}
        label="Dashboard"
        isExpanded
      />
    );
    expect(screen.getByText('Dashboard')).toBeVisible();
  });

  it('applies active styles when path matches', () => {
    render(
      <CardRackItem href="/dashboard" icon={LayoutDashboard} label="Dashboard" isActive />
    );
    const link = screen.getByRole('link', { name: 'Dashboard' });
    expect(link.className).toContain('bg-');
  });

  it('renders notification badge when provided', () => {
    render(
      <CardRackItem
        href="/chat"
        icon={LayoutDashboard}
        label="Chat"
        badge={3}
      />
    );
    expect(screen.getByText('3')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/CardRack/__tests__/CardRackItem.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write the implementation**

Create `apps/web/src/components/layout/CardRack/CardRackItem.tsx`:

```tsx
'use client';

import type { LucideIcon } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

export interface CardRackItemProps {
  href: string;
  icon: LucideIcon;
  label: string;
  isActive?: boolean;
  isExpanded?: boolean;
  badge?: number | string;
}

export function CardRackItem({
  href,
  icon: Icon,
  label,
  isActive = false,
  isExpanded = false,
  badge,
}: CardRackItemProps) {
  return (
    <Link
      href={href}
      aria-label={label}
      className={cn(
        'relative flex items-center rounded-lg',
        'min-h-[44px] transition-colors duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring',
        isExpanded ? 'gap-3 px-3 py-2' : 'justify-center px-2 py-2',
        isActive
          ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
          : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
      )}
    >
      <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
      {isExpanded && (
        <span className="text-sm font-medium truncate">{label}</span>
      )}
      {badge != null && (
        <span
          className={cn(
            'absolute flex items-center justify-center',
            'min-w-[18px] h-[18px] rounded-full',
            'bg-destructive text-destructive-foreground',
            'text-[10px] font-bold px-1',
            isExpanded ? 'right-2' : 'top-1 right-1'
          )}
        >
          {typeof badge === 'number' && badge > 99 ? '99+' : badge}
        </span>
      )}
    </Link>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/CardRack/__tests__/CardRackItem.test.tsx`
Expected: PASS (all 4 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/CardRack/
git commit -m "feat(card-rack): add CardRackItem with icon, label, badge support"
```

---

### Task 4: useCardRackState — Hover Expand Hook

**Files:**
- Create: `apps/web/src/hooks/useCardRackState.ts`
- Create: `apps/web/src/hooks/__tests__/useCardRackState.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/hooks/__tests__/useCardRackState.test.ts`:

```typescript
import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import { useCardRackState } from '../useCardRackState';

describe('useCardRackState', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts collapsed (isExpanded = false)', () => {
    const { result } = renderHook(() => useCardRackState());
    expect(result.current.isExpanded).toBe(false);
  });

  it('expands on mouse enter after delay', () => {
    const { result } = renderHook(() => useCardRackState());

    act(() => {
      result.current.onMouseEnter();
    });

    // Not expanded yet (delay not elapsed)
    expect(result.current.isExpanded).toBe(false);

    act(() => {
      vi.advanceTimersByTime(200);
    });

    expect(result.current.isExpanded).toBe(true);
  });

  it('collapses on mouse leave after delay', () => {
    const { result } = renderHook(() => useCardRackState());

    // Expand first
    act(() => {
      result.current.onMouseEnter();
      vi.advanceTimersByTime(200);
    });
    expect(result.current.isExpanded).toBe(true);

    // Now leave
    act(() => {
      result.current.onMouseLeave();
      vi.advanceTimersByTime(300);
    });
    expect(result.current.isExpanded).toBe(false);
  });

  it('cancels expand if mouse leaves before delay', () => {
    const { result } = renderHook(() => useCardRackState());

    act(() => {
      result.current.onMouseEnter();
      vi.advanceTimersByTime(100); // Only half the delay
      result.current.onMouseLeave();
      vi.advanceTimersByTime(500); // Way past both delays
    });

    expect(result.current.isExpanded).toBe(false);
  });

  it('provides ref for the container element', () => {
    const { result } = renderHook(() => useCardRackState());
    expect(result.current.rackRef).toBeDefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useCardRackState.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Write the implementation**

Create `apps/web/src/hooks/useCardRackState.ts`:

```typescript
import { useCallback, useRef, useState } from 'react';

const EXPAND_DELAY = 200;   // ms before expanding on hover
const COLLAPSE_DELAY = 300; // ms before collapsing on leave

export interface UseCardRackStateReturn {
  isExpanded: boolean;
  onMouseEnter: () => void;
  onMouseLeave: () => void;
  rackRef: React.RefObject<HTMLElement | null>;
}

/**
 * Manages hover-expand state for the Card Rack sidebar.
 * Includes intentional delays to prevent accidental expand/collapse.
 */
export function useCardRackState(): UseCardRackStateReturn {
  const [isExpanded, setIsExpanded] = useState(false);
  const expandTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const collapseTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const rackRef = useRef<HTMLElement | null>(null);

  const clearTimers = useCallback(() => {
    if (expandTimer.current) {
      clearTimeout(expandTimer.current);
      expandTimer.current = null;
    }
    if (collapseTimer.current) {
      clearTimeout(collapseTimer.current);
      collapseTimer.current = null;
    }
  }, []);

  const onMouseEnter = useCallback(() => {
    clearTimers();
    expandTimer.current = setTimeout(() => {
      setIsExpanded(true);
    }, EXPAND_DELAY);
  }, [clearTimers]);

  const onMouseLeave = useCallback(() => {
    clearTimers();
    collapseTimer.current = setTimeout(() => {
      setIsExpanded(false);
    }, COLLAPSE_DELAY);
  }, [clearTimers]);

  return { isExpanded, onMouseEnter, onMouseLeave, rackRef };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useCardRackState.test.ts`
Expected: PASS (all 5 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useCardRackState.ts apps/web/src/hooks/__tests__/useCardRackState.test.ts
git commit -m "feat(card-rack): add useCardRackState hook with hover delay logic"
```

---

### Task 5: CardRack — Full Sidebar Component

**Files:**
- Create: `apps/web/src/components/layout/CardRack/CardRack.tsx`
- Create: `apps/web/src/components/layout/CardRack/index.ts`
- Modify test: `apps/web/src/components/layout/CardRack/__tests__/CardRack.test.tsx`

- [ ] **Step 1: Write the failing test**

Add to `apps/web/src/components/layout/CardRack/__tests__/CardRack.test.tsx` (or create new file `CardRack.test.tsx` alongside the item tests):

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { CardRack } from '../CardRack';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

describe('CardRack', () => {
  it('renders as a nav element with correct aria-label', () => {
    render(<CardRack />);
    const nav = screen.getByRole('navigation', { name: /sidebar/i });
    expect(nav).toBeInTheDocument();
  });

  it('renders all primary navigation items', () => {
    render(<CardRack />);
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Libreria' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Scopri' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Chat AI' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sessioni' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Serate' })).toBeInTheDocument();
  });

  it('is hidden on mobile (has hidden md:flex classes)', () => {
    render(<CardRack />);
    const nav = screen.getByRole('navigation', { name: /sidebar/i });
    expect(nav.className).toContain('hidden');
    expect(nav.className).toContain('md:flex');
  });

  it('has 64px width by default', () => {
    render(<CardRack />);
    const nav = screen.getByRole('navigation', { name: /sidebar/i });
    // Uses CSS variable, check the class
    expect(nav.className).toMatch(/w-16|w-\[64px\]|w-\[var\(--card-rack-width\)\]/);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/CardRack/__tests__/CardRack.test.tsx`
Expected: FAIL — CardRack not found

- [ ] **Step 3: Write the implementation**

Create `apps/web/src/components/layout/CardRack/CardRack.tsx`:

```tsx
'use client';

import {
  LayoutDashboard,
  Library,
  Gamepad2,
  MessageSquare,
  Play,
  Calendar,
  Award,
  BookOpen,
} from 'lucide-react';
import { usePathname } from 'next/navigation';

import { useCardRackState } from '@/hooks/useCardRackState';
import { cn } from '@/lib/utils';

import { CardRackItem } from './CardRackItem';

const NAV_ITEMS = [
  { href: '/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
  { href: '/library', icon: Library, label: 'Libreria' },
  { href: '/games', icon: Gamepad2, label: 'Scopri' },
  { href: '/chat', icon: MessageSquare, label: 'Chat AI' },
  { href: '/sessions', icon: Play, label: 'Sessioni' },
  { href: '/game-nights', icon: Calendar, label: 'Serate' },
] as const;

const BOTTOM_ITEMS = [
  { href: '/agents', icon: BookOpen, label: 'Agenti' },
  { href: '/badges', icon: Award, label: 'Badge' },
] as const;

export interface CardRackProps {
  className?: string;
}

export function CardRack({ className }: CardRackProps) {
  const pathname = usePathname();
  const { isExpanded, onMouseEnter, onMouseLeave, rackRef } = useCardRackState();

  const isActive = (href: string) =>
    href === '/dashboard' ? pathname === '/dashboard' : pathname?.startsWith(href);

  return (
    <nav
      ref={rackRef as React.Ref<HTMLElement>}
      onMouseEnter={onMouseEnter}
      onMouseLeave={onMouseLeave}
      aria-label="Main sidebar navigation"
      data-testid="card-rack"
      className={cn(
        'hidden md:flex flex-col',
        'fixed left-0 z-40',
        'top-[var(--top-bar-height,48px)]',
        'h-[calc(100vh-var(--top-bar-height,48px))]',
        'bg-sidebar text-sidebar-foreground',
        'border-r border-sidebar-border',
        'transition-[width] duration-200 ease-in-out motion-reduce:transition-none',
        isExpanded ? 'w-[var(--card-rack-hover-width,240px)]' : 'w-[var(--card-rack-width,64px)]',
        className
      )}
    >
      {/* Primary navigation items */}
      <div className="flex-1 flex flex-col gap-0.5 px-2 py-3 overflow-y-auto">
        {NAV_ITEMS.map((item) => (
          <CardRackItem
            key={item.href}
            href={item.href}
            icon={item.icon}
            label={item.label}
            isActive={!!isActive(item.href)}
            isExpanded={isExpanded}
          />
        ))}
      </div>

      {/* Divider */}
      <hr className="mx-3 border-sidebar-border" />

      {/* Bottom items */}
      <div className="flex flex-col gap-0.5 px-2 py-3">
        {BOTTOM_ITEMS.map((item) => (
          <CardRackItem
            key={item.href}
            href={item.href}
            icon={item.icon}
            label={item.label}
            isActive={!!isActive(item.href)}
            isExpanded={isExpanded}
          />
        ))}
      </div>
    </nav>
  );
}
```

Create `apps/web/src/components/layout/CardRack/index.ts`:

```typescript
export { CardRack } from './CardRack';
export type { CardRackProps } from './CardRack';
export { CardRackItem } from './CardRackItem';
export type { CardRackItemProps } from './CardRackItem';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/CardRack/__tests__/`
Expected: PASS (all CardRack + CardRackItem tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/CardRack/
git commit -m "feat(card-rack): add CardRack sidebar with 64px/240px hover-expand"
```

---

### Task 6: TopBar — Breadcrumb + Command Palette

**Files:**
- Create: `apps/web/src/components/layout/TopBar/DesktopBreadcrumb.tsx`
- Create: `apps/web/src/components/layout/TopBar/TopBar.tsx`
- Create: `apps/web/src/components/layout/TopBar/index.ts`
- Create: `apps/web/src/components/layout/TopBar/__tests__/TopBar.test.tsx`

- [ ] **Step 1: Write the failing test for DesktopBreadcrumb**

Create `apps/web/src/components/layout/TopBar/__tests__/DesktopBreadcrumb.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { DesktopBreadcrumb } from '../DesktopBreadcrumb';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/game-nights/abc123',
}));

describe('DesktopBreadcrumb', () => {
  it('renders breadcrumb navigation', () => {
    render(<DesktopBreadcrumb />);
    const nav = screen.getByRole('navigation', { name: /breadcrumb/i });
    expect(nav).toBeInTheDocument();
  });

  it('renders home link', () => {
    render(<DesktopBreadcrumb />);
    expect(screen.getByRole('link', { name: /home/i })).toBeInTheDocument();
  });

  it('renders intermediate segments as links', () => {
    render(<DesktopBreadcrumb />);
    expect(screen.getByRole('link', { name: /serate di gioco/i })).toBeInTheDocument();
  });

  it('renders last segment as current (not a link)', () => {
    render(<DesktopBreadcrumb />);
    const current = screen.getByText('abc123');
    expect(current.closest('a')).toBeNull();
  });

  it('uses separator between segments', () => {
    render(<DesktopBreadcrumb />);
    const separators = screen.getAllByText('›');
    expect(separators.length).toBeGreaterThanOrEqual(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/TopBar/__tests__/DesktopBreadcrumb.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement DesktopBreadcrumb**

Create `apps/web/src/components/layout/TopBar/DesktopBreadcrumb.tsx`:

```tsx
'use client';

import { Home } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { pathToBreadcrumbs } from '@/lib/breadcrumb-utils';
import { cn } from '@/lib/utils';

export function DesktopBreadcrumb() {
  const pathname = usePathname();
  const segments = pathToBreadcrumbs(pathname ?? '/');

  return (
    <nav aria-label="Breadcrumb" className="hidden md:flex items-center gap-1 text-sm">
      <ol className="flex items-center gap-1">
        {segments.map((segment, index) => {
          const isFirst = index === 0;
          const isLast = index === segments.length - 1;

          return (
            <li key={segment.href} className="flex items-center gap-1">
              {index > 0 && (
                <span className="text-muted-foreground/50 select-none" aria-hidden="true">
                  ›
                </span>
              )}
              {isLast ? (
                <span
                  className="text-foreground font-medium truncate max-w-[200px]"
                  aria-current="page"
                >
                  {isFirst ? <Home className="h-4 w-4" /> : segment.label}
                </span>
              ) : (
                <Link
                  href={segment.href}
                  className={cn(
                    'text-muted-foreground hover:text-foreground transition-colors',
                    'truncate max-w-[160px]'
                  )}
                >
                  {isFirst ? (
                    <Home className="h-4 w-4" aria-label="Home" />
                  ) : (
                    segment.label
                  )}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/TopBar/__tests__/DesktopBreadcrumb.test.tsx`
Expected: PASS

- [ ] **Step 5: Write the TopBar test**

Create `apps/web/src/components/layout/TopBar/__tests__/TopBar.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { TopBar } from '../TopBar';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

// Mock child components to isolate TopBar
vi.mock('@/components/layout/Breadcrumb/DesktopBreadcrumb', () => ({
  DesktopBreadcrumb: () => <nav aria-label="Percorso di navigazione">breadcrumb</nav>,
}));

vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">bell</button>,
}));

vi.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: () => ({ user: { displayName: 'Test', role: 'user' } }),
}));

vi.mock('@/hooks/useScrollState', () => ({
  useScrollState: () => ({ isScrolled: false }),
}));

vi.mock('@/hooks/useCommandPalette', () => ({
  useCommandPalette: () => ({ toggle: vi.fn() }),
}));

vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn(),
}));

describe('TopBar', () => {
  it('renders as a header element', () => {
    render(<TopBar />);
    const header = screen.getByTestId('top-bar');
    expect(header.tagName).toBe('HEADER');
  });

  it('contains breadcrumb navigation', () => {
    render(<TopBar />);
    expect(screen.getByRole('navigation', { name: /percorso/i })).toBeInTheDocument();
  });

  it('contains search trigger with "Cerca..." text', () => {
    render(<TopBar />);
    expect(screen.getByText('Cerca...')).toBeInTheDocument();
  });

  it('contains notifications', () => {
    render(<TopBar />);
    expect(screen.getByRole('button', { name: /notifications/i })).toBeInTheDocument();
  });

  it('contains user menu', () => {
    render(<TopBar />);
    expect(screen.getByRole('button', { name: /user menu/i })).toBeInTheDocument();
  });

  it('has 48px height', () => {
    render(<TopBar />);
    const header = screen.getByTestId('top-bar');
    expect(header.className).toContain('h-12');
  });
});
```

- [ ] **Step 6: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/TopBar/__tests__/TopBar.test.tsx`
Expected: FAIL — TopBar not found

- [ ] **Step 7: Implement TopBar**

Create `apps/web/src/components/layout/TopBar/TopBar.tsx`:

> **Important:** Reuse the existing `DesktopBreadcrumb` from `@/components/layout/Breadcrumb/DesktopBreadcrumb` — do NOT create a new one. Include `UserMenu` from the existing `TopNavbar` pattern (profile, settings, logout).

```tsx
'use client';

import { Suspense } from 'react';

import { ChevronDown, LogOut, Search, Settings, User } from 'lucide-react';
import Link from 'next/link';

import { logoutAction } from '@/actions/auth';
import { DesktopBreadcrumb } from '@/components/layout/Breadcrumb/DesktopBreadcrumb';
import { NotificationBell } from '@/components/notifications';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useCommandPalette } from '@/hooks/useCommandPalette';
import { useScrollState } from '@/hooks/useScrollState';
import { cn } from '@/lib/utils';

import { MobileNavDrawer } from '../MobileNavDrawer';
import { Logo } from '../Navbar/Logo';

// ─── User Menu (adapted from TopNavbar) ─────────────────────────────────────

function UserMenu({ userName, userRole }: { userName?: string; userRole?: string }) {
  const initials = userName
    ? userName.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2)
    : 'U';

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          aria-label="User menu"
          className="flex items-center gap-2 rounded-lg px-2 py-1.5 transition-colors duration-200 hover:bg-muted"
        >
          <div className="h-7 w-7 rounded-full bg-primary flex items-center justify-center">
            <span className="text-xs font-bold text-primary-foreground">{initials}</span>
          </div>
          <ChevronDown className="h-3 w-3 text-muted-foreground hidden sm:block" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-52">
        <DropdownMenuLabel>
          <p className="text-sm font-semibold truncate">{userName ?? 'Utente'}</p>
          <p className="text-xs text-muted-foreground capitalize font-normal">{userRole ?? 'user'}</p>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href="/profile" className="gap-2.5"><User className="h-4 w-4" />Il mio profilo</Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link href="/profile?tab=settings" className="gap-2.5"><Settings className="h-4 w-4" />Impostazioni</Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <div className="px-2 py-1">
          <ThemeToggle showLabel size="sm" className="w-full justify-start" />
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onClick={() => void logoutAction()}
          className="text-destructive focus:text-destructive gap-2.5"
        >
          <LogOut className="h-4 w-4" />Esci
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

// ─── TopBar ─────────────────────────────────────────────────────────────────

export interface TopBarProps {
  className?: string;
}

/**
 * TopBar — 48px sticky header with:
 *   Left: MobileNavDrawer (mobile) + Breadcrumb (desktop)
 *   Center: ⌘K search trigger
 *   Right: Notifications + UserMenu
 */
export function TopBar({ className }: TopBarProps) {
  const { user } = useAuthUser();
  const { isScrolled: scrolled } = useScrollState({ scrolledThreshold: 4 });
  const { toggle: toggleCommandPalette } = useCommandPalette();

  return (
    <>
      {/* Skip to main content — WCAG 2.4.1 */}
      <a
        href="#main-content"
        className={cn(
          'sr-only focus:not-sr-only',
          'focus:fixed focus:top-2 focus:left-2 focus:z-[100]',
          'focus:px-4 focus:py-2 focus:rounded-lg',
          'focus:bg-background focus:text-foreground',
          'focus:border focus:border-border',
          'focus:text-sm focus:font-semibold',
          'focus:shadow-md'
        )}
      >
        Vai al contenuto principale
      </a>

      <header
        data-testid="top-bar"
        className={cn(
          'sticky top-0 z-40 w-full',
          'h-12', // 48px
          'bg-background/95 backdrop-blur-md backdrop-saturate-150',
          'border-b border-border/60',
          'transition-shadow duration-200',
          scrolled && 'shadow-sm',
          className
        )}
      >
        <div className="flex h-full items-center justify-between px-4 md:px-6 gap-4">
          {/* LEFT: Mobile hamburger + Logo (mobile) / Breadcrumb (desktop) */}
          <div className="flex items-center gap-2 shrink-0 min-w-0">
            <Suspense>
              <MobileNavDrawer />
            </Suspense>
            <div className="md:hidden">
              <Logo variant="auto" size="sm" />
            </div>
            <DesktopBreadcrumb />
          </div>

          {/* CENTER: ⌘K search trigger */}
          <button
            onClick={toggleCommandPalette}
            className={cn(
              'hidden md:flex items-center gap-2',
              'px-3 py-1.5 rounded-lg',
              'border border-border/60 bg-muted/50',
              'text-sm text-muted-foreground',
              'hover:bg-muted hover:text-foreground',
              'transition-colors duration-150',
              'min-w-[200px] max-w-[320px]'
            )}
          >
            <Search className="h-3.5 w-3.5" />
            <span className="flex-1 text-left truncate">Cerca...</span>
            <kbd className="hidden lg:inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded border border-border text-[10px] font-mono text-muted-foreground/70">
              ⌘K
            </kbd>
          </button>

          {/* RIGHT: Notifications + UserMenu */}
          <div className="flex items-center gap-1 shrink-0">
            <NotificationBell />
            <UserMenu userName={user?.displayName ?? user?.email} userRole={user?.role} />
          </div>
        </div>
      </header>
    </>
  );
}
```

Create `apps/web/src/components/layout/TopBar/index.ts`:

```typescript
export { TopBar } from './TopBar';
export type { TopBarProps } from './TopBar';
```

- [ ] **Step 8: Run all TopBar tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/TopBar/__tests__/`
Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/layout/TopBar/ apps/web/src/lib/breadcrumb-utils.ts apps/web/src/lib/__tests__/breadcrumb-utils.test.ts
git commit -m "feat(top-bar): add TopBar with breadcrumb and ⌘K search trigger"
```

---

### Task 7: AppShell Integration — Wire CardRack + TopBar

**Files:**
- Modify: `apps/web/src/components/layout/AppShell/AppShellClient.tsx`
- Modify: `apps/web/src/components/layout/MiniNav/MiniNav.tsx`
- Modify: `apps/web/src/styles/design-tokens.css` — Now safe to update old sidebar vars
- Modify: `apps/web/src/components/layout/__tests__/AppShellClient.test.tsx` — Update assertions

> **This is the critical integration task.** It replaces Sidebar with CardRack, TopNavbar with TopBar, and updates all consumers. The existing AppShellClient test suite (~22 tests) will break and must be updated.

- [ ] **Step 1: Read the current AppShellClient and its test file**

Read both files to understand all assertions that reference `top-navbar`, `sidebar`, `--sidebar-width-expanded`, `--sidebar-width-collapsed`, and `useSidebarState`.

- [ ] **Step 2: Update CSS variables — now safe to modify old sidebar vars**

In `apps/web/src/styles/design-tokens.css`, update:

```css
--sidebar-width-expanded: 64px;   /* was 220px — now equals card-rack-width */
--sidebar-width-collapsed: 64px;  /* was 60px — Card Rack is always 64px */
```

This ensures any remaining consumers of the old variables still work during transition.

- [ ] **Step 3: Update imports in AppShellClient**

Replace:
```typescript
import { Sidebar } from '../Sidebar/Sidebar';
import { TopNavbar } from '../TopNavbar';
```
with:
```typescript
import { CardRack } from '../CardRack';
import { TopBar } from '../TopBar';
```

- [ ] **Step 4: Replace TopNavbar with TopBar in JSX**

Replace `<TopNavbar />` with `<TopBar />`.

- [ ] **Step 5: Replace Sidebar with CardRack**

Replace:
```tsx
{isAuthenticated && <Sidebar isCollapsed={isCollapsed} onToggle={toggle} />}
```
with:
```tsx
{isAuthenticated && <CardRack />}
```

- [ ] **Step 6: Update content area margin offset**

Replace the conditional sidebar width logic:
```tsx
isAuthenticated &&
  (isCollapsed
    ? 'md:ml-[var(--sidebar-width-collapsed)]'
    : 'md:ml-[var(--sidebar-width-expanded)]')
```
with:
```tsx
isAuthenticated && 'md:ml-[var(--card-rack-width,64px)]'
```

- [ ] **Step 7: Update MiniNav sticky offset from top-14 to top-12**

In `apps/web/src/components/layout/MiniNav/MiniNav.tsx`, find the `sticky top-14` class and change to `sticky top-12` (48px for new TopBar height).

- [ ] **Step 8: Update AppShellClient test suite**

In `apps/web/src/components/layout/__tests__/AppShellClient.test.tsx`, update:

1. Replace `screen.getByTestId('top-navbar')` → `screen.getByTestId('top-bar')`
2. Replace `screen.getByTestId('sidebar')` → `screen.getByTestId('card-rack')`
3. Remove assertions for `--sidebar-width-expanded` / `--sidebar-width-collapsed` conditional logic
4. Add assertion: `expect(content.className).toContain('md:ml-[var(--card-rack-width,64px)]')`
5. Update `mockUseSidebarState` references — `CardRack` uses `useCardRackState` internally, not `useSidebarState`
6. Remove tests that assert sidebar collapse toggle behavior (CardRack handles this via hover)

- [ ] **Step 9: Add MiniNav offset test**

In the MiniNav test file, add:
```tsx
it('sticks at top-12 (48px for TopBar)', () => {
  render(<MiniNav />);
  const nav = screen.getByTestId('mini-nav');
  expect(nav.className).toContain('top-12');
});
```

- [ ] **Step 10: Run all tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/`
Expected: PASS

- [ ] **Step 11: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: PASS

- [ ] **Step 12: Commit**

```bash
git add apps/web/src/components/layout/AppShell/AppShellClient.tsx apps/web/src/components/layout/MiniNav/MiniNav.tsx apps/web/src/styles/design-tokens.css apps/web/src/components/layout/__tests__/AppShellClient.test.tsx
git commit -m "feat(layout): wire CardRack + TopBar into AppShellClient, update tests"
```

---

### Task 8: Clean Up — Remove useSidebarState Toggle Dependency

**Files:**
- Modify: `apps/web/src/components/layout/AppShell/AppShellClient.tsx`
- Modify: `apps/web/src/hooks/useSidebarState.ts`

- [ ] **Step 1: Simplify AppShellClient**

The Card Rack manages its own hover state internally via `useCardRackState`. Remove the `useSidebarState` import and its `isCollapsed`/`toggle` destructure from `AppShellClient.tsx` if no longer needed by other components.

Check if `isCollapsed` is still passed to `AdaptiveBottomBar`. If so, keep a simplified version. If not, remove the hook usage entirely.

- [ ] **Step 2: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: PASS

- [ ] **Step 3: Run typecheck and lint**

Run: `cd apps/web && npx tsc --noEmit && pnpm lint`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/AppShell/AppShellClient.tsx apps/web/src/hooks/useSidebarState.ts
git commit -m "refactor(layout): remove sidebar toggle state from AppShell (CardRack self-manages)"
```

---

## Chunk 2: Sprint 2 — Game Night Experience (P1)

### Task 9: Game Night List Page — Card Events

**Files:**
- Modify: `apps/web/src/app/(authenticated)/game-nights/page.tsx`
- Create: `apps/web/src/components/game-night/GameNightCard.tsx` — Card event with status, avatar stack, mini-game images
- Create: `apps/web/src/components/game-night/__tests__/GameNightCard.test.tsx`

**Key behaviors:**
- Card states: Prossima (upcoming), Bozza (draft), Completata (completed)
- Avatar stack showing invited players (max 4 + "+N" overflow)
- Mini-card thumbnails for planned games
- Click → navigates to `/game-nights/[id]`

- [ ] **Step 1: Write failing test for GameNightCard**
- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement GameNightCard using MeepleCard entity="event" as base**
- [ ] **Step 4: Run test to verify it passes**
- [ ] **Step 5: Update game-nights list page to use GameNightCard grid**
- [ ] **Step 6: Commit**

---

### Task 10: Game Night Planning — Table Cards

**Files:**
- Modify: `apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx`
- Create: `apps/web/src/components/game-night/GameTable.tsx` — Center area with card-style game tiles
- Create: `apps/web/src/components/game-night/GameTableCard.tsx` — Single game "card on table" (slight rotation)
- Create: `apps/web/src/components/game-night/__tests__/GameTable.test.tsx`

**Key behaviors:**
- Game cards appear with slight random rotation (CSS transform rotate ±3deg)
- Drop zone "+" button to trigger inline picker
- Cards are removable (X button on hover)
- Responsive grid layout

- [ ] **Step 1: Write failing test for GameTable**
- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement GameTable and GameTableCard**
- [ ] **Step 4: Run test to verify it passes**
- [ ] **Step 5: Update game-nights/[id] page to use new layout**
- [ ] **Step 6: Commit**

---

### Task 11: Inline Picker — Collection Carousel

**Files:**
- Create: `apps/web/src/components/game-night/InlinePicker.tsx` — Horizontal carousel of mini-cards from user's collection
- Create: `apps/web/src/components/game-night/InlinePickerCard.tsx` — Mini-card in carousel
- Create: `apps/web/src/components/game-night/__tests__/InlinePicker.test.tsx`
- Create: `apps/web/src/hooks/useCollectionGames.ts` — Fetch user's collection filtered by player count/duration/complexity

**Key behaviors:**
- Triggered by click on "+" drop zone or "Aggiungi dalla collezione" button
- Horizontal scroll carousel with mini-card game thumbnails
- Auto-filters by: number of players (from invited), duration, complexity
- Click on game → adds to table → picker closes
- Keyboard navigable (arrow keys)

- [ ] **Step 1: Write failing test for InlinePicker**
- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement InlinePicker with carousel and filter logic**
- [ ] **Step 4: Run test to verify it passes**
- [ ] **Step 5: Integrate with GameTable drop zone trigger**
- [ ] **Step 6: Commit**

---

### Task 12: Game Night Planning — Player Panel + AI + Timeline

**Files:**
- Modify: `apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx`
- Create: `apps/web/src/components/game-night/PlayerPanel.tsx` — Left panel with player cards
- Create: `apps/web/src/components/game-night/AISuggestion.tsx` — AI game suggestions based on players
- Create: `apps/web/src/components/game-night/EventTimeline.tsx` — Timeline with estimated times

**Key behaviors:**
- Left panel: Info serata + player cards + AI suggestion
- AI suggestion: "Consigliati per N giocatori" with smart game picks
- Timeline: chronological list of planned activities with estimated durations
- Right panel: Quick View with rules of selected game (Sprint 4 dependency, placeholder for now)

- [ ] **Step 1-6: TDD cycle for each component**
- [ ] **Step 7: Wire into game-nights/[id] 3-column layout**
- [ ] **Step 8: Commit**

---

## Chunk 3: Sprint 3 — Live Session (P1)

### Task 13: Live Session Layout — 3-Column Structure

**Files:**
- Create: `apps/web/src/components/live-session/LiveSessionLayout.tsx` — 3-column layout wrapper
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/play/page.tsx`

**Layout:**
```
┌─────────────┬──────────────────────┬─────────────┐
│ Left (280px) │   Center (flex-1)    │ Right (280px) │
│ Scoreboard  │   Activity Feed      │ AI + Rules   │
│ + Toolkit   │   + Input Bar        │ + FAQ        │
└─────────────┴──────────────────────┴─────────────┘
```

- [ ] **Step 1-5: TDD cycle**
- [ ] **Step 6: Commit**

---

### Task 14: Scoreboard Panel — Live Scores

**Files:**
- Modify: `apps/web/src/components/session/Scoreboard.tsx` (or create new `LiveScorePanel.tsx`)
- Existing: `apps/web/src/components/game-night/LiveScoreboard.tsx` — Reuse/extend

**Key behaviors:**
- Ranked player list with PV (victory points) and breakdown
- Progress bar toward game objective
- Real-time updates via existing SignalR/SSE
- Animated score changes

- [ ] **Step 1-5: TDD cycle**
- [ ] **Step 6: Commit**

---

### Task 15: Game Toolkit Panel

**Files:**
- Modify: `apps/web/src/components/session/ToolRail.tsx` — Adapt existing tool selector
- Existing components to integrate:
  - `DiceRoller.tsx` — Add 2d6, 1d6, 1d20, custom modes
  - `CardDeck.tsx` — Card draw by deck type
  - `WheelSpinner.tsx` — Random selection
  - `CountdownTimer.tsx` — Timer tool
  - `CoinFlip.tsx` — Coin flip
- Create: `apps/web/src/components/live-session/GameToolkit.tsx` — Unified toolkit panel

**Key behaviors:**
- Dice: configurable (2d6, 1d6, 1d20, custom), animated result
- Card draw: by deck type (Development, Resource, etc.)
- Random: timer, player selection, color picker
- All results auto-saved to activity feed chronology

- [ ] **Step 1-6: TDD cycle**
- [ ] **Step 7: Commit**

---

### Task 16: Activity Feed — Vertical Timeline

**Files:**
- Create: `apps/web/src/components/live-session/ActivityFeed.tsx` — Main feed container
- Create: `apps/web/src/components/live-session/ActivityFeedItem.tsx` — Single timeline item
- Create: `apps/web/src/components/live-session/ActivityInput.tsx` — Input bar (text + media + AI)
- Create: `apps/web/src/stores/activity-feed-store.ts` — Zustand store for feed items

**Event types:**
- `dice` — Dice roll results
- `score` — Score updates
- `photo` — Photo captures
- `video` — Video captures
- `audio` — Audio notes
- `note` — Text notes
- `ai_tip` — AI suggestions
- `turn` — Turn changes
- `pause` — Pause/resume

**Key behaviors:**
- Vertical timeline with colored type icons
- Input bar at bottom: text input + media buttons + AI button
- Tabs: Cronologia / Media / Note / Statistiche
- Auto-scroll to latest entry

- [ ] **Step 1-8: TDD cycle for store + components**
- [ ] **Step 9: Commit**

---

### Task 17: AI Chat Side Panel

**Files:**
- Create: `apps/web/src/components/live-session/AIChatPanel.tsx` — Right panel with AI chat
- Create: `apps/web/src/components/live-session/QuickPrompts.tsx` — Preset prompt buttons

**Key behaviors:**
- Chat contextual to game in progress (uses existing KB/RAG for game rules)
- Quick prompts: "Chi vince?", "Strategia per X", "Regola Y"
- Tab switch: AI / Regole / FAQ / Stats
- Auto-tips for beginners (toggleable)

- [ ] **Step 1-5: TDD cycle**
- [ ] **Step 6: Commit**

---

### Task 18: Media Capture + Pause/Save

**Files:**
- Create: `apps/web/src/components/live-session/MediaCapture.tsx` — Photo/video/audio capture buttons
- Create: `apps/web/src/components/live-session/PauseOverlay.tsx` — Centered overlay with full state
- Modify: `apps/web/src/lib/stores/sessionStore.ts` — Add pause/resume/save state

**Key behaviors:**
- Photo: Camera API / file picker
- Video: Short video capture
- Audio: Voice note recording
- Pause overlay: centered, shows full state (scores, turn, time), actions (resume, photo, note)
- Timer: separate pause timer
- Resumable after browser close

- [ ] **Step 1-6: TDD cycle**
- [ ] **Step 7: Commit**

---

## Chunk 4: Sprint 4 — Collapsible Panels + Quick View (P2)

### Task 19: Collapsible Panel Component

**Files:**
- Create: `apps/web/src/components/layout/CollapsiblePanel/CollapsiblePanel.tsx` — Generic collapsible panel (left or right)
- Create: `apps/web/src/components/layout/CollapsiblePanel/index.ts`
- Create: `apps/web/src/components/layout/CollapsiblePanel/__tests__/CollapsiblePanel.test.tsx`
- Create: `apps/web/src/hooks/useCollapsiblePanel.ts` — Panel state with localStorage persistence

**Key behaviors:**
- Expanded: 280px width with full content
- Collapsed: 44px width with icon strip
- Left panel collapsed: avatar + mini scores + quick dice button
- Right panel collapsed: AI/Rules/FAQ icons
- Independent — left and right collapse independently
- Animated transition (200ms ease-in-out)
- `prefers-reduced-motion` respected

- [ ] **Step 1-6: TDD cycle**
- [ ] **Step 7: Commit**

---

### Task 20: Quick View Side Panel

**Files:**
- Create: `apps/web/src/components/layout/QuickView/QuickView.tsx` — 300px side panel
- Create: `apps/web/src/components/layout/QuickView/QuickViewTab.tsx` — Tab component
- Create: `apps/web/src/components/layout/QuickView/index.ts`
- Create: `apps/web/src/components/layout/QuickView/__tests__/QuickView.test.tsx`

**Tabs:**
- Regole: Game rules (from KB/RAG)
- FAQ: Frequently asked questions
- AI Chat: Contextual AI assistant
- Stats: Game statistics

**Key behaviors:**
- 300px expanded, 44px collapsed (vertical icon strip)
- Toggle: click ◀/▶ button
- Content contextual to selected game/session
- Collapsed icons clickable to expand on specific tab

- [ ] **Step 1-6: TDD cycle**
- [ ] **Step 7: Wire into game-nights/[id] and sessions/[id] layouts**
- [ ] **Step 8: Commit**

---

### Task 21: Keyboard Shortcuts + State Persistence

**Files:**
- Create: `apps/web/src/hooks/useLayoutShortcuts.ts` — Keyboard shortcut handler
- Modify: `apps/web/src/hooks/useCollapsiblePanel.ts` — Add localStorage persistence

**Shortcuts:**
- `⌘K` / `Ctrl+K` — Command palette (already exists)
- `⌘[` / `Ctrl+[` — Toggle left panel
- `⌘]` / `Ctrl+]` — Toggle right panel
- `⌘\` / `Ctrl+\` — Toggle Quick View

**Persistence:**
- Panel collapsed states saved to localStorage
- Active tab persisted per panel
- Scroll position restored on navigation return
- Filter states preserved

- [ ] **Step 1-6: TDD cycle**
- [ ] **Step 7: Commit**

---

## Chunk 5: Sprint 5 — Mobile Live Session (P2)

### Task 22: Mobile Live Session Layout

**Files:**
- Create: `apps/web/src/components/live-session/MobileLiveSession.tsx` — Mobile wrapper
- Create: `apps/web/src/components/live-session/MobileStatusBar.tsx` — 36px status bar
- Create: `apps/web/src/components/live-session/MobileScorebar.tsx` — 52px horizontal scrollable scorebar
- Create: `apps/web/src/components/live-session/MobileActionBar.tsx` — Bottom input + action buttons

**Layout:**
```
┌─────────────────────────────┐
│ Status Bar (36px)           │
│ LIVE · Catan · T.8 · 1:23  │
├─────────────────────────────┤
│ Scorebar (52px, scroll-x)   │
│ [M:7] [S:6] [L:5] [A:3]   │
├─────────────────────────────┤
│ Feed (flex, scroll-y)       │
├─────────────────────────────┤
│ Input + Actions             │
│ [Tira] [camera] [video]... │
└─────────────────────────────┘
```

- [ ] **Step 1-8: TDD cycle for each sub-component**
- [ ] **Step 9: Responsive switch: desktop → LiveSessionLayout, mobile → MobileLiveSession**
- [ ] **Step 10: Commit**

---

### Task 23: Mobile Bottom Sheets

**Files:**
- Create: `apps/web/src/components/live-session/DiceBottomSheet.tsx` — Dice sheet with large buttons (72px)
- Create: `apps/web/src/components/live-session/AIChatBottomSheet.tsx` — 70% height AI chat sheet
- Create: `apps/web/src/components/live-session/ScoreBottomSheet.tsx` — Player score breakdown sheet
- Create: `apps/web/src/components/live-session/PauseBottomSheet.tsx` — Fullscreen pause overlay

**Uses:** `@/components/ui/navigation/sheet` (existing Sheet component from shadcn)

**Key behaviors:**
- Dice sheet: large dice buttons (72px), type selector, recent rolls history
- AI sheet: 70% viewport height, chat + tabs (Regole/FAQ)
- Score sheet: triggered by tapping player in scorebar, breakdown detail
- Pause: fullscreen overlay with resume, photo, note actions

- [ ] **Step 1-8: TDD cycle**
- [ ] **Step 9: Wire to MobileActionBar triggers**
- [ ] **Step 10: Commit**

---

### Task 24: Mobile Safe Area + Reduced Motion

**Files:**
- Modify: `apps/web/src/components/live-session/MobileLiveSession.tsx`
- Modify: `apps/web/src/components/live-session/MobileActionBar.tsx`

**Key behaviors:**
- iOS safe area: `pb-[env(safe-area-inset-bottom)]` on action bar
- `prefers-reduced-motion`: disable animations, use instant transitions
- Touch feedback: `active:scale-95` on interactive elements
- Prevent zoom on double-tap (viewport meta)

- [ ] **Step 1-4: Add safe area padding + reduced motion classes**
- [ ] **Step 5: Test with useResponsive and usePrefersReducedMotion hooks**
- [ ] **Step 6: Commit**

---

## GitHub Epic Creation

After plan approval, create the GitHub epic and issues:

- [ ] **Step 1: Create epic issue on GitHub**

```bash
gh issue create \
  --title "Epic: Desktop UX Redesign — The Game Table" \
  --label "epic,frontend,ux" \
  --body-file <(cat <<'BODY'
## Overview
Redesign desktop UX centrata sulla metafora del tavolo da gioco.

**Spec**: docs/superpowers/specs/2026-03-11-desktop-ux-game-table-design.md
**Plan**: docs/superpowers/plans/2026-03-11-desktop-ux-game-table.md

## Sprints
- S1: Desktop Navigation Foundation (P0)
- S2: Game Night Experience (P1)
- S3: Live Session (P1)
- S4: Collapsible Panels + Quick View (P2)
- S5: Mobile Live Session (P2)
BODY
)
```

- [ ] **Step 2: Create individual issues for each task (link to epic)**

Create 18 issues matching the Epic Structure table above, each referencing the epic and linking to the relevant plan chunk.

---

## Dependencies & Order

```
Sprint 1 (Tasks 1-8) ─── Foundation, no dependencies
    │
    ├── Sprint 2 (Tasks 9-12) ─── Depends on CardRack + TopBar
    │
    ├── Sprint 3 (Tasks 13-18) ─── Depends on CardRack + existing session components
    │       │
    │       └── Sprint 5 (Tasks 22-24) ─── Mobile version of Sprint 3
    │
    └── Sprint 4 (Tasks 19-21) ─── Depends on CardRack, enhances Sprint 3
```

Sprint 1 must complete first. Sprints 2, 3, 4 can be worked in parallel after Sprint 1. Sprint 5 depends on Sprint 3.
