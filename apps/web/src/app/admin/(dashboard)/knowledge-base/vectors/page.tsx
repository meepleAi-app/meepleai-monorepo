'use client';

import { useCallback, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertCircleIcon, RefreshCwIcon } from 'lucide-react';

import { VectorCollectionCard } from '@/components/admin/knowledge-base/vector-collection-card';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
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
  const [actionMessage, setActionMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const {
    data,
    isLoading,
    error,
    refetch,
    isRefetching,
  } = useQuery({
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
      setActionMessage({ type: 'error', text: `Failed to delete "${name}": ${err instanceof Error ? err.message : 'Unknown error'}` });
      setConfirmDelete(null);
    },
  });

  const rebuildMutation = useMutation({
    mutationFn: (name: string) => adminClient.rebuildQdrantIndex(name),
    onSuccess: (_data, name) => {
      setActionMessage({ type: 'success', text: `Rebuild triggered for "${name}".` });
    },
    onError: (err, name) => {
      setActionMessage({ type: 'error', text: `Failed to rebuild "${name}": ${err instanceof Error ? err.message : 'Unknown error'}` });
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

  const handleReindex = useCallback((name: string) => {
    setActionMessage(null);
    rebuildMutation.mutate(name);
  }, [rebuildMutation]);

  const collections = data?.collections ?? [];
  const totalVectors = collections.reduce((sum, c) => sum + c.vectorCount, 0);
  const avgHealth = collections.length > 0
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
        <div className={`rounded-lg p-3 flex items-center gap-3 text-sm ${
          actionMessage.type === 'success'
            ? 'bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800/40 text-green-800 dark:text-green-200'
            : 'bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 text-red-800 dark:text-red-200'
        }`}>
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
          {Array.from({ length: 4 }).map((_, i) => <StatSkeleton key={i} />)}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Total Collections</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">{collections.length}</div>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Total Vectors</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">{totalVectors.toLocaleString()}</div>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Dimensions</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">
              {collections.length > 0 ? collections[0].dimensions : '\u2014'}
            </div>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Avg Health</div>
            <div className={`text-2xl font-bold ${avgHealth >= 90 ? 'text-green-600 dark:text-green-400' : avgHealth >= 70 ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400'}`}>
              {collections.length > 0 ? `${avgHealth}%` : '\u2014'}
            </div>
          </div>
        </div>
      )}

      {/* Collections */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : collections.length > 0 ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {collections.map((collection) => (
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
          <p className="text-xs text-muted-foreground mt-1">Collections will appear here once documents are processed</p>
        </div>
      ) : null}
    </div>
  );
}
