/**
 * PersonalLibraryPage Component
 *
 * Gaming Immersive library layout with:
 * - LibraryPageHeader (title + count + CTA)
 * - LibraryHeroBanner (contextual: next session or discovery)
 * - Expanded filter chips (7 chips: Tutti, Recenti, Più giocati, Rating, 2-4p, <60min, Strategici)
 * - MeepleCard grid 4col (desktop) / list variant (mobile)
 * - Gaming immersive empty state
 * - Compact UsageWidget sidebar (desktop only)
 *
 * Splits games into two sections:
 *   1. Shared catalog games (sharedGameId exists / isPrivateGame = false)
 *   2. Private/custom games (privateGameId exists / isPrivateGame = true)
 */

'use client';

import { useMemo, useState } from 'react';

import { useRouter } from 'next/navigation';

import { useLayoutResponsive } from '@/components/layout/LayoutProvider';
import { SectionBlock } from '@/components/ui/SectionBlock';
import { useLibrary } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

import { CreateGameCtaCard } from './CreateGameCtaCard';
import { LibraryEmptyState } from './LibraryEmptyState';
import { LibraryFiltersPanel } from './LibraryFiltersPanel';
import { LibraryGameGrid } from './LibraryGameGrid';
import { LibraryHeroBanner } from './LibraryHeroBanner';
import { LibraryPageHeader } from './LibraryPageHeader';
import { LibraryToolbar } from './LibraryToolbar';
import { UsageWidget } from './UsageWidget';

// ── Filter logic ────────────────────────────────────────────────────────────

