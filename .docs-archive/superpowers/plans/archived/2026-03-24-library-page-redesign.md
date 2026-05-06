# Library Page Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the `/library` page with Gaming Immersive layout: PageHeader, HeroBanner, expanded filters, immersive empty state, mobile list variant, compact quota widget.

**Architecture:** In-place updates to existing components (no v2). New components for PageHeader, HeroBanner, EmptyState. PersonalLibraryPage becomes the primary layout target. UsageWidget compacted and repositioned as sidebar.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind 4, shadcn/ui, Zustand, React Query (TanStack), Vitest, Framer Motion

**Spec:** `docs/superpowers/specs/2026-03-24-library-page-redesign-design.md`

---

## File Map

### New Files
| File | Responsibility |
|------|---------------|
| `src/components/library/LibraryPageHeader.tsx` | Title + count badge + "Aggiungi Gioco" CTA |
| `src/components/library/LibraryHeroBanner.tsx` | Contextual hero: next session or discovery |
| `src/components/library/TrendingGamesRow.tsx` | Horizontal trending cards for empty state |
| `src/hooks/queries/useNextSession.ts` | Fetch next upcoming session for hero |
| `src/__tests__/components/library/LibraryPageHeader.test.tsx` | Unit tests |
| `src/__tests__/components/library/LibraryHeroBanner.test.tsx` | Unit tests |
| `src/__tests__/components/library/LibraryEmptyState.test.tsx` | Unit tests for immersive version |

### Modified Files
| File | Change |
|------|--------|
| `src/components/library/PersonalLibraryPage.tsx` | Add PageHeader, Hero, new flex layout with sidebar quota |
| `src/components/library/LibraryEmptyState.tsx` | Replace with gaming immersive version (animated meeples, quick-start cards, trending) |
| `src/components/library/UsageWidget.tsx` | Add compact variant (top 3 quotas only) |
| `src/app/(authenticated)/library/_content.tsx` | Move UsageWidget into PersonalLibraryPage flex container |
| `src/components/library/LibraryFilters.tsx` | Not changed — PersonalLibraryPage uses its own FilterChipsRow which we'll expand |

### Removed
| File | Reason |
|------|--------|
| `LibraryQuickStats.tsx` usage | Replaced by PageHeader count badge (file kept if used elsewhere) |

---

## Task 1: LibraryPageHeader Component

**Files:**
- Create: `apps/web/src/components/library/LibraryPageHeader.tsx`
- Create: `apps/web/src/__tests__/components/library/LibraryPageHeader.test.tsx`

- [ ] **Step 1: Write test file**

```typescript
// apps/web/src/__tests__/components/library/LibraryPageHeader.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LibraryPageHeader } from '@/components/library/LibraryPageHeader';

describe('LibraryPageHeader', () => {
  it('renders title and count badge', () => {
    render(<LibraryPageHeader gameCount={24} onAddGame={vi.fn()} />);
    expect(screen.getByRole('heading', { name: /i miei giochi/i })).toBeInTheDocument();
    expect(screen.getByText('24 giochi')).toBeInTheDocument();
  });

  it('renders add game CTA button', () => {
    const onAddGame = vi.fn();
    render(<LibraryPageHeader gameCount={24} onAddGame={onAddGame} />);
    const btn = screen.getByRole('button', { name: /aggiungi/i });
    fireEvent.click(btn);
    expect(onAddGame).toHaveBeenCalledOnce();
  });

  it('shows 0 giochi when empty', () => {
    render(<LibraryPageHeader gameCount={0} onAddGame={vi.fn()} />);
    expect(screen.getByText('0 giochi')).toBeInTheDocument();
  });

  it('renders FAB on mobile viewport', () => {
    render(<LibraryPageHeader gameCount={5} onAddGame={vi.fn()} />);
    // Mobile FAB has aria-label
    expect(screen.getByLabelText('Aggiungi un gioco alla libreria')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/library/LibraryPageHeader.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement LibraryPageHeader**

```typescript
// apps/web/src/components/library/LibraryPageHeader.tsx
'use client';

