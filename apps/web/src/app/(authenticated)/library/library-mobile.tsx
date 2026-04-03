'use client';

/**
 * LibraryMobile - Mobile-first library page
 *
 * Segmented control: Collezione (catalog games) / Privati (custom games) / Wishlist
 *
 * Semantica segmenti:
 *   Collezione → isPrivateGame=false, stato != Wishlist
 *   Privati    → isPrivateGame=true (qualsiasi stato)
 *   Wishlist   → currentState=Wishlist
 *
 * Il fetch carica tutti i giochi (no stateFilter) e il filtering è client-side.
 *
 * Nota: BGG search rimosso dalle pagine utente (licenza commerciale BGG).
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';

import { Filter, Plus, Search } from 'lucide-react';

import { AddGameDrawer } from '@/app/(authenticated)/library/AddGameDrawer';
import { LibraryFilterSheet, type LibraryFilters } from '@/components/library/LibraryFilterSheet';
import { SegmentedControl, type Segment } from '@/components/library/SegmentedControl';
import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useLibrary, useLibraryStats } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ── Segments ─────────────────────────────────────────────────────────────────

const SEGMENTS: Segment[] = [
  { id: 'collection', label: 'Collezione' },
  { id: 'private', label: 'Privati' },
  { id: 'wishlist', label: 'Wishlist' },
];

// ── Client-side segment filter ────────────────────────────────────────────────

export function filterBySegment(items: UserLibraryEntry[], segment: string): UserLibraryEntry[] {
  switch (segment) {
    case 'collection':
      return items.filter(g => !g.isPrivateGame && g.currentState !== 'Wishlist');
    case 'private':
      return items.filter(g => g.isPrivateGame);
    case 'wishlist':
      return items.filter(g => g.currentState === 'Wishlist');
    default:
      return items;
  }
}

// ── Default filters ──────────────────────────────────────────────────────────

const DEFAULT_FILTERS: LibraryFilters = {
  search: '',
  state: null,
  sortBy: 'addedAt',
  favoritesOnly: false,
};

// ── Component ────────────────────────────────────────────────────────────────

export function LibraryMobile() {
  // ── Local state ──────────────────────────────────────────────────────────
  const [activeSegment, setActiveSegment] = useState('collection');
  const [searchText, setSearchText] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [filters, setFilters] = useState<LibraryFilters>(DEFAULT_FILTERS);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [addDrawerOpen, setAddDrawerOpen] = useState(false);
  const [pageSize, setPageSize] = useState(20);

  // ── Debounced search ─────────────────────────────────────────────────────
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(searchText), 300);
    return () => clearTimeout(timer);
  }, [searchText]);

  // Reset pageSize when search or filters change
  useEffect(() => {
    setPageSize(20);
  }, [debouncedSearch, filters, activeSegment]);

  // ── Segments with counts from stats ──────────────────────────────────────
  const { data: stats } = useLibraryStats();

  const segmentsWithCounts: Segment[] = useMemo(() => {
    if (!stats) return SEGMENTS;
    return [
      { id: 'collection', label: 'Collezione', count: stats.ownedCount },
      { id: 'private', label: 'Privati' }, // stats API non espone privateGameCount
      { id: 'wishlist', label: 'Wishlist', count: stats.wishlistCount },
    ];
  }, [stats]);

  // ── Fetch all games, filter client-side per segment ───────────────────────
  const { data, isLoading } = useLibrary({
    page: 1,
    pageSize,
    search: debouncedSearch || undefined,
    sortBy: filters.sortBy,
    sortDescending: filters.sortBy === 'addedAt',
    favoritesOnly: filters.favoritesOnly,
    // Nessun stateFilter: il filtering per segmento è client-side
  });

  const allGames = data?.items ?? [];

  const games = useMemo(() => filterBySegment(allGames, activeSegment), [allGames, activeSegment]);

  // ── Subtitle text ────────────────────────────────────────────────────────
  const subtitle = stats
    ? `${stats.totalGames} gioch${stats.totalGames === 1 ? 'o' : 'i'}`
    : undefined;

  // ── Filter apply callback ────────────────────────────────────────────────
  const handleApplyFilters = useCallback((newFilters: LibraryFilters) => {
    setFilters(newFilters);
  }, []);

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col gap-3 pb-24 lg:hidden">
      {/* Header */}
      <MobileHeader
        title="La Mia Libreria"
        subtitle={subtitle}
        rightActions={
          <div className="flex items-center gap-1">
            <button
              type="button"
              aria-label="Filtra"
              onClick={() => setFilterSheetOpen(true)}
              className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--gaming-text-secondary)] hover:bg-white/5"
            >
              <Filter className="h-5 w-5" />
            </button>
            <button
              type="button"
              aria-label="Aggiungi gioco"
              onClick={() => setAddDrawerOpen(true)}
              className="flex h-9 w-9 items-center justify-center rounded-full text-amber-400 hover:bg-amber-500/10"
            >
              <Plus className="h-5 w-5" />
            </button>
          </div>
        }
      />

      {/* Body content */}
      <div className="flex flex-col gap-3 px-4">
        {/* Segmented control */}
        <SegmentedControl
          segments={segmentsWithCounts}
          activeId={activeSegment}
          onChange={setActiveSegment}
        />

        {/* Search input */}
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--gaming-text-secondary)]" />
          <input
            type="text"
            value={searchText}
            onChange={e => setSearchText(e.target.value)}
            placeholder="Cerca nella libreria..."
            className="h-10 w-full rounded-lg bg-white/5 pl-9 pr-3 text-sm text-[var(--gaming-text-primary)] placeholder:text-[var(--gaming-text-secondary)] outline-none ring-1 ring-white/10 focus:ring-amber-500/40 transition-shadow"
          />
        </div>

        {/* Loading skeleton */}
        {isLoading && (
          <div className="grid grid-cols-2 gap-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <MeepleCardSkeleton key={i} variant="grid" />
            ))}
          </div>
        )}

        {/* Empty state */}
        {!isLoading && games.length === 0 && (
          <div className="flex flex-col items-center gap-4 py-16 text-center">
            <p className="text-sm text-[var(--gaming-text-secondary)]">
              {debouncedSearch ? 'Nessun risultato trovato' : 'Nessun gioco in questa sezione'}
            </p>
            {!debouncedSearch && activeSegment !== 'wishlist' && (
              <button
                type="button"
                onClick={() => setAddDrawerOpen(true)}
                className="inline-flex items-center gap-2 rounded-lg bg-amber-500/20 px-4 py-2 text-sm font-medium text-amber-400 hover:bg-amber-500/30 transition-colors"
              >
                <Plus className="h-4 w-4" />
                Aggiungi gioco
              </button>
            )}
          </div>
        )}

        {/* Game grid */}
        {!isLoading && games.length > 0 && (
          <div className="grid grid-cols-2 gap-3">
            {games.map(entry => (
              <MeepleCard
                key={entry.id}
                id={entry.gameId}
                entity="game"
                variant="grid"
                title={entry.gameTitle}
                subtitle={entry.gamePublisher ?? undefined}
                imageUrl={entry.gameImageUrl ?? undefined}
                rating={entry.averageRating ?? undefined}
                ratingMax={10}
                badge={entry.isFavorite ? '★' : undefined}
              />
            ))}
          </div>
        )}

        {/* Paginazione: "Carica altri" quando ci sono più risultati server-side */}
        {!isLoading && data?.hasNextPage && (
          <button
            type="button"
            onClick={() => setPageSize(prev => prev + 20)}
            className="w-full rounded-lg bg-white/5 px-4 py-3 text-sm font-medium text-[var(--gaming-text-secondary)] hover:bg-white/10 hover:text-[var(--gaming-text-primary)] transition-colors"
          >
            Carica altri
          </button>
        )}
      </div>

      {/* Filter bottom sheet */}
      <LibraryFilterSheet
        open={filterSheetOpen}
        onOpenChange={setFilterSheetOpen}
        filters={filters}
        onApply={handleApplyFilters}
      />

      {/* Add game drawer */}
      <AddGameDrawer open={addDrawerOpen} onClose={() => setAddDrawerOpen(false)} />
    </div>
  );
}