function applyFilter(items: UserLibraryEntry[], filterId: string): UserLibraryEntry[] {
  switch (filterId) {
    case 'recent':
      return [...items].sort(
        (a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime()
      );
    case 'rating':
      return [...items].sort((a, b) => (b.averageRating ?? 0) - (a.averageRating ?? 0));
    case 'players-2-4':
      return items.filter(
        g => g.minPlayers != null && g.maxPlayers != null && g.minPlayers <= 4 && g.maxPlayers >= 2
      );
    case 'under-60':
      return items.filter(g => g.playingTimeMinutes != null && g.playingTimeMinutes <= 60);
    case 'most-played':
      // playCount not yet on UserLibraryEntry DTO — sort by addedAt (oldest first = likely most played)
      return [...items].sort(
        (a, b) => new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime()
      );
    case 'strategy':
      // category/mechanics not yet on UserLibraryEntry DTO — requires backend GetUserGamesQuery filter
      // For now, return all items (chip acts as no-op until backend wired)
      return items;
    default:
      return items;
  }
}

// ── Main component ──────────────────────────────────────────────────────────

export interface PersonalLibraryPageProps {
  /** Additional CSS classes for the root element */
  className?: string;
}

/**
 * PersonalLibraryPage — library with filter chips, view toggle, and responsive layout.
 *
 * Renders two sections:
 * - "Dal Catalogo": shared catalog games added to the library
 * - "Giochi Personalizzati": private/custom games created by the user
 *
 * Mobile defaults to list view; desktop to grid with manual toggle.
 */
export function PersonalLibraryPage({ className }: PersonalLibraryPageProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');
  const [activeFilter, setActiveFilter] = useState('all');
  const { data, isLoading } = useLibrary();
  const { isMobile } = useLayoutResponsive();

  // Mobile always shows list; desktop uses user-selected viewMode
  const effectiveView = isMobile ? 'list' : viewMode;

  // Split items into catalog vs custom games
  const { catalogGames, customGames } = useMemo(() => {
    const items = data?.items ?? [];
    const catalog = items.filter(entry => !entry.isPrivateGame);
    const custom = items.filter(entry => entry.isPrivateGame);
    return { catalogGames: catalog, customGames: custom };
  }, [data]);

  // Apply search filter
  const query = searchQuery.toLowerCase().trim();
  const searchedCatalog = query
    ? catalogGames.filter(g => g.gameTitle.toLowerCase().includes(query))
    : catalogGames;
  const searchedCustom = query
    ? customGames.filter(g => g.gameTitle.toLowerCase().includes(query))
    : customGames;

  // Apply chip filter
  const filteredCatalog = applyFilter(searchedCatalog, activeFilter);
  const filteredCustom = applyFilter(searchedCustom, activeFilter);

  const totalCount = data?.totalCount ?? 0;

  const router = useRouter();
  const isEmpty = totalCount === 0 && !isLoading;

  // Trigger AddGameDrawer via URL param
  const handleAddGame = () => {
    const url = new URL(window.location.href);
    url.searchParams.set('action', 'add');
    router.push(url.pathname + url.search);
  };

  // Loading state
  if (isLoading) {
    return (
      <div className={cn('space-y-4', className)} data-testid="personal-library-page">
        {/* PageHeader skeleton */}
        <div className="flex items-center justify-between">
          <div className="flex items-baseline gap-3">
            <div className="h-8 w-48 rounded-md bg-muted animate-pulse" />
            <div className="h-6 w-20 rounded-full bg-muted animate-pulse" />
          </div>
          <div className="h-9 w-36 rounded-md bg-muted animate-pulse" />
        </div>
        {/* Hero skeleton */}
        <div className="h-[72px] rounded-[14px] bg-muted animate-pulse" />
        {/* Filter chips skeleton */}
        <div className="flex gap-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-8 w-20 rounded-full bg-muted animate-pulse" />
          ))}
        </div>
        {/* Card grid skeleton */}
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="h-[280px] rounded-xl bg-muted animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  // Full empty state — gaming immersive
  if (isEmpty) {
    return (
      <div className={cn('space-y-4', className)} data-testid="personal-library-page">
        <LibraryPageHeader gameCount={0} onAddGame={handleAddGame} />
        <LibraryEmptyState
          onExploreCatalog={() => router.push('/library?tab=public')}
          onCreateCustom={() => router.push('/library/private/add')}
        />
      </div>
    );
  }

  return (
    <div className={cn('space-y-4', className)} data-testid="personal-library-page">
      {/* PageHeader: title + count + CTA */}
      <LibraryPageHeader gameCount={totalCount} onAddGame={handleAddGame} />

      {/* Hero Banner: contextual (session or discovery) */}
      <LibraryHeroBanner />

      {/* Main content + sidebar */}
      <div className="flex items-start gap-5">
        {/* Content column */}
        <div className="min-w-0 flex-1 space-y-4">
          {/* Toolbar: search */}
          <LibraryToolbar
            totalCount={totalCount}
            searchQuery={searchQuery}
            onSearchChange={setSearchQuery}
          />

          {/* Filter chips row + view toggle */}
          <LibraryFiltersPanel
            activeFilter={activeFilter}
            onFilterChange={setActiveFilter}
            viewMode={viewMode}
            onViewModeChange={setViewMode}
          />

          {/* Section 1: Shared catalog games */}
          {(filteredCatalog.length > 0 || !query) && (
            <SectionBlock icon={'\ud83d\udcda'} title="Dal Catalogo">
              <LibraryGameGrid
                items={filteredCatalog}
                effectiveView={effectiveView}
                emptyMessage="Nessun gioco del catalogo corrisponde alla ricerca."
              />
            </SectionBlock>
          )}

          {/* Section 2: Private/custom games */}
          {(filteredCustom.length > 0 || !query) && (
            <SectionBlock icon={'\ud83c\udfae'} title="Giochi Personalizzati">
              {filteredCustom.length === 0 && !query ? (
                <CreateGameCtaCard />
              ) : (
                <>
                  <LibraryGameGrid
                    items={filteredCustom}
                    effectiveView={effectiveView}
                    emptyMessage="Nessun gioco personalizzato corrisponde alla ricerca."
                  />
                  {filteredCustom.length > 0 && (
                    <div className="mt-3">
                      <CreateGameCtaCard />
                    </div>
                  )}
                </>
              )}
            </SectionBlock>
          )}
        </div>

        {/* Sidebar: compact quota widget (desktop only) */}
        <aside className="hidden lg:block w-[200px] flex-shrink-0">
          <div className="sticky top-[68px]">
            <UsageWidget variant="compact" />
          </div>
        </aside>
      </div>
    </div>
  );
}

export default PersonalLibraryPage;
