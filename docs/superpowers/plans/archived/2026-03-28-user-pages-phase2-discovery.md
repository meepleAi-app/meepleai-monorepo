# Phase 2: Discovery & Add Game — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the dashboard and game search flow as a mobile-first experience: search bar → BGG results → bottom sheet preview → one-tap add to library.

**Architecture:** Replace `dashboard-client.tsx` with a new mobile-first layout using Phase 1 components (GlassCard, GradientButton, MobileHeader). Create `FullScreenSearch` overlay and `GamePreviewSheet` bottom sheet. Reuse ALL existing API hooks (`useSearchBggGames`, `useAddGameToLibrary`, `useLibraryQuota`, `useLibrary`).

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, TanStack Query (existing hooks), Zustand (existing stores), Phase 1 components

**Spec:** `docs/superpowers/specs/2026-03-28-user-pages-redesign-design.md` — Section 2

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `src/app/(authenticated)/dashboard/dashboard-mobile.tsx` | New mobile-first dashboard replacing dashboard-client |
| `src/components/search/FullScreenSearch.tsx` | Full-screen BGG search overlay with debounced input |
| `src/components/search/FullScreenSearch.test.tsx` | Tests |
| `src/components/search/GamePreviewSheet.tsx` | Bottom sheet with game details and add-to-library action |
| `src/components/search/GamePreviewSheet.test.tsx` | Tests |
| `src/components/dashboard/RecentGamesRow.tsx` | Horizontal scrollable MeepleCard row for recent library games |
| `src/components/dashboard/QuickActionCards.tsx` | 2-3 GlassCard quick actions (Nuova Serata, Chat AI, Esplora) |

### Modified Files

| File | Changes |
|------|---------|
| `src/app/(authenticated)/dashboard/page.tsx` | Switch from DashboardClient to DashboardMobile |
| `src/app/(authenticated)/dashboard/dashboard-client.tsx` | Keep as-is for desktop fallback (not deleted yet) |

### All paths relative to `apps/web/`

---

## Task 1: Create RecentGamesRow Component

**Files:**
- Create: `src/components/dashboard/RecentGamesRow.tsx`

This is a simple horizontal scrollable row of MeepleCard compact variants showing the user's recent library games.

- [ ] **Step 1: Create RecentGamesRow**

```tsx
'use client';

import React from 'react';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useLibrary } from '@/hooks/queries/useLibrary';

export function RecentGamesRow() {
  const { data, isLoading } = useLibrary({
    page: 1,
    pageSize: 8,
    sortBy: 'addedAt',
    sortDirection: 'desc',
  });

  if (isLoading) {
    return (
      <div className="flex gap-3 overflow-x-auto px-4 py-2 scrollbar-none">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-32 w-24 shrink-0 animate-pulse rounded-lg bg-white/5" />
        ))}
      </div>
    );
  }

  const games = data?.items ?? [];
  if (games.length === 0) return null;

  return (
    <section>
      <h2 className="mb-2 px-4 text-sm font-medium text-[var(--gaming-text-secondary)]">
        Giochi recenti
      </h2>
      <div className="flex gap-3 overflow-x-auto px-4 pb-2 scrollbar-none">
        {games.map((entry) => (
          <div key={entry.id} className="w-28 shrink-0">
            <MeepleCard
              entity="game"
              variant="compact"
              title={entry.title ?? entry.gameTitle ?? ''}
              imageUrl={entry.imageUrl ?? entry.coverImageUrl}
              detailHref={`/library/games/${entry.gameId}`}
            />
          </div>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 2: Verify the actual useLibrary hook params and response shape**

Read `src/hooks/queries/useLibrary.ts` to confirm the query params interface and response shape. Adjust the component if the actual field names differ (e.g., `entry.title` vs `entry.gameTitle`, `entry.imageUrl` vs `entry.coverImageUrl`). Also check MeepleCard compact variant props.

- [ ] **Step 3: Verify compilation**

Run: `cd apps/web && npx tsc --noEmit`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/dashboard/RecentGamesRow.tsx
git commit -m "feat(dashboard): add RecentGamesRow with horizontal scroll"
```

