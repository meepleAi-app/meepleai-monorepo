'use client';

import { useState, useMemo } from 'react';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';

import { HubLayout, type FilterChip } from '@/components/layout/HubLayout';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useGames } from '@/hooks/queries/useGames';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

// ========== Filter chip sets per tab ==========

const LIBRARY_FILTERS: FilterChip[] = [
  { id: 'all', label: 'Tutti' },
  { id: 'recent', label: 'Recenti' },
];

const CATALOG_FILTERS: FilterChip[] = [
  { id: 'all', label: 'Tutti' },
  { id: 'top', label: 'Top Rated' },
];

const KB_FILTERS: FilterChip[] = [{ id: 'all', label: 'Tutti' }];

// ========== Loading skeleton ==========

function LoadingSkeleton() {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3 px-4">
      {Array.from({ length: 8 }).map((_, i) => (
        <Skeleton key={i} className="h-40 rounded-xl" />
      ))}
    </div>
  );
}

// ========== Empty states per tab ==========

function EmptyLibrary() {
  return (
    <div className="flex flex-col items-center gap-3 py-16 px-4 text-center text-[var(--nh-text-muted,#94a3b8)]">
      <span className="text-5xl">🎲</span>
      <p className="font-medium">Nessun gioco in libreria.</p>
      <p className="text-sm">Aggiungi i tuoi giochi preferiti per trovarli qui.</p>
    </div>
  );
}

function EmptyCatalog() {
  return (
    <div className="flex flex-col items-center gap-3 py-16 px-4 text-center text-[var(--nh-text-muted,#94a3b8)]">
      <span className="text-5xl">🔍</span>
      <p className="font-medium">Nessun gioco nel catalogo.</p>
      <p className="text-sm">Prova a cambiare i filtri di ricerca.</p>
    </div>
  );
}

function EmptyKB() {
  return (
    <div className="flex flex-col items-center gap-3 py-16 px-4 text-center text-[var(--nh-text-muted,#94a3b8)]">
      <span className="text-5xl">📚</span>
      <p className="font-medium">Nessun documento nella Knowledge Base.</p>
      <p className="text-sm">Carica i regolamenti dei tuoi giochi per abilitare l&apos;AI.</p>
    </div>
  );
}

// ========== Hub page ==========

export default function GamesHubPage() {
  const searchParams = useSearchParams();
  const activeTab = (searchParams.get('tab') ?? 'library') as 'library' | 'catalog' | 'kb';

  const [search, setSearch] = useState('');
  const [activeFilter, setActiveFilter] = useState('all');
  const [viewMode, setViewMode] = useState<'grid' | 'list' | 'carousel'>('grid');

  const { data: libraryData, isLoading: libraryLoading } = useLibrary({ page: 1, pageSize: 20 });
  const { data: catalogData, isLoading: catalogLoading } = useGames(undefined, undefined, 1, 20);

  useMiniNavConfig({
    breadcrumb: 'Giochi',
    tabs: [
      { id: 'library', label: 'Libreria', href: '/games?tab=library' },
      { id: 'catalog', label: 'Catalogo', href: '/games?tab=catalog' },
      { id: 'kb', label: 'Knowledge Base', href: '/games?tab=kb' },
    ],
    activeTabId: activeTab,
  });

  // ---- Library items ----
  const libraryItems = useMemo(() => {
    const entries = libraryData?.items ?? [];
    const q = search.toLowerCase();
    // For "recent", sort by addedAt descending and take the first 10
    const sorted =
      activeFilter === 'recent'
        ? [...entries]
            .sort((a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime())
            .slice(0, 10)
        : entries;
    return sorted
      .filter(entry => !q || entry.gameTitle.toLowerCase().includes(q))
      .map(entry => ({
        entity: 'game' as const,
        id: entry.id,
        title: entry.gameTitle,
        subtitle: entry.gamePublisher ?? undefined,
        imageUrl: entry.gameImageUrl ?? undefined,
        rating: entry.averageRating ?? undefined,
        variant: 'grid' as const,
      }));
  }, [libraryData, search, activeFilter]);

  // ---- Catalog items ----
  const catalogItems = useMemo(() => {
    const games = catalogData?.games ?? [];
    const q = search.toLowerCase();
    return games
      .filter(game => {
        if (activeFilter === 'top') {
          return (game.averageRating ?? 0) > 7;
        }
        return true;
      })
      .filter(game => !q || game.title.toLowerCase().includes(q))
      .map(game => ({
        entity: 'game' as const,
        id: game.id,
        title: game.title,
        subtitle: game.publisher ?? undefined,
        imageUrl: game.imageUrl ?? undefined,
        rating: game.averageRating ?? undefined,
        variant: 'grid' as const,
      }));
  }, [catalogData, search, activeFilter]);

  // ---- Determine active tab state ----
  const isLoading =
    activeTab === 'library' ? libraryLoading : activeTab === 'catalog' ? catalogLoading : false;

  const items =
    activeTab === 'library' ? libraryItems : activeTab === 'catalog' ? catalogItems : [];

  const currentFilters =
    activeTab === 'library'
      ? LIBRARY_FILTERS
      : activeTab === 'catalog'
        ? CATALOG_FILTERS
        : KB_FILTERS;

  const topActions = (
    <Link
      href="/games/new"
      className="inline-flex items-center gap-1 h-9 px-3 text-sm font-semibold rounded-2xl bg-[var(--nh-text-primary,#1a1a1a)] text-white shrink-0"
    >
      ＋
    </Link>
  );

  return (
    <HubLayout
      searchPlaceholder="Cerca giochi..."
      filterChips={currentFilters}
      activeFilterId={activeFilter}
      onFilterChange={setActiveFilter}
      searchValue={search}
      onSearchChange={setSearch}
      viewMode={viewMode}
      onViewModeChange={setViewMode}
      showViewToggle
      topActions={topActions}
    >
      {isLoading ? (
        <LoadingSkeleton />
      ) : items.length === 0 ? (
        activeTab === 'library' ? (
          <EmptyLibrary />
        ) : activeTab === 'catalog' ? (
          <EmptyCatalog />
        ) : (
          <EmptyKB />
        )
      ) : (
        <div
          className={
            viewMode === 'list'
              ? 'flex flex-col gap-2 px-4 pb-24'
              : 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3 px-4 pb-24'
          }
        >
          {items.map(item => (
            <MeepleCard key={item.id} {...item} />
          ))}
        </div>
      )}
    </HubLayout>
  );
}
