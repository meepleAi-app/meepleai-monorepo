'use client';

/**
 * Vector Store Page (pgvector)
 * Issue #4861: Qdrant → pgvector migration — Task 7
 *
 * Displays vector store statistics, a semantic search panel with optional
 * game filter and limit selector, and a grid of VectorGameCard components.
 */

import { useCallback, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  AlertCircleIcon,
  RefreshCwIcon,
  SearchIcon,
  XCircleIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  DatabaseIcon,
  LayersIcon,
  ActivityIcon,
  GridIcon,
} from 'lucide-react';

import { VectorGameCard } from '@/components/admin/knowledge-base/vector-game-card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { VectorSearchResultItem } from '@/lib/api/schemas/admin-knowledge-base.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

const LIMIT_OPTIONS = [5, 10, 20, 50] as const;

function StatSkeleton() {
  return (
    <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-kb animate-pulse min-h-[88px]">
      <div className="h-2.5 w-24 bg-muted rounded" />
      <div className="h-7 w-16 bg-muted rounded mt-1" />
      <div className="h-2.5 w-20 bg-muted rounded mt-1" />
    </div>
  );
}

export default function VectorStorePage() {
  // ── Semantic Search state ──
  const [searchQuery, setSearchQuery] = useState('');
  const [searchLimit, setSearchLimit] = useState<string>('10');
  const [searchGameId, setSearchGameId] = useState<string>('all');
  const [searchResults, setSearchResults] = useState<VectorSearchResultItem[] | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [expandedResult, setExpandedResult] = useState<string | null>(null);

  const { data, isLoading, error, refetch, isRefetching } = useQuery({
    queryKey: ['admin', 'vector-stats'],
    queryFn: () => adminClient.getVectorStats(),
    staleTime: 60_000,
  });

  const handleSearch = useCallback(async () => {
    if (!searchQuery.trim()) return;
    setSearchLoading(true);
    setSearchError(null);
    setSearchResults(null);
    setExpandedResult(null);
    try {
      const gameIdParam = searchGameId !== 'all' ? searchGameId : undefined;
      const result = await adminClient.searchVectors(
        searchQuery.trim(),
        Number(searchLimit),
        gameIdParam
      );
      setSearchResults(result.results);
      if (result.errorMessage) {
        setSearchError(result.errorMessage);
      }
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setSearchLoading(false);
    }
  }, [searchQuery, searchLimit, searchGameId]);

  const gameBreakdown = data?.gameBreakdown ?? [];
  const totalVectors = data?.totalVectors ?? 0;
  const gamesIndexed = data?.gamesIndexed ?? 0;
  const dimensions = data?.dimensions ?? 0;
  const avgHealth = data?.avgHealthPercent ?? 0;

  const healthColor =
    avgHealth >= 90
      ? 'text-entity-toolkit'
      : avgHealth >= 70
        ? 'text-entity-agent'
        : 'text-entity-event';

  return (
    <div className="space-y-4">
      {/* Page toolbar */}
      <div className="flex justify-end">
        <Button
          variant="outline"
          size="sm"
          onClick={() => refetch()}
          disabled={isRefetching}
          className="gap-2"
        >
          <RefreshCwIcon className={`h-4 w-4 ${isRefetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {/* Error State */}
      {error && (
        <div className="rounded-lg border border-entity-event/30 bg-entity-event/12 p-4 flex items-center gap-3">
          <AlertCircleIcon className="h-5 w-5 text-entity-event shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-entity-event">Failed to load vector stats</p>
            <p className="text-xs text-entity-event/80 mt-1">
              {error instanceof Error ? error.message : 'Unknown error'}
            </p>
          </div>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            Retry
          </Button>
        </div>
      )}

      {/* KPI Strip */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <StatSkeleton key={i} />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
          {/* Total Vectors */}
          <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-kb min-h-[88px]">
            <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
              Total Vectors
            </span>
            <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground leading-tight">
              {totalVectors.toLocaleString()}
            </span>
            <span className="font-mono text-[11px] text-entity-toolkit">
              <DatabaseIcon className="inline h-3 w-3 mr-0.5 -mt-px" />
              pgvector store
            </span>
          </div>

          {/* Games Indexed */}
          <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-game min-h-[88px]">
            <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
              Games Indexed
            </span>
            <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground leading-tight">
              {gamesIndexed}
            </span>
            <span className="font-mono text-[11px] text-entity-toolkit">
              <GridIcon className="inline h-3 w-3 mr-0.5 -mt-px" />
              with vectors
            </span>
          </div>

          {/* Dimensions */}
          <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-chat min-h-[88px]">
            <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
              Dimensions
            </span>
            <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground leading-tight">
              {dimensions > 0 ? dimensions : '—'}
            </span>
            <span className="font-mono text-[11px] text-muted-foreground">
              <LayersIcon className="inline h-3 w-3 mr-0.5 -mt-px" />
              float32 embedding
            </span>
          </div>

          {/* Avg Health */}
          <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-toolkit min-h-[88px]">
            <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
              Avg Health
            </span>
            <span
              className={`font-quicksand text-[28px] font-extrabold tabular-nums leading-tight ${healthColor}`}
            >
              {data ? `${Math.round(avgHealth)}%` : '—'}
            </span>
            <span className="font-mono text-[11px] text-entity-toolkit">
              <ActivityIcon className="inline h-3 w-3 mr-0.5 -mt-px" />
              chunk completeness
            </span>
          </div>
        </div>
      )}

      {/* Semantic Search Panel */}
      <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
        <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
          <SearchIcon className="h-3.5 w-3.5 text-entity-kb shrink-0" />
          <h2 className="font-quicksand text-[13px] font-extrabold text-foreground">
            Semantic Search
          </h2>
          <span className="font-mono text-[10px] text-muted-foreground ml-1">cosine · top-k</span>
        </div>
        <div className="p-3.5 space-y-3">
          <p className="text-xs text-muted-foreground">
            Run a similarity search against the pgvector knowledge base.
          </p>

          {/* Search Controls */}
          <div className="flex flex-col sm:flex-row gap-2">
            {/* Query input */}
            <div className="relative flex-1">
              <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
              <Input
                placeholder="Enter a query to search vectors..."
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                onKeyDown={e => {
                  if (e.key === 'Enter') void handleSearch();
                }}
                className="pl-9 bg-background"
              />
            </div>

            {/* Game filter */}
            <Select value={searchGameId} onValueChange={setSearchGameId}>
              <SelectTrigger className="w-full sm:w-48 bg-background">
                <SelectValue placeholder="All games" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All games</SelectItem>
                {gameBreakdown.map(game => (
                  <SelectItem key={game.gameId} value={game.gameId}>
                    {game.gameName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {/* Limit selector */}
            <Select value={searchLimit} onValueChange={setSearchLimit}>
              <SelectTrigger className="w-full sm:w-28 bg-background">
                <SelectValue placeholder="Limit" />
              </SelectTrigger>
              <SelectContent>
                {LIMIT_OPTIONS.map(n => (
                  <SelectItem key={n} value={String(n)}>
                    {n} results
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Button
              onClick={() => void handleSearch()}
              disabled={searchLoading || !searchQuery.trim()}
              className="gap-2 bg-entity-kb text-white hover:bg-entity-kb/90"
            >
              {searchLoading ? (
                <RefreshCwIcon className="h-4 w-4 animate-spin" />
              ) : (
                <SearchIcon className="h-4 w-4" />
              )}
              Search
            </Button>
          </div>

          {/* Search Error */}
          {searchError && (
            <div className="flex items-center gap-2 text-xs text-entity-event bg-entity-event/12 rounded-lg px-3 py-2 border border-entity-event/30">
              <XCircleIcon className="h-4 w-4 shrink-0" />
              {searchError}
            </div>
          )}

          {/* Search Results */}
          {searchResults !== null && (
            <div className="space-y-1.5">
              <div className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground px-1">
                {searchResults.length > 0
                  ? `${searchResults.length} result${searchResults.length !== 1 ? 's' : ''} found`
                  : 'No results found for this query.'}
              </div>

              {searchResults.length > 0 && (
                <div className="rounded-[8px] border border-border/60 overflow-hidden">
                  {/* Table header */}
                  <div className="grid grid-cols-[110px_110px_1fr_70px_28px] gap-3 px-3.5 py-2 bg-background border-b border-border/60">
                    <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                      Doc ID
                    </span>
                    <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                      Page · Chunk
                    </span>
                    <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                      Snippet
                    </span>
                    <span
                      className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground text-right"
                      title="Score not exposed by the current API"
                    >
                      Score
                    </span>
                    <span />
                  </div>

                  {searchResults.map(item => {
                    const isExpanded = expandedResult === item.documentId;
                    const shortId = item.documentId.slice(0, 8) || item.documentId || '—';
                    return (
                      <div key={`${item.documentId}-${item.chunkIndex}`}>
                        <button
                          type="button"
                          onClick={() => setExpandedResult(isExpanded ? null : item.documentId)}
                          className={`w-full grid grid-cols-[110px_110px_1fr_70px_28px] gap-3 px-3.5 py-2.5 text-left border-b border-border/60 last:border-0 hover:bg-muted/50 transition-colors ${isExpanded ? 'bg-entity-kb/4' : ''}`}
                        >
                          <span className="font-mono text-[10.5px] font-bold text-entity-kb flex items-center gap-1.5 shrink-0 truncate">
                            <span className="h-1.5 w-1.5 rounded-full bg-entity-game shrink-0" />
                            {shortId}
                          </span>
                          <span className="font-mono text-[10px] text-muted-foreground shrink-0">
                            p.{item.pageNumber} · ch[#{item.chunkIndex}]
                          </span>
                          <span className="text-[11.5px] text-muted-foreground truncate">
                            {item.text}
                          </span>
                          <span className="font-mono text-[12px] font-bold text-entity-kb text-right shrink-0">
                            —
                          </span>
                          <span className="text-muted-foreground font-mono text-[12px] shrink-0">
                            {isExpanded ? (
                              <ChevronUpIcon className="h-3.5 w-3.5" />
                            ) : (
                              <ChevronDownIcon className="h-3.5 w-3.5" />
                            )}
                          </span>
                        </button>

                        {isExpanded && (
                          <div className="px-3.5 py-3 border-b border-border/60 bg-muted/30 grid grid-cols-1 md:grid-cols-[1fr_260px] gap-4">
                            <div className="text-[12.5px] text-muted-foreground leading-relaxed bg-background border border-border/60 rounded-[6px] p-3">
                              {item.text}
                            </div>
                            <div className="flex flex-col gap-1.5">
                              <div className="flex gap-2 font-mono text-[10.5px]">
                                <span className="text-muted-foreground uppercase tracking-wide text-[9.5px] font-bold min-w-[90px]">
                                  documentId
                                </span>
                                <span className="text-foreground break-all">{item.documentId}</span>
                              </div>
                              <div className="flex gap-2 font-mono text-[10.5px]">
                                <span className="text-muted-foreground uppercase tracking-wide text-[9.5px] font-bold min-w-[90px]">
                                  page
                                </span>
                                <span className="text-foreground">{item.pageNumber}</span>
                              </div>
                              <div className="flex gap-2 font-mono text-[10.5px]">
                                <span className="text-muted-foreground uppercase tracking-wide text-[9.5px] font-bold min-w-[90px]">
                                  chunk
                                </span>
                                <span className="text-foreground">{item.chunkIndex}</span>
                              </div>
                            </div>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </div>
      </section>

      {/* Game Breakdown Panel */}
      {isLoading && (
        <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
          <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
            <div className="h-3 w-24 bg-muted rounded animate-pulse" />
          </div>
          <div className="p-3.5 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-36 bg-muted rounded-[8px] animate-pulse" />
            ))}
          </div>
        </section>
      )}

      {!isLoading && gameBreakdown.length > 0 && (
        <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
          <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
            <h2 className="font-quicksand text-[13px] font-extrabold text-foreground">
              Game Breakdown
            </h2>
            <span className="font-mono text-[10px] text-muted-foreground ml-auto">
              sorted by vectors
            </span>
          </div>
          <div className="p-3.5 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
            {gameBreakdown.map(game => (
              <VectorGameCard key={game.gameId} game={game} />
            ))}
          </div>
        </section>
      )}

      {/* Empty State */}
      {!isLoading && !error && gameBreakdown.length === 0 && (
        <div className="text-center py-12 rounded-[10px] border border-border/60 bg-card">
          <DatabaseIcon className="h-10 w-10 text-muted-foreground mx-auto mb-3" />
          <p className="text-muted-foreground font-medium">No vectors indexed yet</p>
          <p className="text-xs text-muted-foreground mt-1">
            Vectors will appear here once documents are processed
          </p>
        </div>
      )}
    </div>
  );
}