import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface LibraryPageHeaderProps {
  gameCount: number;
  onAddGame: () => void;
  className?: string;
}

export function LibraryPageHeader({ gameCount, onAddGame, className }: LibraryPageHeaderProps) {
  return (
    <div className={cn('flex items-center justify-between', className)}>
      {/* Left: title + count */}
      <div className="flex items-baseline gap-3">
        <h1 className="font-quicksand text-2xl font-bold text-foreground">
          I Miei Giochi
        </h1>
        <span className="rounded-full bg-muted px-2.5 py-0.5 text-sm font-medium text-muted-foreground">
          {gameCount} giochi
        </span>
      </div>

      {/* Desktop: full CTA button */}
      <Button
        onClick={onAddGame}
        className="hidden sm:inline-flex gap-1.5"
        aria-label="Aggiungi un gioco alla libreria"
      >
        <Plus className="h-4 w-4" />
        Aggiungi Gioco
      </Button>

      {/* Mobile: FAB circular */}
      <Button
        onClick={onAddGame}
        size="icon"
        className="sm:hidden h-9 w-9 rounded-full shadow-warm-md"
        aria-label="Aggiungi un gioco alla libreria"
      >
        <Plus className="h-5 w-5" />
      </Button>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/library/LibraryPageHeader.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/LibraryPageHeader.tsx apps/web/src/__tests__/components/library/LibraryPageHeader.test.tsx
git commit -m "feat(library): add LibraryPageHeader component with title, count, CTA"
```

---

## Task 2: useNextSession Hook + LibraryHeroBanner

**Files:**
- Create: `apps/web/src/hooks/queries/useNextSession.ts`
- Create: `apps/web/src/components/library/LibraryHeroBanner.tsx`
- Create: `apps/web/src/__tests__/components/library/LibraryHeroBanner.test.tsx`

- [ ] **Step 1: Create useNextSession hook**

Check existing sessions API client first: `apps/web/src/lib/api/clients/sessionsClient.ts`. If no sessions endpoint exists, the hook should gracefully return null.

```typescript
// apps/web/src/hooks/queries/useNextSession.ts
'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export interface NextSessionData {
  id: string;
  name: string;
  scheduledAt: string;
  playerCount: number;
  games: string[];
}

export function useNextSession() {
  return useQuery<NextSessionData | null>({
    queryKey: ['sessions', 'next'],
    queryFn: async () => {
      try {
        // Use existing sessions API if available
        const sessions = await api.sessions?.getUpcoming?.({ limit: 1 });
        if (!sessions?.items?.length) return null;
        const s = sessions.items[0];
        return {
          id: s.id,
          name: s.name ?? 'Prossima sessione',
          scheduledAt: s.scheduledAt,
          playerCount: s.playerCount ?? 0,
          games: s.games?.map((g: { title: string }) => g.title) ?? [],
        };
      } catch {
        return null; // Sessions API not available — fallback to discovery
      }
    },
    staleTime: 5 * 60 * 1000, // 5 min
    retry: false,
  });
}
```

- [ ] **Step 2: Write HeroBanner tests**

```typescript
// apps/web/src/__tests__/components/library/LibraryHeroBanner.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LibraryHeroBanner } from '@/components/library/LibraryHeroBanner';

// Mock the hook
vi.mock('@/hooks/queries/useNextSession', () => ({
  useNextSession: vi.fn(),
}));

import { useNextSession } from '@/hooks/queries/useNextSession';
const mockUseNextSession = vi.mocked(useNextSession);

describe('LibraryHeroBanner', () => {
  it('renders session variant when upcoming session exists', () => {
    mockUseNextSession.mockReturnValue({
      data: { id: '1', name: 'Serata Strategici', scheduledAt: '2026-03-28T20:00:00Z', playerCount: 3, games: ['Terraforming Mars'] },
      isLoading: false,
    } as any);

    render(<LibraryHeroBanner />);
    expect(screen.getByText(/serata strategici/i)).toBeInTheDocument();
    expect(screen.getByText(/3 giocatori/i)).toBeInTheDocument();
  });

  it('renders discovery variant when no session', () => {
    mockUseNextSession.mockReturnValue({ data: null, isLoading: false } as any);

    render(<LibraryHeroBanner />);
    expect(screen.getByText(/scopri nuovi giochi/i)).toBeInTheDocument();
  });

  it('renders nothing when hidden (empty library, handled externally)', () => {
    mockUseNextSession.mockReturnValue({ data: null, isLoading: false } as any);

    const { container } = render(<LibraryHeroBanner hide />);
    expect(container.querySelector('[role="banner"]')).toBeNull();
  });
});
```

- [ ] **Step 3: Implement LibraryHeroBanner**

```typescript
// apps/web/src/components/library/LibraryHeroBanner.tsx
'use client';

import Link from 'next/link';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useNextSession } from '@/hooks/queries/useNextSession';
import { cn } from '@/lib/utils';

