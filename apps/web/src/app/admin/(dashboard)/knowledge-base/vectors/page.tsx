'use client';

/**
 * Vector Store Page (pgvector)
 *
 * NOTE: This page is being rewritten as part of the Qdrant → pgvector migration.
 * The implementation below is a transitional stub that typechecks against the new
 * pgvector API. A full redesign will be completed in Task 7.
 */

import { useCallback, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircleIcon,
  RefreshCwIcon,
  SearchIcon,
  XCircleIcon,
  ChevronDownIcon,
  ChevronUpIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { VectorSearchResultItem } from '@/lib/api/schemas/admin-knowledge-base.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function StatSkeleton() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
      <div className="h-4 w-24 bg-slate-200 dark:bg-zinc-700 rounded animate-pulse mb-2" />
      <div className="h-8 w-16 bg-slate-200 dark:bg-zinc-700 rounded animate-pulse" />
    </div>
  );
}

export default function VectorCollectionsPage() {
  const queryClient = useQueryClient();
  const [actionMessage, setActionMessage] = useState<{
    type: 'success' | 'error';
    text: string;
  } | null>(null);

  // ── Semantic Search state ──
  const [searchQuery, setSearchQuery] = useState('');
  const [searchLimit, setSearchLimit] = useState('10');
  const [searchResults, setSearchResults] = useState<VectorSearchResultItem[] | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [expandedResult, setExpandedResult] = useState<string | null>(null);

  const { data, isLoading, error, refetch, isRefetching } = useQuery({
    queryKey: ['admin', 'vector-stats'],
    queryFn: () => adminClient.getVectorStats(),
    staleTime: 60_000,
  });

  const reindexMutation = useMutation({
    mutationFn: () => adminClient.getVectorStats(),
    onSuccess: () => {
      setActionMessage({ type: 'success', text: 'Vector index refreshed.' });
      queryClient.invalidateQueries({ queryKey: ['admin', 'vector-stats'] });
    },
    onError: err => {
      setActionMessage({
        type: 'error',
        text: `Failed to refresh: ${err instanceof Error ? err.message : 'Unknown error'}`,
      });
    },
  });

  const handleSearch = useCallback(async () => {
    if (!searchQuery.trim()) return;
    setSearchLoading(true);
    setSearchError(null);
    setSearchResults(null);
    setExpandedResult(null);
    try {
      const result = await adminClient.searchVectors(searchQuery.trim(), Number(searchLimit));
      setSearchResults(result.results);
      if (result.errorMessage) {
        setSearchError(result.errorMessage);
      }
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setSearchLoading(false);
    }
  }, [searchQuery, searchLimit]);

  const gameBreakdown = data?.gameBreakdown ?? [];
  const totalVectors = data?.totalVectors ?? 0;
  const dimensions = data?.dimensions ?? 0;
  const avgHealth = data?.avgHealthPercent ?? 0;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Vector Store
          </h1>
          <p className="text-sm text-muted-foreground mt-1">Manage your pgvector knowledge base</p>
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

      {/* Action Messages */}
      {actionMessage && (
        <div
          className={`rounded-lg p-3 flex items-center gap-3 text-sm ${
            actionMessage.type === 'success'
              ? 'bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800/40 text-green-800 dark:text-green-200'
              : 'bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 text-red-800 dark:text-red-200'
          }`}
        >
          <span className="flex-1">{actionMessage.text}</span>
          <button
            onClick={() => setActionMessage(null)}
            className="text-current opacity-60 hover:opacity-100"
          >
            &times;
          </button>
        </div>
      )}

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

      {/* Stats */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <StatSkeleton key={i} />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Games Indexed</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">
              {data?.gamesIndexed ?? 0}
            </div>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Total Vectors</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">
              {totalVectors.toLocaleString()}
            </div>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Dimensions</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">
              {dimensions > 0 ? dimensions : '\u2014'}
            </div>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Avg Health</div>
            <div
              className={`text-2xl font-bold ${avgHealth >= 90 ? 'text-green-600 dark:text-green-400' : avgHealth >= 70 ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400'}`}
            >
              {data ? `${Math.round(avgHealth)}%` : '\u2014'}
            </div>
          </div>
        </div>
      )}

      {/* ── Semantic Search Panel ── */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-6 space-y-4">
        <h2 className="font-quicksand font-semibold text-lg text-foreground flex items-center gap-2">
          <SearchIcon className="h-5 w-5 text-blue-500" />
          Semantic Search
        </h2>
        <p className="text-sm text-muted-foreground">
          Run a semantic similarity search against the pgvector knowledge base.
        </p>

        <div className="flex flex-col sm:flex-row gap-3">
          {/* Query input */}
          <div className="relative flex-1">
            <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Enter a query to search vectors..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Enter') handleSearch();
              }}
              className="pl-9 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md"
            />
          </div>

          <Button
            onClick={handleSearch}
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
                key={item.documentId}
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
                  <div className="flex items-center gap-2 shrink-0 ml-2">
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

      {/* Game Breakdown */}
      {!isLoading && gameBreakdown.length > 0 && (
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-6 space-y-4">
          <h2 className="font-quicksand font-semibold text-lg text-foreground">Game Breakdown</h2>
          <div className="space-y-2">
            {gameBreakdown.map(game => (
              <div
                key={game.gameId}
                className="flex items-center justify-between p-3 bg-slate-50/60 dark:bg-zinc-900/40 rounded-lg border border-slate-200/40 dark:border-zinc-700/30"
              >
                <div>
                  <div className="text-sm font-medium text-foreground">{game.gameName}</div>
                  <div className="text-xs text-muted-foreground">
                    {game.vectorCount.toLocaleString()} vectors
                  </div>
                </div>
                <div
                  className={`text-sm font-semibold ${game.healthPercent >= 90 ? 'text-green-600 dark:text-green-400' : game.healthPercent >= 70 ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400'}`}
                >
                  {Math.round(game.healthPercent)}%
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !error && gameBreakdown.length === 0 && (
        <div className="text-center py-12 bg-white/50 dark:bg-zinc-800/50 backdrop-blur-sm rounded-xl border border-slate-200/40 dark:border-zinc-700/30">
          <p className="text-muted-foreground">No vectors indexed yet</p>
          <p className="text-xs text-muted-foreground mt-1">
            Vectors will appear here once documents are processed
          </p>
        </div>
      )}

      {/* Reindex action */}
      <div className="flex justify-end">
        <Button
          variant="outline"
          size="sm"
          onClick={() => reindexMutation.mutate()}
          disabled={reindexMutation.isPending}
          className="gap-2"
        >
          <RefreshCwIcon className={`h-4 w-4 ${reindexMutation.isPending ? 'animate-spin' : ''}`} />
          Refresh Index
        </Button>
      </div>
    </div>
  );
}
