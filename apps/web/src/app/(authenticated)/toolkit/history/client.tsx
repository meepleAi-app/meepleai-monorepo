'use client';

import type { JSX } from 'react';
import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { AlertCircle } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { HubPageContainer } from '@/components/layout/PageContainer';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import { downloadFile, rowsToCsv } from '@/lib/utils/csv';

import { HistoryCards } from './_components/HistoryCards';
import { HistoryDetailModal } from './_components/HistoryDetailModal';
import { HistoryPagination } from './_components/HistoryPagination';
import { HistoryTable } from './_components/HistoryTable';
import { HistoryToolbar } from './_components/HistoryToolbar';
import {
  countActiveFilters,
  filterRows,
  NO_WINNER_FILTER_VALUE,
  paginate,
  sortRows,
  toHistoryRow,
  type HistoryFilterState,
  type HistoryRow,
  type HistorySort,
} from './_lib/history-filters';
import { formatDuration } from './_lib/history-format';

/**
 * Toolkit History Page — orchestrator (Issue #3006, Task A9).
 *
 * Assembles the pure filter/sort/paginate pipeline (`_lib/history-filters`)
 * with the presentational components built in A1-A8 into the full
 * `/toolkit/history` experience. The backend history endpoint has no
 * search/filter/sort support, so a single batch (`limit: 500`) is fetched
 * once and the entire table experience — search, filters, sort, pagination,
 * CSV export — runs client-side.
 */

/** Batch size fetched from `GET /sessions/history` — see `_lib/history-filters.ts` header comment. */
const HISTORY_BATCH_LIMIT = 500;

/** Default page size, matching `HistoryPagination`'s `PAGE_SIZE_OPTIONS`. */
const DEFAULT_PAGE_SIZE = 20;

const DEFAULT_FILTER_STATE: HistoryFilterState = {
  search: '',
  gameIds: [],
  winners: [],
  datePreset: 'all',
  sort: 'recent',
};

interface ToolkitTab {
  id: 'stats' | 'history' | 'templates' | 'play';
  href: string;
  icon: string;
}

const TOOLKIT_TABS: ToolkitTab[] = [
  { id: 'stats', href: '/toolkit/stats', icon: '📊' },
  { id: 'history', href: '/toolkit/history', icon: '📜' },
  { id: 'templates', href: '/toolkit/templates', icon: '🎨' },
  { id: 'play', href: '/toolkit/play', icon: '🎮' },
];

