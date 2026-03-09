'use client';

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

import { VectorCollectionCard } from '@/components/admin/knowledge-base/vector-collection-card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { createAdminClient, type QdrantSearchResultItem } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function CardSkeleton() {
  return (
    <div className="h-[200px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
  );
}

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
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<{
    type: 'success' | 'error';
    text: string;
  } | null>(null);

  // ── Semantic Search state ──
  const [searchQuery, setSearchQuery] = useState('');
  const [searchCollection, setSearchCollection] = useState('');
  const [searchLimit, setSearchLimit] = useState('10');
  const [searchResults, setSearchResults] = useState<QdrantSearchResultItem[] | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [expandedResult, setExpandedResult] = useState<number | null>(null);

  const { data, isLoading, error, refetch, isRefetching } = useQuery({
    queryKey: ['admin', 'vector-collections'],
    queryFn: () => adminClient.getVectorCollections(),
    staleTime: 60_000,
  });

  const deleteMutation = useMutation({
    mutationFn: (name: string) => adminClient.deleteQdrantCollection(name),
    onSuccess: (_data, name) => {
      setActionMessage({ type: 'success', text: `Collection "${name}" deleted.` });
      setConfirmDelete(null);
      queryClient.invalidateQueries({ queryKey: ['admin', 'vector-collections'] });
    },
    onError: (err, name) => {
      setActionMessage({
        type: 'error',
        text: `Failed to delete "${name}": ${err instanceof Error ? err.message : 'Unknown error'}`,
      });
      setConfirmDelete(null);
    },
  });

  const rebuildMutation = useMutation({
    mutationFn: (name: string) => adminClient.rebuildQdrantIndex(name),
    onSuccess: (_data, name) => {
      setActionMessage({ type: 'success', text: `Rebuild triggered for "${name}".` });
    },
    onError: (err, name) => {
      setActionMessage({
        type: 'error',
        text: `Failed to rebuild "${name}": ${err instanceof Error ? err.message : 'Unknown error'}`,
      });
    },
  });

  const handleDelete = useCallback((name: string) => {
    setConfirmDelete(name);
    setActionMessage(null);
  }, []);

  const handleConfirmDelete = useCallback(() => {
    if (confirmDelete) {
      deleteMutation.mutate(confirmDelete);
    }
  }, [confirmDelete, deleteMutation]);

  const handleReindex = useCallback(
    (name: string) => {
      setActionMessage(null);
      rebuildMutation.mutate(name);
    },
    [rebuildMutation]
  );

  const handleSearch = useCallback(async () => {
    if (!searchQuery.trim() || !searchCollection) return;
    setSearchLoading(true);
    setSearchError(null);
    setSearchResults(null);
    setExpandedResult(null);
    try {
      const result = await adminClient.searchQdrantCollection(
        searchCollection,
        searchQuery.trim(),
        Number(searchLimit)
      );
      setSearchResults(result.results);
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setSearchLoading(false);
    }
  }, [searchQuery, searchCollection, searchLimit]);

  const collections = data?.collections ?? [];
  const totalVectors = collections.reduce((sum, c) => sum + c.vectorCount, 0);
  const avgHealth =
    collections.length > 0
      ? Math.round(collections.reduce((sum, c) => sum + c.health, 0) / collections.length)
      : 0;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Vector Collections
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Manage your knowledge base vector stores
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

      {/* Delete Confirmation */}
      {confirmDelete && (
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 rounded-lg p-4 flex items-center gap-3">
          <AlertCircleIcon className="h-5 w-5 text-red-500 shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-red-800 dark:text-red-200">
              Delete collection &quot;{confirmDelete}&quot;?
            </p>
            <p className="text-xs text-red-600 dark:text-red-400 mt-1">
              This will permanently remove all vectors. This action cannot be undone.
            </p>
          </div>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setConfirmDelete(null)}
              disabled={deleteMutation.isPending}
            >
              Cancel
            </Button>
            <Button
              variant="destructive"
              size="sm"
              onClick={handleConfirmDelete}
              disabled={deleteMutation.isPending}
            >
              {deleteMutation.isPending ? 'Deleting...' : 'Delete'}
            </Button>
          </div>
        </div>
      )}

      {/* Error State */}
      {error && (
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 rounded-lg p-4 flex items-center gap-3">
          <AlertCircleIcon className="h-5 w-5 text-red-500 shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-red-800 dark:text-red-200">
              Failed to load vector collections
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
            <div className="text-sm text-gray-600 dark:text-zinc-400">Total Collections</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">
              {collections.length}
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
              {collections.length > 0 ? collections[0].dimensions : '\u2014'}
            </div>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Avg Health</div>
            <div
              className={`text-2xl font-bold ${avgHealth >= 90 ? 'text-green-600 dark:text-green-400' : avgHealth >= 70 ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400'}`}
            >
              {collections.length > 0 ? `${avgHealth}%` : '\u2014'}
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
          Run a semantic similarity search against any vector collection using the embedding model.
        </p>

        <div className="flex flex-col sm:flex-row gap-3">
          {/* Collection selector */}
          <Select value={searchCollection} onValueChange={setSearchCollection}>
            <SelectTrigger className="w-full sm:w-[200px] bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
              <SelectValue placeholder="Select collection" />
            </SelectTrigger>
            <SelectContent>
              {collections.map(c => (
                <SelectItem key={c.name} value={c.name}>
                  {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

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

          {/* Limit selector */}
          <Select value={searchLimit} onValueChange={setSearchLimit}>
            <SelectTrigger className="w-[100px] bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {['5', '10', '20', '50'].map(v => (
                <SelectItem key={v} value={v}>
                  Top {v}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button
            onClick={handleSearch}
            disabled={searchLoading || !searchQuery.trim() || !searchCollection}
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
                key={item.id}
                className="bg-slate-50/70 dark:bg-zinc-900/50 rounded-lg border border-slate-200/50 dark:border-zinc-700/50 overflow-hidden"
              >
                <button
                  type="button"
                  onClick={() => setExpandedResult(expandedResult === item.id ? null : item.id)}
                  className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-slate-100/60 dark:hover:bg-zinc-800/60 transition-colors"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <span className="text-xs font-mono text-muted-foreground shrink-0">
                      #{item.id}
                    </span>
                    <span className="text-sm text-foreground truncate">
                      {item.payload?.text ??
                        item.payload?.content ??
                        item.payload?.chunk ??
                        'No text payload'}
                    </span>
                  </div>
                  <div className="flex items-center gap-2 shrink-0 ml-2">
                    <span
                      className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
                        item.score >= 0.8
                          ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300'
                          : item.score >= 0.5
                            ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300'
                            : 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300'
                      }`}
                    >
                      {(item.score * 100).toFixed(1)}%
                    </span>
                    {expandedResult === item.id ? (
                      <ChevronUpIcon className="h-4 w-4 text-muted-foreground" />
                    ) : (
                      <ChevronDownIcon className="h-4 w-4 text-muted-foreground" />
                    )}
                  </div>
                </button>

                {expandedResult === item.id && item.payload && (
                  <div className="px-4 py-3 border-t border-slate-200/50 dark:border-zinc-700/50 bg-white/50 dark:bg-zinc-900/30">
                    <div className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">
                      Payload
                    </div>
                    <div className="space-y-1">
                      {Object.entries(item.payload).map(([key, value]) => (
                        <div key={key} className="flex gap-2 text-xs">
                          <span className="text-muted-foreground font-mono shrink-0">{key}:</span>
                          <span className="text-foreground break-all">{value}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Collections */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {Array.from({ length: 3 }).map((_, i) => (
            <CardSkeleton key={i} />
          ))}
        </div>
      ) : collections.length > 0 ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {collections.map(collection => (
            <VectorCollectionCard
              key={collection.name}
              name={collection.name}
              vectorCount={collection.vectorCount}
              dimensions={collection.dimensions}
              storage={collection.storage}
              health={collection.health}
              onReindex={handleReindex}
              onDelete={handleDelete}
            />
          ))}
        </div>
      ) : !error ? (
        <div className="text-center py-12 bg-white/50 dark:bg-zinc-800/50 backdrop-blur-sm rounded-xl border border-slate-200/40 dark:border-zinc-700/30">
          <p className="text-muted-foreground">No vector collections found</p>
          <p className="text-xs text-muted-foreground mt-1">
            Collections will appear here once documents are processed
          </p>
        </div>
      ) : null}
    </div>
  );
}
