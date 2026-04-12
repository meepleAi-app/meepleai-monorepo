'use client';

/**
 * PlayHistory — Lista partite mobile-first con filter chips
 *
 * - Ricerca inline
 * - Filter chips orizzontali (stato, ordinamento)
 * - MeepleCard variant="list" con badge stato + vincitore
 * - Empty state e skeleton
 * - Paginazione "Carica altro"
 */

import { useEffect, useState } from 'react';

import { Search, X } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { buildSessionNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
import type { PlayRecordStatus } from '@/lib/api/schemas/play-records.schemas';
import { usePlayHistory } from '@/lib/domain-hooks/usePlayRecords';
import {
  usePlayRecordsStore,
  selectFilters,
  selectHasActiveFilters,
} from '@/lib/stores/play-records-store';
import { cn } from '@/lib/utils';

// ── Helpers ─────────────────────────────────────────────────────────────────

function formatDuration(duration: string | null): string {
  if (!duration) return '';
  // .NET TimeSpan "HH:MM:SS" or "D.HH:MM:SS"
  // eslint-disable-next-line security/detect-unsafe-regex
  const dotNetMatch = duration.match(/^(?:(\d+)\.)?(\d+):(\d+):(\d+)$/);
  if (dotNetMatch) {
    const days = dotNetMatch[1] ? parseInt(dotNetMatch[1]) : 0;
    const hours = parseInt(dotNetMatch[2]) + days * 24;
    const minutes = parseInt(dotNetMatch[3]);
    const h = hours > 0 ? `${hours}h` : '';
    const m = minutes > 0 ? `${minutes}min` : '';
    return [h, m].filter(Boolean).join(' ') || '';
  }
  // ISO 8601 "PT2H30M"
  // eslint-disable-next-line security/detect-unsafe-regex
  const isoMatch = duration.match(/^PT(?:(\d+)H)?(?:(\d+)M)?$/);
  if (isoMatch) {
    const h = isoMatch[1] ? `${isoMatch[1]}h` : '';
    const m = isoMatch[2] ? `${isoMatch[2]}min` : '';
    return [h, m].filter(Boolean).join(' ') || '';
  }
  return duration;
}

function statusLabel(status: PlayRecordStatus): string {
  switch (status) {
    case 'Completed':
      return '✅ Completata';
    case 'InProgress':
      return '🔄 In corso';
    case 'Planned':
      return '📅 Pianificata';
    case 'Archived':
      return '🗄 Archiviata';
  }
}

function statusBadgeColor(status: PlayRecordStatus): string {
  switch (status) {
    case 'Completed':
      return 'text-emerald-400 bg-emerald-400/10';
    case 'InProgress':
      return 'text-blue-400 bg-blue-400/10';
    case 'Planned':
      return 'text-slate-400 bg-white/5';
    case 'Archived':
      return 'text-slate-500 bg-white/5';
  }
}

// ── Filter chips config ──────────────────────────────────────────────────────

const STATUS_CHIPS: { value: PlayRecordStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'Tutte' },
  { value: 'Completed', label: 'Completate' },
  { value: 'InProgress', label: 'In corso' },
  { value: 'Planned', label: 'Pianificate' },
];

const SORT_CHIPS: { value: 'recent' | 'oldest' | 'game'; label: string }[] = [
  { value: 'recent', label: 'Recenti' },
  { value: 'oldest', label: 'Meno recenti' },
  { value: 'game', label: 'Per gioco' },
];

// ── Component ────────────────────────────────────────────────────────────────

export interface PlayHistoryProps {
  /** Filtra per gioco specifico (entry point dalla library card) */
  gameId?: string;
  /** Limita il numero di risultati (per uso embedded, es. tab Storico) */
  limit?: number;
}