interface LibraryHeroBannerProps {
  hide?: boolean;
  className?: string;
}

export function LibraryHeroBanner({ hide, className }: LibraryHeroBannerProps) {
  const { data: session, isLoading } = useNextSession();

  if (hide) return null;
  if (isLoading) {
    return (
      <div className={cn('h-[72px] animate-pulse rounded-[14px] bg-muted', className)} />
    );
  }

  // Session variant
  if (session) {
    const gamesText = session.games.length > 0 ? session.games.join(', ') : '';
    return (
      <div
        role="banner"
        aria-label="Prossima sessione"
        className={cn(
          'flex items-center justify-between rounded-[14px] border border-primary/20 px-5 py-4',
          'bg-gradient-to-r from-primary/15 via-accent/8 to-blue-500/8',
          // Mobile compact
          'flex-col gap-2 sm:flex-row sm:gap-4',
          className
        )}
      >
        <div className="flex flex-col gap-1">
          <span className="inline-flex w-fit items-center gap-1.5 rounded-full bg-primary/20 px-2.5 py-0.5 font-quicksand text-[10px] font-bold uppercase tracking-wider text-primary">
            <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-primary" />
            Prossima sessione
          </span>
          <span className="font-quicksand text-base font-bold sm:text-lg">{session.name}</span>
          <span className="text-xs text-muted-foreground sm:text-sm">
            {session.playerCount} giocatori{gamesText ? ` · ${gamesText}` : ''}
          </span>
        </div>
        <Button asChild size="sm" className="shrink-0">
          <Link href={`/sessions/${session.id}`}>
            Vedi Dettagli <ArrowRight className="ml-1 h-3.5 w-3.5" />
          </Link>
        </Button>
      </div>
    );
  }

  // Discovery variant
  return (
    <div
      role="banner"
      aria-label="Esplora il catalogo"
      className={cn(
        'flex items-center justify-between rounded-[14px] border border-primary/20 px-5 py-4',
        'bg-gradient-to-r from-primary/15 via-accent/8 to-blue-500/8',
        'flex-col gap-2 sm:flex-row sm:gap-4',
        className
      )}
    >
      <div className="flex flex-col gap-1">
        <span className="font-quicksand text-base font-bold sm:text-lg">Scopri nuovi giochi</span>
        <span className="text-xs text-muted-foreground sm:text-sm">
          Esplora il catalogo e arricchisci la tua collezione
        </span>
      </div>
      <Button asChild size="sm" variant="default" className="shrink-0">
        <Link href="/games">
          Esplora Catalogo <ArrowRight className="ml-1 h-3.5 w-3.5" />
        </Link>
      </Button>
    </div>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/library/LibraryHeroBanner.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/queries/useNextSession.ts apps/web/src/components/library/LibraryHeroBanner.tsx apps/web/src/__tests__/components/library/LibraryHeroBanner.test.tsx
git commit -m "feat(library): add LibraryHeroBanner with session/discovery variants"
```

---

## Task 3: Gaming Immersive Empty State

**Files:**
- Modify: `apps/web/src/components/library/LibraryEmptyState.tsx` (in-place update)
- Create: `apps/web/src/components/library/TrendingGamesRow.tsx`
- Create: `apps/web/src/__tests__/components/library/LibraryEmptyState.test.tsx`

- [ ] **Step 1: Write tests for immersive empty state**

```typescript
// apps/web/src/__tests__/components/library/LibraryEmptyState.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LibraryEmptyState } from '@/components/library/LibraryEmptyState';

// Mock next/link
vi.mock('next/link', () => ({ default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a> }));

describe('LibraryEmptyState (immersive)', () => {
  const defaultProps = {
    onExploreCatalog: vi.fn(),
    onImportBgg: vi.fn(),
    onCreateCustom: vi.fn(),
  };

  it('renders animated illustration (aria-hidden)', () => {
    const { container } = render(<LibraryEmptyState {...defaultProps} />);
    const illustration = container.querySelector('[aria-hidden="true"]');
    expect(illustration).toBeInTheDocument();
  });

  it('renders gradient title', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    expect(screen.getByText(/la tua collezione ti aspetta/i)).toBeInTheDocument();
  });

  it('renders 3 quick-start action cards', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    expect(screen.getByText(/esplora il catalogo/i)).toBeInTheDocument();
    expect(screen.getByText(/importa da bgg/i)).toBeInTheDocument();
    expect(screen.getByText(/crea gioco custom/i)).toBeInTheDocument();
  });

  it('calls onExploreCatalog when clicking explore card', () => {
    render(<LibraryEmptyState {...defaultProps} />);
    fireEvent.click(screen.getByText(/esplora il catalogo/i));
    expect(defaultProps.onExploreCatalog).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Create TrendingGamesRow component**

```typescript
// apps/web/src/components/library/TrendingGamesRow.tsx
'use client';

import Link from 'next/link';
import { cn } from '@/lib/utils';

interface TrendingGame {
  id: string;
  title: string;
  imageUrl?: string;
  rating: number;
  playerRange: string;
}

interface TrendingGamesRowProps {
  games?: TrendingGame[];
  className?: string;
}

// Fallback static data when API not available
const FALLBACK_TRENDING: TrendingGame[] = [
  { id: '1', title: 'Terraforming Mars', rating: 4.8, playerRange: '1-5' },
  { id: '2', title: 'Scythe', rating: 4.5, playerRange: '1-5' },
  { id: '3', title: 'Wingspan', rating: 4.3, playerRange: '1-5' },
  { id: '4', title: 'Everdell', rating: 4.2, playerRange: '1-4' },
  { id: '5', title: 'Spirit Island', rating: 4.9, playerRange: '1-4' },
];

export function TrendingGamesRow({ games, className }: TrendingGamesRowProps) {
  const items = games ?? FALLBACK_TRENDING;

  return (
    <div className={cn('w-full', className)}>
      <div className="mb-3 flex items-center justify-between">
        <h3 className="flex items-center gap-2 font-quicksand text-base font-bold">
          <span>🔥</span> Popolari questa settimana
        </h3>
        <Link href="/games" className="text-sm font-semibold text-primary hover:underline">
          Vedi tutti →
        </Link>
      </div>
      <div className="flex gap-3 overflow-x-auto pb-2 scrollbar-none" role="list">
        {items.map((game, i) => (
          <Link
            key={game.id}
            href={`/games/${game.id}`}
            role="listitem"
            className="flex w-[110px] flex-shrink-0 flex-col overflow-hidden rounded-xl border-l-[3px] border-l-entity-game bg-card shadow-warm-sm transition-all duration-300 hover:-translate-y-1 hover:shadow-warm-md"
          >
            <div className="relative flex aspect-square items-center justify-center bg-gradient-to-br from-primary/20 to-primary/5 text-2xl">
              <span className="absolute left-1 top-1 flex h-5 w-5 items-center justify-center rounded-md bg-primary font-quicksand text-[10px] font-extrabold text-white">
                {i + 1}
              </span>
              {game.imageUrl ? (
                <img src={game.imageUrl} alt={game.title} className="h-full w-full object-cover" />
              ) : (
                <span className="text-2xl opacity-50">🎲</span>
              )}
            </div>
            <div className="p-2">
              <p className="truncate font-quicksand text-xs font-bold">{game.title}</p>
              <p className="text-[10px] text-muted-foreground">★ {game.rating} · {game.playerRange}</p>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Rewrite LibraryEmptyState (in-place)**

Read current `apps/web/src/components/library/LibraryEmptyState.tsx` first to understand existing props/imports, then replace the JSX body with the gaming immersive version:

- Animated floating meeple icons (`animate-float` with staggered delays)
- Central glow ring with pulsing shadow
- Gradient title "La tua collezione ti aspetta"
- 3 Quick-start cards (Esplora Catalogo, Importa BGG, Crea Custom)
- TrendingGamesRow at bottom
- `prefers-reduced-motion` support
- Mobile: quick-start cards single column

The component should accept the same `onExploreCatalog`, `onImportBgg`, `onCreateCustom` callbacks. Add a Tailwind animation in `globals.css`:

```css
@keyframes float {
  0%, 100% { transform: translateY(0) rotate(0deg); }
  25% { transform: translateY(-12px) rotate(-3deg); }
  50% { transform: translateY(-6px) rotate(2deg); }
  75% { transform: translateY(-14px) rotate(-2deg); }
}
.animate-float { animation: float 4s ease-in-out infinite; }

@keyframes glow-pulse {
  0%, 100% { box-shadow: 0 0 20px hsla(25, 95%, 60%, 0.1); }
  50% { box-shadow: 0 0 40px hsla(25, 95%, 60%, 0.25); }
}
.animate-glow-pulse { animation: glow-pulse 3s ease-in-out infinite; }

@media (prefers-reduced-motion: reduce) {
  .animate-float { animation: none; }
  .animate-glow-pulse { animation: none; box-shadow: 0 0 20px hsla(25, 95%, 60%, 0.15); }
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/library/LibraryEmptyState.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/LibraryEmptyState.tsx apps/web/src/components/library/TrendingGamesRow.tsx apps/web/src/__tests__/components/library/LibraryEmptyState.test.tsx apps/web/src/styles/globals.css
git commit -m "feat(library): gaming immersive empty state with animated meeples and quick-start cards"
```

---

## Task 4: Expand Filter Chips in PersonalLibraryPage

**Files:**
- Modify: `apps/web/src/components/library/PersonalLibraryPage.tsx`

PersonalLibraryPage currently uses a local `FilterChipsRow` or inline filter chips with 4 state chips (Tutti, Posseduti, Wishlist, Prestati). We need to expand to 7 chips: Tutti, Recenti, Più giocati, Rating ↓, 2-4 giocatori, <60 min, Strategici.

- [ ] **Step 1: Read current PersonalLibraryPage filter logic**

Read `apps/web/src/components/library/PersonalLibraryPage.tsx` and identify:
- How `activeFilter` state is used
- How `applyFilter()` transforms the items array
- Where filter chips are rendered

- [ ] **Step 2: Update filter chips array and logic**

Replace the current 4 state-based chips with the expanded set. Update the `applyFilter` function:

```typescript
const FILTER_CHIPS = [
  { id: 'all', label: 'Tutti' },
  { id: 'recent', label: 'Recenti' },
  { id: 'most-played', label: 'Più giocati' },
  { id: 'rating', label: 'Rating ↓' },
  { id: 'players-2-4', label: '2-4 giocatori' },
  { id: 'short', label: '<60 min' },
  { id: 'strategy', label: 'Strategici' },
] as const;

function applyFilter(items: UserLibraryEntry[], filterId: string): UserLibraryEntry[] {
  switch (filterId) {
    case 'recent':
      return [...items].sort((a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime());
    case 'most-played':
      return [...items].sort((a, b) => (b.playCount ?? 0) - (a.playCount ?? 0));
    case 'rating':
      return [...items].sort((a, b) => (b.averageRating ?? 0) - (a.averageRating ?? 0));
    case 'players-2-4':
      return items.filter(g => (g.minPlayers ?? 99) <= 4 && (g.maxPlayers ?? 0) >= 2);
    case 'short':
      return items.filter(g => (g.playingTimeMinutes ?? 999) <= 60);
    case 'strategy':
      return items.filter(g => g.category?.toLowerCase() === 'strategy' || g.mechanics?.some(m => m.toLowerCase().includes('strateg')));
    default:
      return items;
  }
}
```

- [ ] **Step 3: Update filter chips rendering**

Add `role="toolbar"` and `aria-pressed` per spec accessibility requirements. Make chips horizontally scrollable on mobile:

```tsx
<div
  role="toolbar"
  aria-label="Filtra giochi"
  className="flex gap-2 overflow-x-auto pb-2 scrollbar-none sm:flex-wrap sm:overflow-visible sm:pb-0"
>
  {FILTER_CHIPS.map((chip) => (
    <button
      key={chip.id}
      role="button"
      aria-pressed={activeFilter === chip.id}
      onClick={() => setActiveFilter(chip.id)}
      className={cn(
        'flex-shrink-0 rounded-full border px-3.5 py-1.5 text-sm font-semibold transition-all',
        activeFilter === chip.id
          ? 'border-primary bg-primary text-primary-foreground'
          : 'border-border text-muted-foreground hover:border-primary hover:text-foreground'
      )}
    >
      {chip.label}
    </button>
  ))}
  <div className="ml-auto flex-shrink-0">
    <ViewModeToggle viewMode={viewMode} onViewModeChange={setViewMode} />
  </div>
</div>
```

- [ ] **Step 4: Run existing tests + typecheck**

Run: `cd apps/web && pnpm vitest run --reporter=verbose 2>&1 | head -50`
Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/PersonalLibraryPage.tsx
git commit -m "feat(library): expand filter chips to 7 (recenti, rating, players, duration, strategy)"
```

---

## Task 5: Compact UsageWidget Variant

**Files:**
- Modify: `apps/web/src/components/library/UsageWidget.tsx`

- [ ] **Step 1: Read current UsageWidget**

Read `apps/web/src/components/library/UsageWidget.tsx` to understand current structure.

- [ ] **Step 2: Add compact variant prop**

```typescript
interface UsageWidgetProps {
  tier?: UserTier;
  variant?: 'full' | 'compact'; // NEW
  className?: string;
}
```

When `variant="compact"`:
- Show only top 3 quotas: Giochi privati, PDF questo mese, Query oggi
- Smaller padding/text
- Keep "Passa a Premium" CTA
- Hide session save indicator and other secondary quotas
- Width: 200px

- [ ] **Step 3: Run typecheck**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/library/UsageWidget.tsx
git commit -m "feat(library): add compact variant to UsageWidget (top 3 quotas only)"
```

---

## Task 6: Wire Everything in PersonalLibraryPage

**Files:**
- Modify: `apps/web/src/components/library/PersonalLibraryPage.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/_content.tsx`

This is the main integration task. PersonalLibraryPage becomes the layout orchestrator.

- [ ] **Step 1: Read current _content.tsx**

Read `apps/web/src/app/(authenticated)/library/_content.tsx` to understand how UsageWidget is currently positioned.

- [ ] **Step 2: Update PersonalLibraryPage layout structure**

New JSX structure:

```tsx
export function PersonalLibraryPage({ className }: PersonalLibraryPageProps) {
  // ... existing hooks (useLibrary, search, filter, viewMode)

  const isEmpty = !isLoading && (!data?.items || data.items.length === 0);

  return (
    <div className={cn('space-y-4', className)}>
      {/* PageHeader — always visible */}
      <LibraryPageHeader
        gameCount={data?.totalCount ?? 0}
        onAddGame={() => /* trigger AddGameDrawer via URL param ?action=add */}
      />

      {/* Hero — hidden when empty (empty state handles onboarding) */}
      <LibraryHeroBanner hide={isEmpty} />

      {/* Main content area with optional sidebar */}
      <div className="flex items-start gap-5">
        {/* Content column */}
        <div className="min-w-0 flex-1 space-y-4">
          {isEmpty ? (
            <LibraryEmptyState
              onExploreCatalog={() => router.push('/games')}
              onImportBgg={() => router.push('/library/private/add')}
              onCreateCustom={() => router.push('/library/private/add')}
            />
          ) : (
            <>
              {/* Filter chips + view toggle */}
              {/* ... expanded filter chips from Task 4 */}

              {/* Card grid/list */}
              {/* ... existing game card rendering */}
            </>
          )}
        </div>

        {/* Sidebar: compact quota (desktop only, not on empty) */}
        {!isEmpty && (
          <aside className="hidden lg:block w-[200px] flex-shrink-0">
            <div className="sticky top-[68px]">
              <UsageWidget variant="compact" />
            </div>
          </aside>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Update _content.tsx**

Remove the UsageWidget from `_content.tsx` (it's now inside PersonalLibraryPage):

```tsx
// Before: _content.tsx renders UsageWidget alongside PersonalLibraryPage
// After: _content.tsx just renders PersonalLibraryPage (which includes UsageWidget internally)
```

- [ ] **Step 4: Fix breadcrumb**

If breadcrumb is rendered in layout.tsx or _content.tsx, update it to show "Libreria › I Miei Giochi" instead of "Home".

- [ ] **Step 5: Verify mobile list default**

Ensure PersonalLibraryPage defaults to `list` on mobile. Check existing logic — it likely already does this via `useMediaQuery` or responsive check. If not:

```typescript
const isMobile = useMediaQuery('(max-width: 1023px)');
const [viewMode, setViewMode] = useState<'grid' | 'list'>(isMobile ? 'list' : 'grid');
```

- [ ] **Step 6: Run typecheck + test suite**

Run: `cd apps/web && pnpm typecheck && pnpm vitest run --reporter=verbose 2>&1 | tail -20`

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/library/PersonalLibraryPage.tsx apps/web/src/app/(authenticated)/library/_content.tsx
git commit -m "feat(library): wire PageHeader, HeroBanner, expanded filters, compact quota layout"
```

---

## Task 7: Visual Verification + Bug Fixes

**Files:** Various — fix any issues found

- [ ] **Step 1: Start dev server and verify desktop**

Run: `cd apps/web && pnpm dev`
Open: `http://localhost:3000/library`

Check:
- PageHeader visible with title + count + CTA
- Hero banner renders (discovery variant if no sessions)
- Filter chips row shows all 7 chips
- View toggle works (grid ↔ list)
- Quota widget compact in sidebar (desktop)
- Card grid 4col on desktop

- [ ] **Step 2: Verify empty state**

If library is empty, check:
- Animated meeple icons float
- Glow ring pulses
- 3 Quick-start cards render
- TrendingGamesRow at bottom
- Cards are clickable

- [ ] **Step 3: Verify mobile**

Resize to mobile viewport (390px) or use Chrome DevTools:
- No sidebar
- List variant by default
- Filter chips scroll horizontally
- FAB button (not full CTA)
- Hero compact (if visible)
- TabBar clearance (pb-80px)

- [ ] **Step 4: Fix any issues found**

Address visual bugs, spacing issues, responsive breakpoints.

- [ ] **Step 5: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Run: `cd apps/web && pnpm typecheck`
Run: `cd apps/web && pnpm lint`

- [ ] **Step 6: Final commit**

```bash
git add -A
git commit -m "fix(library): visual polish and responsive fixes for library redesign"
```

---

## Task 8: Create PR

- [ ] **Step 1: Create feature branch (if not already on one)**

```bash
git checkout -b feature/library-page-redesign
git config branch.feature/library-page-redesign.parent frontend-dev
```

- [ ] **Step 2: Push and create PR**

```bash
git push -u origin feature/library-page-redesign
gh pr create --base frontend-dev --title "feat(library): Gaming Immersive library page redesign" --body "..."
```

PR description should reference the spec and list all changes.
