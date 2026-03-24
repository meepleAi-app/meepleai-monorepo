/**
 * PersonalLibraryPage Component
 *
 * Vetrina layout for the user's personal library.
 * Splits games into two sections:
 *   1. Shared catalog games (sharedGameId exists / isPrivateGame = false)
 *   2. Private/custom games (privateGameId exists / isPrivateGame = true)
 *
 * Features:
 * - FilterChipsRow for quick filtering (Tutti, Recenti, Più giocati, Rating, players, time)
 * - ViewToggle for grid/list switching (desktop only)
 * - Mobile defaults to list view, desktop to grid
 * - MeepleCard rendering with variant switching
 */

'use client';

import { useMemo, useState } from 'react';

import { BookOpen, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { EmptyState } from '@/components/empty-state/EmptyState';
import { useLayoutResponsive } from '@/components/layout/LayoutProvider';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { FilterChipsRow } from '@/components/ui/FilterChipsRow';
import { SectionBlock } from '@/components/ui/SectionBlock';
import { ViewToggle } from '@/components/ui/ViewToggle';
import { useLibrary } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

import { LibraryToolbar } from './LibraryToolbar';

// ── Filter chip definitions ─────────────────────────────────────────────────

const LIBRARY_FILTER_CHIPS = [
  { id: 'all', label: 'Tutti' },
  { id: 'recent', label: 'Recenti' },
  { id: 'most-played', label: 'Più giocati' },
  { id: 'rating', label: 'Rating \u2193' },
  { id: 'players-2-4', label: '2-4 giocatori' },
  { id: 'under-60', label: '< 60 min' },
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
      // Sort by addedAt as proxy (no playCount field available)
      return [...items].sort(
        (a, b) => new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime()
      );
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

  // Loading state
  if (isLoading) {
    return (
      <div className={cn('space-y-8', className)} data-testid="personal-library-page">
        <div className="h-10 w-full max-w-sm rounded-md bg-[#161b22] animate-pulse" />
        <div className="space-y-3">
          <div className="h-5 w-40 rounded bg-[#21262d] animate-pulse" />
          <div className="flex gap-3">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="w-[140px] h-[160px] rounded-xl bg-[#21262d] animate-pulse" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  // Full empty state — no games at all
  if (totalCount === 0 && !isLoading) {
    return (
      <div className={cn('py-8', className)} data-testid="personal-library-page">
        <EmptyState
          title="La tua libreria è vuota"
          description="Aggiungi giochi dalla sezione Catalogo Condiviso o crea giochi personalizzati."
          icon={BookOpen}
          variant="noData"
          data-testid="library-empty-state"
        />
      </div>
    );
  }

  return (
    <div className={cn('space-y-6', className)} data-testid="personal-library-page">
      {/* Toolbar: search + count + view toggle */}
      <div className="flex items-center gap-3">
        <div className="flex-1">
          <LibraryToolbar
            totalCount={totalCount}
            searchQuery={searchQuery}
            onSearchChange={setSearchQuery}
          />
        </div>
        {/* ViewToggle — desktop only */}
        {!isMobile && <ViewToggle view={viewMode} onViewChange={setViewMode} />}
      </div>

      {/* Filter chips row */}
      <FilterChipsRow
        chips={LIBRARY_FILTER_CHIPS}
        activeId={activeFilter}
        onSelect={setActiveFilter}
      />

      {/* Section 1: Shared catalog games */}
      {(filteredCatalog.length > 0 || !query) && (
        <SectionBlock icon="\ud83d\udcda" title="Dal Catalogo">
          <GameListContainer
            items={filteredCatalog}
            effectiveView={effectiveView}
            emptyMessage="Nessun gioco del catalogo corrisponde alla ricerca."
          />
        </SectionBlock>
      )}

      {/* Section 2: Private/custom games */}
      {(filteredCustom.length > 0 || !query) && (
        <SectionBlock icon="\ud83c\udfae" title="Giochi Personalizzati">
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
  );
}

export default PersonalLibraryPage;
