'use client';

/**
 * LibraryMobile - Mobile-first library page
 *
 * Phase 3, Task 4: Mobile library with grid layout, segmented tabs,
 * search bar, filter sheet, and catalog game import flow.
 *
 * Note: BGG search was removed from user pages (restricted to admin only
 * due to BGG commercial use licensing). Users add games via the shared
 * catalog or manual entry through AddGameDrawer.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';

import { Filter, Plus, Search } from 'lucide-react';

import { AddGameDrawer } from '@/app/(authenticated)/library/AddGameDrawer';
import { LibraryFilterSheet, type LibraryFilters } from '@/components/library/LibraryFilterSheet';
import { SegmentedControl, type Segment } from '@/components/library/SegmentedControl';
import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useLibrary, useLibraryStats } from '@/hooks/queries/useLibrary';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';

// ── Segments ─────────────────────────────────────────────────────────────────

const SEGMENTS: Segment[] = [
  { id: 'collection', label: 'Collezione' },
  { id: 'private', label: 'Privati' },
  { id: 'wishlist', label: 'Wishlist' },
];

const SEGMENT_STATE_MAP: Record<string, GameStateType[] | undefined> = {
  collection: ['Owned'],
  private: ['Nuovo', 'InPrestito'],
  wishlist: ['Wishlist'],
};

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

  // ── Debounced search ─────────────────────────────────────────────────────
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(searchText), 300);
    return () => clearTimeout(timer);
  }, [searchText]);

  // ── Segments with counts ─────────────────────────────────────────────────
  const { data: stats } = useLibraryStats();

  const segmentsWithCounts: Segment[] = useMemo(() => {
    if (!stats) return SEGMENTS;
    return [
      { id: 'collection', label: 'Collezione', count: stats.ownedCount },
      {
        id: 'private',
        label: 'Privati',
        count: stats.nuovoCount + stats.inPrestitoCount,
      },
      { id: 'wishlist', label: 'Wishlist', count: stats.wishlistCount },
    ];
  }, [stats]);

  // ── Build query params ───────────────────────────────────────────────────
  const stateFilter = filters.state
    ? [filters.state as GameStateType]
    : SEGMENT_STATE_MAP[activeSegment];

  const { data, isLoading } = useLibrary({
    page: 1,
    pageSize: 50,
    search: debouncedSearch || undefined,
    stateFilter,
    sortBy: filters.sortBy,
    sortDescending: filters.sortBy === 'addedAt',
    favoritesOnly: filters.favoritesOnly,
  });

  const games = data?.items ?? [];

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
              {debouncedSearch ? 'Nessun risultato trovato' : 'La tua libreria è vuota'}
            </p>
            {!debouncedSearch && (
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
      </div>

      {/* Filter bottom sheet */}
      <LibraryFilterSheet
        open={filterSheetOpen}
        onOpenChange={setFilterSheetOpen}
        filters={filters}
        onApply={handleApplyFilters}
      />

      {/* Add game drawer (catalog search / manual entry) */}
      <AddGameDrawer open={addDrawerOpen} onClose={() => setAddDrawerOpen(false)} />
    </div>
  );
}
