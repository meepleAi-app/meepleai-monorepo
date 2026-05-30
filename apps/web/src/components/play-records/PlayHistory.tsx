'use client';

/**
 * PlayHistory — Play Records Index (Task 1 Reskin)
 *
 * New architecture using sub-components:
 * - RecordsHero: Hero with KPI stats (deferred load via usePlayerStatistics)
 * - RecordFilters: Sticky filter bar with search, status chips, dropdowns, view toggle
 * - RecordCardList / RecordCardGrid: List and grid view variants
 * - Empty states, loading skeleton, error state
 *
 * Issue #1488: Play Records Index Reskin (Task 1)
 */

import { useEffect, useState } from 'react';

import Link from 'next/link';

import { usePlayHistory } from '@/lib/domain-hooks/usePlayRecords';
import {
  usePlayRecordsStore,
  selectFilters,
  selectHasActiveFilters,
} from '@/lib/stores/play-records-store';

import { RecordCardGrid } from './index/RecordCardGrid';
import { RecordCardList } from './index/RecordCardList';
import { RecordFilters } from './index/RecordFilters';
import { RecordsHero } from './index/RecordsHero';

// ── Component ────────────────────────────────────────────────────────────────

export interface PlayHistoryProps {
  /** Filtra per gioco specifico (entry point dalla library card) */
  gameId?: string;
  /** Limita il numero di risultati (per uso embedded, es. tab Storico) */
  limit?: number;
}