---

## Task 2: Create QuickActionCards Component

**Files:**
- Create: `src/components/dashboard/QuickActionCards.tsx`

Three GlassCard quick actions linking to main flows.

- [ ] **Step 1: Create QuickActionCards**

```tsx
'use client';

import React from 'react';
import Link from 'next/link';
import { GlassCard } from '@/components/ui/surfaces/GlassCard';
import { Dice5, MessageCircle, Search } from 'lucide-react';

interface QuickAction {
  href: string;
  label: string;
  description: string;
  icon: React.ElementType;
  iconColor: string;
}

const actions: QuickAction[] = [
  {
    href: '/sessions/new',
    label: 'Nuova Serata',
    description: 'Organizza una partita',
    icon: Dice5,
    iconColor: 'text-amber-400',
  },
  {
    href: '/chat',
    label: 'Chat AI',
    description: 'Chiedi alle regole',
    icon: MessageCircle,
    iconColor: 'text-purple-400',
  },
];

export interface QuickActionCardsProps {
  onSearchClick?: () => void;
}

export function QuickActionCards({ onSearchClick }: QuickActionCardsProps) {
  return (
    <section className="grid grid-cols-2 gap-3 px-4">
      {actions.map((action) => {
        const Icon = action.icon;
        return (
          <Link key={action.href} href={action.href}>
            <GlassCard className="flex flex-col gap-2 p-4">
              <Icon className={`h-6 w-6 ${action.iconColor}`} />
              <div>
                <p className="text-sm font-semibold text-[var(--gaming-text-primary)]">
                  {action.label}
                </p>
                <p className="text-xs text-[var(--gaming-text-secondary)]">
                  {action.description}
                </p>
              </div>
            </GlassCard>
          </Link>
        );
      })}
      <button type="button" onClick={onSearchClick}>
        <GlassCard className="flex flex-col gap-2 p-4 text-left">
          <Search className="h-6 w-6 text-blue-400" />
          <div>
            <p className="text-sm font-semibold text-[var(--gaming-text-primary)]">
              Esplora
            </p>
            <p className="text-xs text-[var(--gaming-text-secondary)]">
              Cerca nel catalogo BGG
            </p>
          </div>
        </GlassCard>
      </button>
    </section>
  );
}
```

- [ ] **Step 2: Verify compilation**

Run: `cd apps/web && npx tsc --noEmit`

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/dashboard/QuickActionCards.tsx
git commit -m "feat(dashboard): add QuickActionCards with glass styling"
```

---

## Task 3: Create FullScreenSearch Component

**Files:**
- Create: `src/components/search/FullScreenSearch.tsx`
- Create: `src/components/search/FullScreenSearch.test.tsx`

Full-screen overlay with auto-focused search input, BGG results as MeepleCard list items. Reuses `useSearchBggGames` hook.

- [ ] **Step 1: Read existing hooks to confirm API**

Read these files to understand the exact interfaces:
- `src/hooks/queries/useSearchBggGames.ts` — confirm params and return type
- `src/lib/api/clients/bggClient.ts` — confirm BggGameSummary type

- [ ] **Step 2: Write the failing test**

Create `src/components/search/FullScreenSearch.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { FullScreenSearch } from './FullScreenSearch';