export function PlayHistory({ gameId: propGameId, limit }: PlayHistoryProps) {
  const router = useRouter();
  const [currentPage, setCurrentPage] = useState(1);
  const [search, setSearch] = useState('');

  const filters = usePlayRecordsStore(selectFilters);
  const sortBy = usePlayRecordsStore(state => state.sortBy);
  const hasActiveFilters = usePlayRecordsStore(selectHasActiveFilters);

  const setFilter = usePlayRecordsStore(state => state.setFilter);
  const resetFilters = usePlayRecordsStore(state => state.resetFilters);
  const setSortBy = usePlayRecordsStore(state => state.setSortBy);

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

  const records = data?.records ?? [];
  const hasMore = data ? currentPage < data.totalPages : false;

  return (
    <div className="flex flex-col gap-3" data-testid="play-history">
      {/* Search */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--gaming-text-muted,#666677)]" />
        <input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Cerca per gioco o giocatore…"
          className="w-full rounded-xl bg-white/5 border border-white/8 pl-9 pr-9 py-2.5 text-sm text-white placeholder:text-white/30 focus:outline-none focus:ring-1 focus:ring-white/20"
          data-testid="play-history-search"
        />
        {search && (
          <button
            type="button"
            onClick={() => setSearch('')}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-white/40 hover:text-white/70"
            aria-label="Cancella ricerca"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      {/* Filter chips — stato */}
      <div className="flex gap-2 overflow-x-auto scrollbar-none pb-0.5">
        {STATUS_CHIPS.map(chip => (
          <button
            key={chip.value}
            type="button"
            onClick={() => setFilter('status', chip.value as PlayRecordStatus | 'all')}
            className={cn(
              'flex-shrink-0 rounded-full border px-3 py-1 text-xs font-semibold transition-colors',
              filters.status === chip.value
                ? 'border-amber-500/40 bg-amber-500/15 text-amber-400'
                : 'border-white/8 bg-white/5 text-white/50 hover:border-white/15'
            )}
            data-testid={`filter-status-${chip.value}`}
          >
            {chip.label}
          </button>
        ))}

        <div className="h-5 w-px bg-white/10 flex-shrink-0 self-center" />

        {SORT_CHIPS.map(chip => (
          <button
            key={chip.value}
            type="button"
            onClick={() => setSortBy(chip.value)}
            className={cn(
              'flex-shrink-0 rounded-full border px-3 py-1 text-xs font-semibold transition-colors',
              sortBy === chip.value
                ? 'border-white/20 bg-white/10 text-white'
                : 'border-white/8 bg-white/5 text-white/40 hover:border-white/15'
            )}
            data-testid={`sort-${chip.value}`}
          >
            {chip.label}
          </button>
        ))}

        {hasActiveFilters && (
          <button
            type="button"
            onClick={resetFilters}
            className="flex-shrink-0 rounded-full border border-white/8 bg-white/5 px-3 py-1 text-xs font-semibold text-red-400 hover:bg-red-400/10"
            data-testid="reset-filters"
          >
            <X className="inline h-3 w-3 mr-1" />
            Reset
          </button>
        )}
      </div>

      {/* Loading skeleton */}
      {isLoading && (
        <div className="flex flex-col gap-2" data-testid="play-history-loading">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-20 animate-pulse rounded-xl bg-white/5" />
          ))}
        </div>
      )}

      {/* Error */}
      {!isLoading && error && (
        <div
          className="rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400"
          data-testid="play-history-error"
        >
          {error instanceof Error ? error.message : 'Errore nel caricamento delle partite.'}
        </div>
      )}

      {/* Empty */}
      {!isLoading && !error && records.length === 0 && (
        <div
          className="flex flex-col items-center gap-4 py-16 text-center"
          data-testid="play-history-empty"
        >
          <span className="text-5xl">🎲</span>
          <div>
            <p className="text-base font-bold text-white">Nessuna partita registrata</p>
            <p className="mt-1 text-sm text-white/40">
              {hasActiveFilters
                ? 'Nessun risultato con i filtri attivi.'
                : 'Inizia a registrare le tue partite!'}
            </p>
          </div>
          {hasActiveFilters && (
            <button
              type="button"
              onClick={resetFilters}
              className="rounded-xl border border-white/10 px-4 py-2 text-sm font-semibold text-white/60 hover:bg-white/5"
            >
              Rimuovi filtri
            </button>
          )}
        </div>
      )}

      {/* List */}
      {!isLoading && !error && records.length > 0 && (
        <div className="flex flex-col gap-2">
          {records.map(record => {
            const dateStr = new Date(record.sessionDate).toLocaleDateString('it-IT', {
              day: 'numeric',
              month: 'short',
              year: 'numeric',
            });
            const dur = formatDuration(record.duration);

            const metaParts = [
              `${record.playerCount} ${record.playerCount === 1 ? 'giocatore' : 'giocatori'}`,
              dateStr,
              ...(dur ? [dur] : []),
            ];

            return (
              <div key={record.id} className="relative">
                <MeepleCard
                  entity="session"
                  variant="list"
                  title={record.gameName}
                  subtitle={dateStr}
                  metadata={metaParts.map(label => ({ label }))}
                  badge={record.status}
                  navItems={buildSessionNavItems(
                    {
                      playerCount: record.playerCount,
                      hasNotes: false,
                      toolCount: 0,
                      photoCount: 0,
                    },
                    { onPlayersClick: undefined }
                  )}
                  onClick={() => router.push(`/play-records/${record.id}`)}
                  data-testid={`play-record-${record.id}`}
                />
                {/* Status + winner overlay */}
                <div className="pointer-events-none absolute bottom-2.5 right-3 flex flex-col items-end gap-1">
                  <span
                    className={cn(
                      'rounded-md px-2 py-0.5 text-[10px] font-semibold',
                      statusBadgeColor(record.status)
                    )}
                  >
                    {statusLabel(record.status)}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Load more */}
      {hasMore && (
        <button
          type="button"
          onClick={() => setCurrentPage(p => p + 1)}
          className="py-3 text-sm font-semibold text-white/40 hover:text-white/70"
          data-testid="load-more-btn"
        >
          Carica altro…
        </button>
      )}
    </div>
  );
}
