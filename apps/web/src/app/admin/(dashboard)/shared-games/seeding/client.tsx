'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { AlertCircleIcon, DownloadIcon, RefreshCwIcon, SproutIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { api } from '@/lib/api';
import type { SeedingGameDto } from '@/lib/api/schemas/seeding.schemas';

// ============================================================================
// Constants
// ============================================================================

/** GameDataStatus enum values */
const GAME_DATA_STATUS = {
  Skeleton: 0,
  EnrichmentQueued: 1,
  Enriching: 2,
  Enriched: 3,
  Complete: 5,
  Failed: 6,
} as const;

type StatusFilter = 'all' | '0' | '1' | '2' | '3' | '5' | '6';

const STATUS_FILTER_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: 'all', label: 'All Statuses' },
  { value: '0', label: 'Skeleton' },
  { value: '1', label: 'Enrichment Queued' },
  { value: '2', label: 'Enriching' },
  { value: '3', label: 'Enriched' },
  { value: '5', label: 'Complete' },
  { value: '6', label: 'Failed' },
];

const POLLING_INTERVAL_MS = 5000;

// ============================================================================
// Badge helpers
// ============================================================================

function getStatusBadgeClass(status: number): string {
  switch (status) {
    case GAME_DATA_STATUS.Skeleton:
      return 'border-transparent bg-red-500 text-white';
    case GAME_DATA_STATUS.EnrichmentQueued:
      return 'border-transparent bg-slate-400 text-white';
    case GAME_DATA_STATUS.Enriching:
      return 'border-transparent bg-blue-500 text-white';
    case GAME_DATA_STATUS.Enriched:
      return 'border-transparent bg-yellow-400 text-black';
    case GAME_DATA_STATUS.Complete:
      return 'border-transparent bg-emerald-500 text-white';
    case GAME_DATA_STATUS.Failed:
      return 'border-transparent bg-orange-500 text-white';
    default:
      return 'border-transparent bg-slate-300 text-slate-800';
  }
}

// ============================================================================
// Component
// ============================================================================