export function PlayHistory({ gameId: propGameId, limit }: PlayHistoryProps) {
  const [currentPage, setCurrentPage] = useState(1);
  const [search, setSearch] = useState('');
  const [view, setView] = useState<'list' | 'grid'>('list');

  const filters = usePlayRecordsStore(selectFilters);
  const hasActiveFilters = usePlayRecordsStore(selectHasActiveFilters);

  const setFilter = usePlayRecordsStore(state => state.setFilter);
  const resetFilters = usePlayRecordsStore(state => state.resetFilters);

  // Reset pagina quando cambiano i filtri
  useEffect(() => {
    setCurrentPage(1);
  }, [filters.gameId, filters.status, search]);

  const { data, isLoading, error } = usePlayHistory({
    page: currentPage,
    pageSize: limit ?? 20,
    gameId: propGameId ?? filters.gameId,
    status: filters.status === 'all' ? undefined : filters.status,
  });

  const allRecords = data?.records ?? [];
  const records = search.trim()
    ? allRecords.filter(r => r.gameName.toLowerCase().includes(search.toLowerCase()))
    : allRecords;
  const hasMore = data ? currentPage < data.totalPages : false;

  // AC-1.5: Empty state logic
  const showFirstRunEmpty = !isLoading && !error && allRecords.length === 0 && !hasActiveFilters;
  const showFilterEmpty =
    !isLoading && !error && records.length === 0 && (hasActiveFilters || search);

  return (
    <div className="flex flex-col" data-testid="play-history">
      {/* AC-1.1: RecordsHero with deferred KPI load */}
      <RecordsHero isLoading={isLoading} />

      {/* AC-1.2: RecordFilters sticky top */}
      <RecordFilters
        statusFilter={filters.status}
        view={view}
        search={search}
        onStatusChange={status => setFilter('status', status)}
        onViewChange={setView}
        onSearchChange={setSearch}
      />

      {/* Main content area */}
      <div className="flex-1 space-y-4 px-4 py-4 sm:px-8 sm:py-6 lg:px-12">
        {/* AC-1.6: Loading skeleton */}
        {isLoading && (
          <div className="space-y-2">
            {[...Array(5)].map((_, i) => (
              <div
                key={i}
                className="h-24 animate-pulse rounded-lg bg-muted sm:h-28"
                data-testid="skeleton-shimmer"
              />
            ))}
          </div>
        )}

        {/* AC-1.7: Error state */}
        {!isLoading && error && (
          <div
            className="rounded-lg border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400"
            data-testid="play-history-error"
            role="alert"
          >
            {error instanceof Error ? error.message : 'Errore nel caricamento delle partite.'}
            <button
              type="button"
              onClick={() => window.location.reload()}
              className="ml-2 inline-flex items-center gap-1 text-xs font-bold underline hover:no-underline"
            >
              ↻ Riprova
            </button>
          </div>
        )}

        {/* AC-1.5: First run empty state */}
        {showFirstRunEmpty && (
          <div
            className="flex flex-col items-center rounded-lg border border-dashed border-border-strong bg-card px-4 py-12 text-center"
            data-testid="play-history-empty-first-run"
          >
            <div
              className="mb-3 flex h-20 w-20 items-center justify-center rounded-full sm:h-24 sm:w-24"
              style={{
                background: 'radial-gradient(circle, var(--c-session) 0%, transparent 70%)',
                opacity: 0.15,
              }}
              aria-hidden="true"
            >
              <span className="text-4xl sm:text-5xl">🎯</span>
            </div>
            <h2 className="font-display text-lg font-extrabold text-foreground sm:text-xl">
              Nessuna partita registrata
            </h2>
            <p className="mt-2 text-sm text-muted-foreground sm:text-base">
              Registra la tua prima partita per tracciare punteggi, esiti e classifiche del gruppo.
            </p>
            <Link
              href="/play-records/new"
              className="mt-4 inline-flex items-center gap-2 rounded-md bg-entity-session px-4 py-2 font-display text-sm font-extrabold text-white shadow-lg shadow-entity-session/40 transition-all hover:bg-entity-session/90"
            >
              <span aria-hidden="true">+</span>
              Registra prima partita
            </Link>
          </div>
        )}

        {/* AC-1.5: Filter no-results empty state */}
        {showFilterEmpty && (
          <div
            className="flex flex-col items-center rounded-lg border border-dashed border-border-strong bg-card px-4 py-12 text-center"
            data-testid="play-history-empty-filter"
          >
            <div
              className="mb-3 flex h-14 w-14 items-center justify-center rounded-full sm:h-16 sm:w-16"
              style={{ background: 'var(--bg-muted)' }}
              aria-hidden="true"
            >
              <span className="text-xl sm:text-2xl">⌕</span>
            </div>
            <h3 className="font-display text-base font-extrabold text-foreground sm:text-lg">
              Nessuna partita per questi filtri
            </h3>
            <p className="mt-1 text-xs text-muted-foreground sm:text-sm">
              Prova a rimuovere alcuni vincoli o cambiare periodo.
            </p>
            <button
              type="button"
              onClick={resetFilters}
              className="mt-4 rounded-md border border-entity-session/40 px-3 py-1.5 font-display text-xs font-bold text-entity-session transition-colors hover:bg-entity-session/5 sm:px-4 sm:py-2 sm:text-sm"
            >
              ↻ Reset filtri
            </button>
          </div>
        )}

        {/* AC-1.3 + AC-1.4: Records list or grid */}
        {!isLoading && !error && records.length > 0 && (
          <>
            {view === 'list' ? (
              // AC-1.3: List view
              <div
                className="mx-auto flex max-w-5xl flex-col gap-2"
                role="list"
                aria-label="Play records"
              >
                {records.map(record => (
                  <div key={record.id} role="listitem">
                    <RecordCardList record={record} />
                  </div>
                ))}
              </div>
            ) : (
              // AC-1.4: Grid view (3-col desktop, 1-col mobile)
              <div
                className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3"
                role="grid"
                aria-label="Play records grid"
              >
                {records.map(record => (
                  <div key={record.id} role="gridcell">
                    <RecordCardGrid record={record} />
                  </div>
                ))}
              </div>
            )}

            {/* Pagination: Load more button */}
            {hasMore && (
              <div className="flex justify-center py-4">
                <button
                  type="button"
                  onClick={() => setCurrentPage(p => p + 1)}
                  className="text-sm font-bold text-muted-foreground transition-colors hover:text-foreground"
                  data-testid="load-more-btn"
                >
                  Carica altro…
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