export default function ToolkitHistoryPage(): JSX.Element {
  const router = useRouter();
  const { t, formatDate, formatTime } = useTranslation();

  const [filterState, setFilterState] = useState<HistoryFilterState>(DEFAULT_FILTER_STATE);
  const [view, setView] = useState<'table' | 'cards'>('table');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [detailRow, setDetailRow] = useState<HistoryRow | null>(null);

  const historyQuery = useQuery({
    queryKey: ['toolkit-history'],
    queryFn: () => api.sessions.getHistory({ limit: HISTORY_BATCH_LIMIT }),
  });

  // Full library page so every game a session references resolves to a real
  // title — falls back to `unknownLabel` for sessions on games no longer in
  // the user's library (e.g. removed private games).
  const { data: libraryData } = useLibrary({ pageSize: HISTORY_BATCH_LIMIT });

  const unknownLabel = t('pages.toolkitHistory.table.unknownGame');

  const gameNameMap = new Map<string, string>();
  for (const entry of libraryData?.items ?? []) {
    gameNameMap.set(entry.gameId, entry.gameTitle);
  }

  const sessions = historyQuery.data?.sessions ?? [];
  const allRows = sessions.map(dto => toHistoryRow(dto, gameNameMap, unknownLabel));

  // Client-side pipeline: filter → sort → paginate. `now` is fresh per
  // render — this is the orchestrator, not a pure helper, so determinism
  // isn't required here (unlike `_lib/history-filters.ts`).
  const now = new Date();
  const filtered = sortRows(filterRows(allRows, filterState, now), filterState.sort);
  const pageRows = paginate(filtered, page, pageSize);

  const gameCounts = new Map<string, { label: string; count: number }>();
  for (const row of allRows) {
    const existing = gameCounts.get(row.gameId);
    if (existing) existing.count += 1;
    else gameCounts.set(row.gameId, { label: row.gameName, count: 1 });
  }
  const gameOptions = Array.from(gameCounts.entries())
    .map(([id, { label, count }]) => ({ id, label, count }))
    .sort((a, b) => b.count - a.count);

  const winnerCounts = new Map<string, number>();
  let noWinnerCount = 0;
  for (const row of allRows) {
    if (row.winnerName == null) {
      noWinnerCount += 1;
      continue;
    }
    winnerCounts.set(row.winnerName, (winnerCounts.get(row.winnerName) ?? 0) + 1);
  }
  // Winner labels are the raw name — HistoryToolbar re-translates the
  // NO_WINNER_FILTER_VALUE entry's label itself, so what we pass here for it
  // is ignored.
  const winnerOptions = [
    ...Array.from(winnerCounts.entries()).map(([value, count]) => ({ value, label: value, count })),
    { value: NO_WINNER_FILTER_VALUE, label: NO_WINNER_FILTER_VALUE, count: noWinnerCount },
  ];

  const totalGames = new Set(allRows.map(row => row.gameId)).size;
  const totalWinners = winnerCounts.size;
  const activeFilterCount = countActiveFilters(filterState);

  const handleFilterChange = (next: HistoryFilterState) => {
    setFilterState(next);
    setPage(1);
  };

  const handleSortChange = (sort: HistorySort) => {
    handleFilterChange({ ...filterState, sort });
  };

  const handlePageSizeChange = (size: number) => {
    setPageSize(size);
    setPage(1);
  };

  const handleClearAll = () => {
    setFilterState(DEFAULT_FILTER_STATE);
    setPage(1);
  };

  const handleOpenGameStats = (_gameId: string) => {
    // No per-game stats route yet — send the user to the aggregate stats tab.
    router.push('/toolkit/stats');
  };

  const handleExport = () => {
    const headers = [
      t('pages.toolkitHistory.table.date'),
      t('pages.toolkitHistory.table.game'),
      t('pages.toolkitHistory.table.duration'),
      t('pages.toolkitHistory.table.players'),
      t('pages.toolkitHistory.table.winner'),
      t('pages.toolkitHistory.table.score'),
      t('pages.toolkitHistory.table.notes'),
    ];
    const csvRows = filtered.map(row => {
      const startedAtDate = new Date(row.startedAt);
      const winner = row.isCoop
        ? t('pages.toolkitHistory.table.coop')
        : (row.winnerName ?? t('pages.toolkitHistory.table.noWinner'));
      return [
        `${formatDate(startedAtDate)} ${formatTime(startedAtDate)}`,
        row.gameName,
        formatDuration(row.durationMinutes),
        row.playerNames.join(', '),
        winner,
        row.isCoop ? null : row.winScore,
        row.notes,
      ];
    });
    downloadFile(rowsToCsv(headers, csvRows), 'storico-sessioni.csv');
  };

  const isLoading = historyQuery.isLoading;
  const isError = historyQuery.isError;
  const hasRows = allRows.length > 0;
  const noFilteredResults = hasRows && filtered.length === 0 && activeFilterCount > 0;
  const showBatchNote = sessions.length === HISTORY_BATCH_LIMIT;

  return (
    <HubPageContainer className="flex flex-col gap-6">
      {/* breadcrumb */}
      <nav
        aria-label={t('pages.toolkitHistory.hero.breadcrumbAria')}
        className="flex items-center gap-1.5 text-sm text-muted-foreground"
      >
        <span>{t('pages.toolkitHistory.hero.breadcrumbToolkit')}</span>
        <span aria-hidden="true">›</span>
        <span className="font-medium text-foreground">
          {t('pages.toolkitHistory.hero.breadcrumbHistory')}
        </span>
      </nav>

      {/* hero */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span aria-hidden="true" className="text-2xl sm:text-3xl">
              🧰
            </span>
            <h1 className="font-quicksand text-2xl font-bold text-foreground sm:text-3xl">
              {t('pages.toolkitHistory.hero.title')}
            </h1>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            {t('pages.toolkitHistory.hero.subtitle')}
          </p>
        </div>
        <p className="text-sm text-muted-foreground">
          {t('pages.toolkitHistory.hero.quickStat', {
            sessions: allRows.length,
            games: totalGames,
            winners: totalWinners,
          })}
        </p>
      </div>

      {/* toolkit tabs */}
      <nav
        aria-label={t('pages.toolkitHistory.tabs.ariaLabel')}
        className="flex gap-1 overflow-x-auto border-b border-border"
      >
        {TOOLKIT_TABS.map(tab => (
          <Link
            key={tab.id}
            href={tab.href}
            aria-current={tab.id === 'history' ? 'page' : undefined}
            className={cn(
              'flex shrink-0 items-center gap-1.5 border-b-2 px-3 py-2 text-sm font-medium transition-colors',
              tab.id === 'history'
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            )}
          >
            <span aria-hidden="true">{tab.icon}</span>
            {t(`pages.toolkitHistory.tabs.${tab.id}`)}
          </Link>
        ))}
      </nav>

      {/* loading */}
      {isLoading && (
        <div
          role="status"
          aria-busy="true"
          aria-label={t('pages.toolkitHistory.loading.ariaLabel')}
          className="flex flex-col gap-3"
        >
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-16 w-full rounded-lg" />
          ))}
        </div>
      )}

      {/* error */}
      {!isLoading && isError && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive"
        >
          <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
          <span className="flex-1">{t('pages.toolkitHistory.error.message')}</span>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void historyQuery.refetch()}
          >
            {t('pages.toolkitHistory.error.retry')}
          </Button>
        </div>
      )}

      {/* empty (no sessions at all) */}
      {!isLoading && !isError && !hasRows && (
        <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-card px-6 py-16 text-center">
          <span aria-hidden="true" className="text-4xl">
            📜
          </span>
          <h2 className="text-lg font-semibold text-foreground">
            {t('pages.toolkitHistory.empty.title')}
          </h2>
          <p className="max-w-md text-sm text-muted-foreground">
            {t('pages.toolkitHistory.empty.body')}
          </p>
          <Button type="button" onClick={() => router.push('/toolkit')}>
            {t('pages.toolkitHistory.empty.cta')}
          </Button>
        </div>
      )}

      {/* toolbar + results */}
      {!isLoading && !isError && hasRows && (
        <div className="flex flex-col gap-4">
          <HistoryToolbar
            state={filterState}
            onChange={handleFilterChange}
            gameOptions={gameOptions}
            winnerOptions={winnerOptions}
            view={view}
            onViewChange={setView}
            totalCount={allRows.length}
            resultCount={filtered.length}
            onClearAll={handleClearAll}
            onExport={handleExport}
          />

          {showBatchNote && (
            <p className="text-xs text-muted-foreground">
              {t('pages.toolkitHistory.batchNote', { n: HISTORY_BATCH_LIMIT })}
            </p>
          )}

          {noFilteredResults ? (
            <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-card px-6 py-16 text-center">
              <span aria-hidden="true" className="text-4xl">
                🔍
              </span>
              <h2 className="text-lg font-semibold text-foreground">
                {t('pages.toolkitHistory.filteredEmpty.title')}
              </h2>
              <p className="max-w-md text-sm text-muted-foreground">
                {t('pages.toolkitHistory.filteredEmpty.body')}
              </p>
              <Button type="button" variant="outline" onClick={handleClearAll}>
                {t('pages.toolkitHistory.filteredEmpty.cta')}
              </Button>
            </div>
          ) : (
            <>
              {view === 'table' ? (
                <div className="overflow-x-auto rounded-lg border border-border">
                  <HistoryTable
                    rows={pageRows}
                    sort={filterState.sort}
                    onSortChange={handleSortChange}
                    onOpenDetail={setDetailRow}
                    onOpenGameStats={handleOpenGameStats}
                  />
                </div>
              ) : (
                <HistoryCards rows={pageRows} onOpenDetail={setDetailRow} />
              )}

              <HistoryPagination
                page={page}
                total={filtered.length}
                pageSize={pageSize}
                onPageChange={setPage}
                onPageSizeChange={handlePageSizeChange}
              />
            </>
          )}
        </div>
      )}

      <HistoryDetailModal row={detailRow} onClose={() => setDetailRow(null)} />
    </HubPageContainer>
  );
}