export function SeedingPageClient() {
  const [games, setGames] = useState<SeedingGameDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [enriching, setEnriching] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [enrichMessage, setEnrichMessage] = useState<string | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // ---- Data fetching ----

  const fetchGames = useCallback(async () => {
    try {
      const data = await api.sharedGames.getSeedingStatus();
      setGames(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load seeding status');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchGames();
    intervalRef.current = setInterval(() => {
      void fetchGames();
    }, POLLING_INTERVAL_MS);

    // Pause polling when tab is hidden to reduce backend load
    const handleVisibilityChange = () => {
      if (document.hidden) {
        if (intervalRef.current) clearInterval(intervalRef.current);
      } else {
        void fetchGames();
        intervalRef.current = setInterval(() => {
          void fetchGames();
        }, POLLING_INTERVAL_MS);
      }
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [fetchGames]);

  // ---- Filtered games ----

  const filteredGames = useMemo<SeedingGameDto[]>(() => {
    if (statusFilter === 'all') return games;
    const targetStatus = parseInt(statusFilter, 10);
    return games.filter(g => g.gameDataStatus === targetStatus);
  }, [games, statusFilter]);

  // ---- Selection ----

  const allFilteredSelected =
    filteredGames.length > 0 && filteredGames.every(g => selectedIds.has(g.id));

  const someFilteredSelected = filteredGames.some(g => selectedIds.has(g.id));

  const toggleSelectAll = useCallback(() => {
    if (allFilteredSelected) {
      setSelectedIds(prev => {
        const next = new Set(prev);
        filteredGames.forEach(g => next.delete(g.id));
        return next;
      });
    } else {
      setSelectedIds(prev => {
        const next = new Set(prev);
        filteredGames.forEach(g => next.add(g.id));
        return next;
      });
    }
  }, [allFilteredSelected, filteredGames]);

  const toggleSelect = useCallback((id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  // ---- Enrichable games ----

  const enrichableGames = useMemo(
    () =>
      games.filter(
        g =>
          selectedIds.has(g.id) &&
          g.bggId !== null &&
          (g.gameDataStatus === GAME_DATA_STATUS.Skeleton ||
            g.gameDataStatus === GAME_DATA_STATUS.Failed)
      ),
    [games, selectedIds]
  );

  const enrichableCount = enrichableGames.length;
  const estimatedSeconds = enrichableCount; // 1 req/sec BGG rate limit

  const failedSelectedGames = useMemo(
    () =>
      games.filter(
        g =>
          selectedIds.has(g.id) && g.bggId !== null && g.gameDataStatus === GAME_DATA_STATUS.Failed
      ),
    [games, selectedIds]
  );
  const failedSelectedCount = failedSelectedGames.length;

  // ---- Actions ----

  const handleEnrich = useCallback(async () => {
    if (enrichableCount === 0) return;
    setEnriching(true);
    setEnrichMessage(null);
    try {
      const bggIds = enrichableGames.map(g => g.bggId as number);
      await api.sharedGames.enqueueBggEnrichment(bggIds);
      setEnrichMessage(`Queued ${bggIds.length} game(s) for enrichment.`);
      setSelectedIds(new Set());
      await fetchGames();
    } catch (err) {
      setEnrichMessage(
        `Failed to enqueue: ${err instanceof Error ? err.message : 'Unknown error'}`
      );
    } finally {
      setEnriching(false);
    }
  }, [enrichableCount, enrichableGames, fetchGames]);

  const handleRetryFailed = useCallback(async () => {
    if (failedSelectedCount === 0) return;
    setEnriching(true);
    setEnrichMessage(null);
    try {
      const bggIds = failedSelectedGames.map(g => g.bggId as number);
      await api.sharedGames.retryBggEnrichment(bggIds);
      setEnrichMessage(`Re-queued ${bggIds.length} failed game(s) for enrichment.`);
      setSelectedIds(new Set());
      await fetchGames();
    } catch (err) {
      setEnrichMessage(`Retry failed: ${err instanceof Error ? err.message : 'Unknown error'}`);
    } finally {
      setEnriching(false);
    }
  }, [failedSelectedCount, failedSelectedGames, fetchGames]);

  const handleDownload = useCallback(async () => {
    setDownloading(true);
    try {
      await api.sharedGames.downloadTrackingExport();
    } catch (err) {
      console.error('Export failed:', err);
    } finally {
      setDownloading(false);
    }
  }, []);

  // ---- Render ----

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground flex items-center gap-2">
          <SproutIcon className="h-6 w-6 text-emerald-500" />
          Seeding &amp; Enrichment
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Manage BGG game enrichment and track seeding progress
        </p>
      </div>

      {/* Rate limit banner */}
      {enrichableCount > 0 && (
        <div className="flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-800/40 dark:bg-amber-900/20 dark:text-amber-300">
          <AlertCircleIcon className="h-4 w-4 shrink-0" />
          <span>
            BGG rate limit: 1 req/sec. Enriching <strong>{enrichableCount}</strong>{' '}
            {enrichableCount === 1 ? 'game' : 'games'} takes ~{estimatedSeconds}{' '}
            {estimatedSeconds === 1 ? 'second' : 'seconds'}.
          </span>
        </div>
      )}

      {/* Feedback message */}
      {enrichMessage && (
        <div className="rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-800/40 dark:bg-blue-900/20 dark:text-blue-300">
          {enrichMessage}
        </div>
      )}

      {/* Card */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 space-y-0 pb-4">
          <CardTitle className="font-quicksand text-lg font-semibold">
            Games ({filteredGames.length})
          </CardTitle>

          <div className="flex items-center gap-2 flex-wrap">
            {/* Status filter */}
            <Select
              value={statusFilter}
              onValueChange={v => {
                setStatusFilter(v as StatusFilter);
                setSelectedIds(new Set());
              }}
            >
              <SelectTrigger className="w-[180px]">
                <SelectValue placeholder="Filter by status" />
              </SelectTrigger>
              <SelectContent>
                {STATUS_FILTER_OPTIONS.map(opt => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {/* Refresh */}
            <Button
              variant="outline"
              size="sm"
              onClick={() => void fetchGames()}
              disabled={loading}
            >
              <RefreshCwIcon className={`h-4 w-4 mr-1.5 ${loading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>

            {/* Enrich selected */}
            <Button
              variant="default"
              size="sm"
              onClick={() => void handleEnrich()}
              disabled={enrichableCount === 0 || enriching}
              title={
                enrichableCount === 0
                  ? 'Select Skeleton or Failed games with BGG IDs to enrich'
                  : undefined
              }
            >
              <SproutIcon className="h-4 w-4 mr-1.5" />
              {enriching
                ? 'Queuing…'
                : enrichableCount > 0
                  ? `Enrich Selected (${enrichableCount})`
                  : 'Enrich Selected'}
            </Button>

            {/* Retry failed */}
            {failedSelectedCount > 0 && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => void handleRetryFailed()}
                disabled={enriching}
              >
                <RefreshCwIcon className="h-4 w-4 mr-1.5" />
                Retry Failed ({failedSelectedCount})
              </Button>
            )}

            {/* Download Excel */}
            <Button
              variant="outline"
              size="sm"
              onClick={() => void handleDownload()}
              disabled={downloading}
            >
              <DownloadIcon className="h-4 w-4 mr-1.5" />
              {downloading ? 'Downloading…' : 'Download Excel'}
            </Button>
          </div>
        </CardHeader>

        <CardContent className="p-0">
          {error ? (
            <div className="px-6 py-8 text-center text-sm text-destructive">{error}</div>
          ) : loading && games.length === 0 ? (
            <div className="px-6 py-8 text-center text-sm text-muted-foreground">Loading…</div>
          ) : filteredGames.length === 0 ? (
            <div className="px-6 py-8 text-center text-sm text-muted-foreground">
              No games match the selected filter.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-10 pl-6">
                    <Checkbox
                      checked={allFilteredSelected}
                      data-state={
                        allFilteredSelected
                          ? 'checked'
                          : someFilteredSelected
                            ? 'indeterminate'
                            : 'unchecked'
                      }
                      onCheckedChange={toggleSelectAll}
                      aria-label="Select all"
                    />
                  </TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead className="w-32">BGG ID</TableHead>
                  <TableHead className="w-44">Data Status</TableHead>
                  <TableHead className="w-24 text-center">Has PDF</TableHead>
                  <TableHead className="w-32">Game Status</TableHead>
                  <TableHead className="w-24 text-center">RAG Ready</TableHead>
                  <TableHead className="w-40">Created</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredGames.map(game => (
                  <TableRow key={game.id} className="cursor-pointer hover:bg-muted/40">
                    <TableCell className="pl-6">
                      <Checkbox
                        checked={selectedIds.has(game.id)}
                        onCheckedChange={() => toggleSelect(game.id)}
                        aria-label={`Select ${game.title}`}
                      />
                    </TableCell>
                    <TableCell className="font-medium">{game.title}</TableCell>
                    <TableCell className="text-muted-foreground text-sm">
                      {game.bggId ?? '—'}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Badge className={getStatusBadgeClass(game.gameDataStatus)}>
                          {game.gameDataStatusName}
                        </Badge>
                        {game.gameDataStatus === GAME_DATA_STATUS.Failed && game.errorMessage && (
                          <span
                            className="text-xs text-orange-600 truncate max-w-[200px] cursor-help"
                            title={game.errorMessage}
                          >
                            {game.errorMessage}
                          </span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-center">
                      {game.hasUploadedPdf ? (
                        <span className="text-emerald-600 font-semibold text-sm">Yes</span>
                      ) : (
                        <span className="text-muted-foreground text-sm">No</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className="text-xs">
                        {game.gameStatusName}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-center">
                      {game.isRagReady ? (
                        <span className="text-emerald-600 font-semibold text-sm">Yes</span>
                      ) : (
                        <span className="text-muted-foreground text-sm">No</span>
                      )}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {new Date(game.createdAt).toLocaleDateString()}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Next-steps guidance when all games are enriched */}
      {games.length > 0 && games.every(g => g.gameDataStatus === GAME_DATA_STATUS.Complete) && (
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800 dark:border-emerald-800/40 dark:bg-emerald-900/20 dark:text-emerald-300">
          All games are enriched. Next step: upload PDFs via{' '}
          <a href="/admin/knowledge-base/upload" className="underline font-medium">
            Upload &amp; Process
          </a>{' '}
          to enable RAG.
        </div>
      )}
    </div>
  );
}
