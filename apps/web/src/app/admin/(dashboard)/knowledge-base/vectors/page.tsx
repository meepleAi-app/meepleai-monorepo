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
import { Card, CardContent } from '@/components/ui/data-display/card';
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
    <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
      <CardContent className="p-4">
        <div className="flex items-center gap-3">
          <div className="h-10 w-10 rounded-lg bg-slate-200 dark:bg-zinc-700 animate-pulse" />
          <div className="space-y-2">
            <div className="h-3 w-24 bg-slate-200 dark:bg-zinc-700 rounded animate-pulse" />
            <div className="h-6 w-16 bg-slate-200 dark:bg-zinc-700 rounded animate-pulse" />
          </div>
        </div>
      </CardContent>
    </Card>
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
      ? 'text-green-600 dark:text-green-400'
      : avgHealth >= 70
        ? 'text-amber-600 dark:text-amber-400'
        : 'text-red-600 dark:text-red-400';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Vector Store
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            pgvector knowledge base — embeddings health and semantic search
          </p>
        </div>
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
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 rounded-lg p-4 flex items-center gap-3">
          <AlertCircleIcon className="h-5 w-5 text-red-500 shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-red-800 dark:text-red-200">
              Failed to load vector stats
            </p>
            <p className="text-xs text-red-600 dark:text-red-400 mt-1">
              {error instanceof Error ? error.message : 'Unknown error'}
            </p>
          </div>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            Retry
          </Button>
        </div>
      )}

      {/* Stats Row */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <StatSkeleton key={i} />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {/* Total Vectors */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardContent className="p-4">
              <div className="flex items-center gap-3">
                <div className="h-10 w-10 rounded-lg bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center">
                  <DatabaseIcon className="h-5 w-5 text-blue-600 dark:text-blue-400" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Total Vectors</p>
                  <p className="text-xl font-bold text-foreground">
                    {totalVectors.toLocaleString()}
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Games Indexed */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardContent className="p-4">
              <div className="flex items-center gap-3">
                <div className="h-10 w-10 rounded-lg bg-purple-100 dark:bg-purple-900/30 flex items-center justify-center">
                  <GridIcon className="h-5 w-5 text-purple-600 dark:text-purple-400" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Games Indexed</p>
                  <p className="text-xl font-bold text-foreground">{gamesIndexed}</p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Dimensions */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardContent className="p-4">
              <div className="flex items-center gap-3">
                <div className="h-10 w-10 rounded-lg bg-cyan-100 dark:bg-cyan-900/30 flex items-center justify-center">
                  <LayersIcon className="h-5 w-5 text-cyan-600 dark:text-cyan-400" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Dimensions</p>
                  <p className="text-xl font-bold text-foreground">
                    {dimensions > 0 ? dimensions : '\u2014'}
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Avg Health */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardContent className="p-4">
              <div className="flex items-center gap-3">
                <div className="h-10 w-10 rounded-lg bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                  <ActivityIcon className="h-5 w-5 text-green-600 dark:text-green-400" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Avg Health</p>
                  <p className={`text-xl font-bold ${healthColor}`}>
                    {data ? `${Math.round(avgHealth)}%` : '\u2014'}
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Semantic Search Panel */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-6 space-y-4">
        <div>
          <h2 className="font-quicksand font-semibold text-lg text-foreground flex items-center gap-2">
            <SearchIcon className="h-5 w-5 text-blue-500" />
            Semantic Search
          </h2>
          <p className="text-sm text-muted-foreground mt-1">
            Run a similarity search against the pgvector knowledge base.
          </p>
        </div>

        {/* Search Controls */}
        <div className="flex flex-col sm:flex-row gap-3">
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
              className="pl-9 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md"
            />
          </div>

          {/* Game filter */}
          <Select value={searchGameId} onValueChange={setSearchGameId}>
            <SelectTrigger className="w-full sm:w-48 bg-white/70 dark:bg-zinc-800/70">
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
            <SelectTrigger className="w-full sm:w-28 bg-white/70 dark:bg-zinc-800/70">
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
            className="gap-2 bg-blue-600 hover:bg-blue-700 text-white"
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
          <div className="flex items-center gap-2 text-sm text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-900/20 rounded-lg px-3 py-2 border border-red-200 dark:border-red-800/40">
            <XCircleIcon className="h-4 w-4 shrink-0" />
            {searchError}
          </div>
        )}

        {/* Search Results */}
        {searchResults !== null && (
          <div className="space-y-2">
            <div className="text-sm font-medium text-foreground">
              {searchResults.length > 0
                ? `${searchResults.length} result${searchResults.length !== 1 ? 's' : ''} found`
                : 'No results found for this query.'}
            </div>
            {searchResults.map(item => (
              <div
                key={`${item.documentId}-${item.chunkIndex}`}
                className="bg-slate-50/70 dark:bg-zinc-900/50 rounded-lg border border-slate-200/50 dark:border-zinc-700/50 overflow-hidden"
              >
                <button
                  type="button"
                  onClick={() =>
                    setExpandedResult(expandedResult === item.documentId ? null : item.documentId)
                  }
                  className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-slate-100/60 dark:hover:bg-zinc-800/60 transition-colors"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <span className="text-xs font-mono text-muted-foreground shrink-0">
                      p.{item.pageNumber} c.{item.chunkIndex}
                    </span>
                    <span className="text-sm text-foreground truncate">{item.text}</span>
                  </div>
                  <div className="shrink-0 ml-2">
                    {expandedResult === item.documentId ? (
                      <ChevronUpIcon className="h-4 w-4 text-muted-foreground" />
                    ) : (
                      <ChevronDownIcon className="h-4 w-4 text-muted-foreground" />
                    )}
                  </div>
                </button>

                {expandedResult === item.documentId && (
                  <div className="px-4 py-3 border-t border-slate-200/50 dark:border-zinc-700/50 bg-white/50 dark:bg-zinc-900/30">
                    <div className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">
                      Details
                    </div>
                    <div className="space-y-1 text-xs">
                      <div className="flex gap-2">
                        <span className="text-muted-foreground font-mono shrink-0">
                          documentId:
                        </span>
                        <span className="text-foreground break-all">{item.documentId}</span>
                      </div>
                      <div className="flex gap-2">
                        <span className="text-muted-foreground font-mono shrink-0">page:</span>
                        <span className="text-foreground">{item.pageNumber}</span>
                      </div>
                      <div className="flex gap-2">
                        <span className="text-muted-foreground font-mono shrink-0">chunk:</span>
                        <span className="text-foreground">{item.chunkIndex}</span>
                      </div>
                      <div className="flex gap-2">
                        <span className="text-muted-foreground font-mono shrink-0">text:</span>
                        <span className="text-foreground break-all">{item.text}</span>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Game Cards Grid */}
      {isLoading && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div
              key={i}
              className="h-40 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse"
            />
          ))}
        </div>
      )}

      {!isLoading && gameBreakdown.length > 0 && (
        <div className="space-y-3">
          <h2 className="font-quicksand font-semibold text-lg text-foreground">Game Breakdown</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            {gameBreakdown.map(game => (
              <VectorGameCard key={game.gameId} game={game} />
            ))}
          </div>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !error && gameBreakdown.length === 0 && (
        <div className="text-center py-12 bg-white/50 dark:bg-zinc-800/50 backdrop-blur-sm rounded-xl border border-slate-200/40 dark:border-zinc-700/30">
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
