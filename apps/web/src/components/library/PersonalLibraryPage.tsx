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

import { Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useLayoutResponsive } from '@/components/layout/LayoutProvider';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { FilterChipsRow } from '@/components/ui/FilterChipsRow';
import { SectionBlock } from '@/components/ui/SectionBlock';
import { ViewToggle } from '@/components/ui/ViewToggle';
import { useLibrary } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

import { LibraryEmptyState } from './LibraryEmptyState';
import { LibraryHeroBanner } from './LibraryHeroBanner';
import { LibraryPageHeader } from './LibraryPageHeader';
import { LibraryToolbar } from './LibraryToolbar';
import { UsageWidget } from './UsageWidget';

// ── Filter chip definitions ─────────────────────────────────────────────────

const LIBRARY_FILTER_CHIPS = [
  { id: 'all', label: 'Tutti' },
  { id: 'recent', label: 'Recenti' },
  { id: 'most-played', label: 'Più giocati' },
  { id: 'rating', label: 'Rating \u2193' },
  { id: 'players-2-4', label: '2-4 giocatori' },
  { id: 'under-60', label: '< 60 min' },
  { id: 'strategy', label: 'Strategici' },
];

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
      // Sort by playCount if available, else by addedAt as proxy
      return [...items].sort(
        (a, b) =>
          ((b as UserLibraryEntry & { playCount?: number }).playCount ?? 0) -
            ((a as UserLibraryEntry & { playCount?: number }).playCount ?? 0) ||
          new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime()
      );
    case 'strategy':
      return items.filter(g => {
        const entry = g as UserLibraryEntry & { category?: string; mechanics?: string[] };
        return (
          entry.category?.toLowerCase() === 'strategy' ||
          entry.mechanics?.some(m => m.toLowerCase().includes('strateg')) ||
          false
        );
      });
    default:
      return items;
  }
}

// ── CTA card — "Crea gioco" at the end of the custom games section ──────────

function CreateGameCtaCard() {
  const router = useRouter();

  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-2',
        'w-full sm:w-[140px] flex-shrink-0 rounded-xl',
        'bg-[#161b22] border border-dashed border-[#30363d]',
        'min-h-[80px] sm:min-h-[160px]',
        'cursor-pointer transition-all duration-200',
        'hover:border-[#58a6ff] hover:bg-[#1c2128]',
        'focus:outline-none focus:ring-1 focus:ring-[#58a6ff]'
      )}
      onClick={() => router.push('/library/private/add')}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          router.push('/library/private/add');
        }
      }}
      role="button"
      tabIndex={0}
      aria-label="Crea un gioco personalizzato"
      data-testid="create-game-cta"
    >
      <Plus className="w-5 h-5 text-[#58a6ff]" aria-hidden="true" />
      <span className="text-[10px] font-medium text-[#58a6ff] text-center px-2">Crea gioco</span>
    </div>
  );
}

// ── Game card renderer ──────────────────────────────────────────────────────

function LibraryGameCard({
  entry,
  variant,
}: {
  entry: UserLibraryEntry;
  variant: 'grid' | 'list';
}) {
  return (
    <MeepleCard
      id={entry.id}
      entity="game"
      variant={variant}
      title={entry.gameTitle}
      subtitle={entry.gamePublisher ?? (entry.isPrivateGame ? 'Gioco personalizzato' : '')}
      imageUrl={entry.gameImageUrl ?? undefined}
      rating={entry.averageRating ?? undefined}
      ratingMax={10}
      status={
        entry.currentState === 'Owned'
          ? 'owned'
          : entry.currentState === 'Wishlist'
            ? 'wishlisted'
            : undefined
      }
      metadata={[
        ...(entry.minPlayers != null && entry.maxPlayers != null
          ? [{ label: 'Giocatori', value: `${entry.minPlayers}-${entry.maxPlayers}` }]
          : []),
        ...(entry.playingTimeMinutes != null
          ? [{ label: 'Durata', value: `${entry.playingTimeMinutes} min` }]
          : []),
      ]}
      data-testid={`library-card-${entry.id}`}
    />
  );
}

// ── Game grid/list container ────────────────────────────────────────────────

function GameListContainer({
  items,
  effectiveView,
  emptyMessage,
}: {
  items: UserLibraryEntry[];
  effectiveView: 'grid' | 'list';
  emptyMessage: string;
}) {
  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground py-4">{emptyMessage}</p>;
  }

  return (
    <div
      className={cn(
        effectiveView === 'grid'
          ? 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3'
          : 'flex flex-col gap-2'
      )}
    >
      {items.map(entry => (
        <LibraryGameCard key={entry.id} entry={entry} variant={effectiveView} />
      ))}
    </div>
  );
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
          onExploreCatalog={() => router.push('/games')}
          onImportBgg={() => router.push('/library/private/add')}
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
          <div className="flex items-center gap-3">
            <div className="min-w-0 flex-1">
              <FilterChipsRow
                chips={LIBRARY_FILTER_CHIPS}
                activeId={activeFilter}
                onSelect={setActiveFilter}
              />
            </div>
            {/* ViewToggle — desktop only */}
            {!isMobile && <ViewToggle view={viewMode} onViewChange={setViewMode} />}
          </div>

          {/* Section 1: Shared catalog games */}
          {(filteredCatalog.length > 0 || !query) && (
            <SectionBlock icon={'\ud83d\udcda'} title="Dal Catalogo">
              <GameListContainer
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
                  <GameListContainer
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