// Mock the BGG search hook
vi.mock('@/hooks/queries/useSearchBggGames', () => ({
  useSearchBggGames: vi.fn(() => ({
    data: undefined,
    isLoading: false,
    error: null,
  })),
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe('FullScreenSearch', () => {
  it('renders search input when open', () => {
    render(<FullScreenSearch open onClose={() => {}} onSelectGame={() => {}} />);
    expect(screen.getByPlaceholderText(/cerca un gioco/i)).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    const { container } = render(
      <FullScreenSearch open={false} onClose={() => {}} onSelectGame={() => {}} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('calls onClose when back button is clicked', () => {
    const onClose = vi.fn();
    render(<FullScreenSearch open onClose={onClose} onSelectGame={() => {}} />);
    fireEvent.click(screen.getByLabelText(/chiudi/i));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('auto-focuses the search input', () => {
    render(<FullScreenSearch open onClose={() => {}} onSelectGame={() => {}} />);
    const input = screen.getByPlaceholderText(/cerca un gioco/i);
    expect(document.activeElement).toBe(input);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/search/FullScreenSearch.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 4: Write FullScreenSearch implementation**

Create `src/components/search/FullScreenSearch.tsx`:

```tsx
'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { cn } from '@/lib/utils';
import { X, Search, Loader2 } from 'lucide-react';
import { useSearchBggGames } from '@/hooks/queries/useSearchBggGames';
import { AnimatePresence, motion } from 'framer-motion';

export interface BggGameResult {
  bggId: number;
  title: string;
  yearPublished?: number;
  thumbnailUrl?: string | null;
}

export interface FullScreenSearchProps {
  open: boolean;
  onClose: () => void;
  onSelectGame: (game: BggGameResult) => void;
}

export function FullScreenSearch({ open, onClose, onSelectGame }: FullScreenSearchProps) {
  const [query, setQuery] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  const { data, isLoading } = useSearchBggGames(query);

  // Auto-focus on open
  useEffect(() => {
    if (open) {
      // Small delay for animation
      const timer = setTimeout(() => inputRef.current?.focus(), 100);
      return () => clearTimeout(timer);
    }
    setQuery('');
  }, [open]);

  // Close on Escape
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    },
    [onClose]
  );

  if (!open) return null;

  const results: BggGameResult[] = data?.items ?? data?.results ?? [];

  return (
    <AnimatePresence>
      <motion.div
        className="fixed inset-0 z-50 flex flex-col bg-[var(--gaming-bg-base)]"
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        exit={{ opacity: 0, y: 20 }}
        transition={{ duration: 0.2 }}
      >
        {/* Search header */}
        <div className="flex items-center gap-3 border-b border-[var(--gaming-border-glass)] px-4 py-3">
          <button
            type="button"
            onClick={onClose}
            aria-label="Chiudi ricerca"
            className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--gaming-text-secondary)] hover:bg-white/5"
          >
            <X className="h-5 w-5" />
          </button>
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--gaming-text-secondary)]" />
            <input
              ref={inputRef}
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Cerca un gioco su BoardGameGeek..."
              className={cn(
                'h-10 w-full rounded-lg pl-10 pr-4',
                'bg-white/5 border border-[var(--gaming-border-glass)]',
                'text-sm text-[var(--gaming-text-primary)] placeholder:text-[var(--gaming-text-secondary)]',
                'focus:outline-none focus:border-amber-500/50'
              )}
            />
          </div>
        </div>

        {/* Results */}
        <div className="flex-1 overflow-y-auto">
          {isLoading && (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-amber-400" />
            </div>
          )}

          {!isLoading && query.length >= 2 && results.length === 0 && (
            <div className="px-4 py-12 text-center text-sm text-[var(--gaming-text-secondary)]">
              Nessun risultato per &ldquo;{query}&rdquo;
            </div>
          )}

          {!isLoading && query.length < 2 && (
            <div className="px-4 py-12 text-center text-sm text-[var(--gaming-text-secondary)]">
              Scrivi almeno 2 caratteri per cercare
            </div>
          )}

          {results.map((game) => (
            <button
              key={game.bggId}
              type="button"
              onClick={() => onSelectGame(game)}
              className={cn(
                'flex w-full items-center gap-3 px-4 py-3',
                'text-left transition-colors hover:bg-white/5',
                'border-b border-[var(--gaming-border-glass)]'
              )}
            >
              {game.thumbnailUrl ? (
                <img
                  src={game.thumbnailUrl}
                  alt=""
                  className="h-12 w-12 shrink-0 rounded-lg object-cover"
                />
              ) : (
                <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-white/5">
                  <Search className="h-5 w-5 text-[var(--gaming-text-secondary)]" />
                </div>
              )}
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-[var(--gaming-text-primary)]">
                  {game.title}
                </p>
                {game.yearPublished && (
                  <p className="text-xs text-[var(--gaming-text-secondary)]">
                    {game.yearPublished}
                  </p>
                )}
              </div>
            </button>
          ))}
        </div>
      </motion.div>
    </AnimatePresence>
  );
}
```

**IMPORTANT:** After writing the component, verify the actual return type of `useSearchBggGames`. The data might be `data.items` or `data.results` or just an array. Read the hook file and adjust `results` extraction accordingly. If the hook requires different params (e.g., `useSearchBggGames({ query })` vs `useSearchBggGames(query)`), adjust the call.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/search/FullScreenSearch.test.tsx`
Expected: All 4 tests PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/search/FullScreenSearch.tsx apps/web/src/components/search/FullScreenSearch.test.tsx
git commit -m "feat(search): add FullScreenSearch overlay with BGG integration"
```

---

## Task 4: Create GamePreviewSheet Component

**Files:**
- Create: `src/components/search/GamePreviewSheet.tsx`
- Create: `src/components/search/GamePreviewSheet.test.tsx`

Bottom sheet showing game preview with one-tap "Add to Library" action. Reuses `useAddGameToLibrary`, `useLibraryQuota`, and `BottomSheet` from Phase 1.

- [ ] **Step 1: Read existing hooks to understand the add-to-library flow**

Read:
- `src/hooks/queries/useLibrary.ts` — find `useAddGameToLibrary` and `useLibraryQuota` return types
- `src/components/library/AddToLibraryButton.tsx` — understand quota checking pattern
- `src/lib/api/clients/gameNightBggClient.ts` — understand importGame flow

The BGG flow has TWO paths:
1. Game already in shared catalog → `useAddGameToLibrary(gameId)`
2. Game from BGG not in catalog → `api.gameNightBgg.importGame(bggId)` → creates PrivateGame + adds to library

We need to handle both. The simplest approach: always use importGame for BGG results (it handles both cases on the backend).

- [ ] **Step 2: Write the failing test**

Create `src/components/search/GamePreviewSheet.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GamePreviewSheet } from './GamePreviewSheet';

// Mock hooks
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryQuota: vi.fn(() => ({
    data: { currentCount: 5, maxAllowed: 50, tier: 'Free' },
  })),
}));

const mockGame = {
  bggId: 123,
  title: 'Catan',
  yearPublished: 1995,
  thumbnailUrl: 'https://example.com/catan.jpg',
};

describe('GamePreviewSheet', () => {
  it('renders game title when open', () => {
    render(
      <GamePreviewSheet
        open
        game={mockGame}
        onOpenChange={() => {}}
        onAdded={() => {}}
      />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders add to library button', () => {
    render(
      <GamePreviewSheet
        open
        game={mockGame}
        onOpenChange={() => {}}
        onAdded={() => {}}
      />
    );
    expect(screen.getByRole('button', { name: /aggiungi alla libreria/i })).toBeInTheDocument();
  });

  it('renders year when provided', () => {
    render(
      <GamePreviewSheet
        open
        game={mockGame}
        onOpenChange={() => {}}
        onAdded={() => {}}
      />
    );
    expect(screen.getByText('1995')).toBeInTheDocument();
  });

  it('does not render when no game', () => {
    render(
      <GamePreviewSheet
        open
        game={null}
        onOpenChange={() => {}}
        onAdded={() => {}}
      />
    );
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/search/GamePreviewSheet.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 4: Write GamePreviewSheet implementation**

Create `src/components/search/GamePreviewSheet.tsx`:

```tsx
'use client';

import React, { useState } from 'react';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { useLibraryQuota } from '@/hooks/queries/useLibrary';
import { Check, BookmarkPlus } from 'lucide-react';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import type { BggGameResult } from './FullScreenSearch';

export interface GamePreviewSheetProps {
  open: boolean;
  game: BggGameResult | null;
  onOpenChange: (open: boolean) => void;
  onAdded: (gameId: string) => void;
}

export function GamePreviewSheet({ open, game, onOpenChange, onAdded }: GamePreviewSheetProps) {
  const [isAdding, setIsAdding] = useState(false);
  const [isAdded, setIsAdded] = useState(false);
  const { data: quota } = useLibraryQuota();
  const { toast } = useToast();

  if (!game) return null;

  const isQuotaReached = quota && quota.currentCount >= quota.maxAllowed;

  const handleAdd = async () => {
    if (isAdded || isAdding) return;
    setIsAdding(true);
    try {
      const result = await api.gameNightBgg.importGame(game.bggId);
      setIsAdded(true);
      toast({
        title: 'Aggiunto alla libreria!',
        description: game.title,
      });
      onAdded(result.libraryEntryId ?? result.privateGameId);
    } catch (error) {
      toast({
        title: 'Errore',
        description: "Non è stato possibile aggiungere il gioco",
        variant: 'destructive',
      });
    } finally {
      setIsAdding(false);
    }
  };

  const handleAddToWishlist = () => {
    // TODO: Wire to wishlist API when implementing Phase 3
    toast({ title: 'Aggiunto alla wishlist', description: game.title });
  };

  return (
    <BottomSheet open={open} onOpenChange={onOpenChange} title={game.title}>
      <div className="flex flex-col gap-4">
        {/* Cover + info */}
        <div className="flex gap-4">
          {game.thumbnailUrl ? (
            <img
              src={game.thumbnailUrl}
              alt={game.title}
              className="h-24 w-24 shrink-0 rounded-lg object-cover"
            />
          ) : (
            <div className="flex h-24 w-24 shrink-0 items-center justify-center rounded-lg bg-white/5 text-3xl">
              🎲
            </div>
          )}
          <div className="flex flex-col justify-center">
            <h3 className="text-lg font-bold text-[var(--gaming-text-primary)]">{game.title}</h3>
            {game.yearPublished && (
              <p className="text-sm text-[var(--gaming-text-secondary)]">{game.yearPublished}</p>
            )}
          </div>
        </div>

        {/* Actions */}
        <div className="flex flex-col gap-2 pt-2">
          {isAdded ? (
            <GradientButton disabled fullWidth>
              <Check className="h-4 w-4" />
              Aggiunto
            </GradientButton>
          ) : (
            <GradientButton
              onClick={handleAdd}
              loading={isAdding}
              disabled={!!isQuotaReached}
              fullWidth
            >
              <BookmarkPlus className="h-4 w-4" />
              Aggiungi alla Libreria
            </GradientButton>
          )}

          {isQuotaReached && !isAdded && (
            <p className="text-center text-xs text-amber-400">
              Hai raggiunto il limite di {quota?.maxAllowed} giochi
            </p>
          )}

          <button
            type="button"
            onClick={handleAddToWishlist}
            className="py-2 text-center text-sm text-[var(--gaming-text-secondary)] hover:text-[var(--gaming-text-primary)]"
          >
            Aggiungi alla Wishlist
          </button>
        </div>
      </div>
    </BottomSheet>
  );
}
```

**IMPORTANT:** After writing, verify:
1. The actual `api.gameNightBgg.importGame()` call signature and response shape
2. The `useToast` hook import path (might be from a UI library or custom)
3. The `useLibraryQuota` return shape (`data.currentCount` vs `data?.quota?.currentCount`)

Adjust the code to match the actual API.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/search/GamePreviewSheet.test.tsx`
Expected: All 4 tests PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/search/GamePreviewSheet.tsx apps/web/src/components/search/GamePreviewSheet.test.tsx
git commit -m "feat(search): add GamePreviewSheet with one-tap add to library"
```

---

## Task 5: Create Dashboard Mobile Page

**Files:**
- Create: `src/app/(authenticated)/dashboard/dashboard-mobile.tsx`
- Modify: `src/app/(authenticated)/dashboard/page.tsx`

The new mobile-first dashboard wires all components together.

- [ ] **Step 1: Create dashboard-mobile.tsx**

```tsx
'use client';

import React, { useState } from 'react';
import { Search } from 'lucide-react';
import { cn } from '@/lib/utils';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { RecentGamesRow } from '@/components/dashboard/RecentGamesRow';
import { QuickActionCards } from '@/components/dashboard/QuickActionCards';
import { FullScreenSearch } from '@/components/search/FullScreenSearch';
import { GamePreviewSheet } from '@/components/search/GamePreviewSheet';
import type { BggGameResult } from '@/components/search/FullScreenSearch';

export function DashboardMobile() {
  const [searchOpen, setSearchOpen] = useState(false);
  const [selectedGame, setSelectedGame] = useState<BggGameResult | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);

  const handleSelectGame = (game: BggGameResult) => {
    setSelectedGame(game);
    setPreviewOpen(true);
  };

  const handleGameAdded = (gameId: string) => {
    // Close preview after a short delay to show success state
    setTimeout(() => {
      setPreviewOpen(false);
      setSelectedGame(null);
    }, 1500);
  };

  return (
    <div className="min-h-screen bg-[var(--gaming-bg-base)]">
      <MobileHeader title="MeepleAI" />

      <div className="flex flex-col gap-6 pb-20 pt-4">
        {/* Search bar */}
        <div className="px-4">
          <button
            type="button"
            onClick={() => setSearchOpen(true)}
            className={cn(
              'flex h-10 w-full items-center gap-3 rounded-lg px-4',
              'bg-white/5 border border-[var(--gaming-border-glass)]',
              'text-sm text-[var(--gaming-text-secondary)]',
              'transition-colors hover:border-amber-500/30'
            )}
          >
            <Search className="h-4 w-4" />
            Cerca un gioco...
          </button>
        </div>

        {/* Recent games */}
        <RecentGamesRow />

        {/* Quick actions */}
        <QuickActionCards onSearchClick={() => setSearchOpen(true)} />
      </div>

      {/* Full screen search overlay */}
      <FullScreenSearch
        open={searchOpen}
        onClose={() => setSearchOpen(false)}
        onSelectGame={handleSelectGame}
      />

      {/* Game preview bottom sheet */}
      <GamePreviewSheet
        open={previewOpen}
        game={selectedGame}
        onOpenChange={setPreviewOpen}
        onAdded={handleGameAdded}
      />
    </div>
  );
}
```

- [ ] **Step 2: Update page.tsx to use DashboardMobile**

Read `src/app/(authenticated)/dashboard/page.tsx` and update the import/render to use `DashboardMobile` instead of (or alongside) `DashboardClient`. If the page wraps with auth guards, keep those. Replace the main content component:

```tsx
import { DashboardMobile } from './dashboard-mobile';

// In the render:
<DashboardMobile />
```

Keep the old `dashboard-client.tsx` file but don't import it. We can remove it in a cleanup pass.

- [ ] **Step 3: Verify compilation**

Run: `cd apps/web && npx tsc --noEmit`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/
git commit -m "feat(dashboard): add mobile-first dashboard with search flow"
```

---

## Task 6: Integration Test & Visual Verification

- [ ] **Step 1: Run all new tests**

Run: `cd apps/web && pnpm vitest run src/components/search/ src/components/dashboard/`
Expected: All tests pass

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors

- [ ] **Step 4: Commit any fixes**

```bash
git commit -m "fix: resolve lint/type issues from Phase 2"
```

---

## Summary

| Task | Component | New Tests | Status |
|------|-----------|-----------|--------|
| 1 | RecentGamesRow | 0 (visual) | ☐ |
| 2 | QuickActionCards | 0 (visual) | ☐ |
| 3 | FullScreenSearch | 4 | ☐ |
| 4 | GamePreviewSheet | 4 | ☐ |
| 5 | DashboardMobile + page wiring | 0 (integration) | ☐ |
| 6 | Integration verification | — | ☐ |

**Total new tests: 8**
**Total new files: 7**
**Total modified files: 1**

**Key reuse**: ALL API calls use existing hooks (`useSearchBggGames`, `useLibrary`, `useLibraryQuota`, `api.gameNightBgg.importGame`). Zero new API integration needed.

After this phase: users can open the app, search BGG, preview a game, and add it to their library in one tap. Phase 3 (Library & Game Detail) builds on this.
