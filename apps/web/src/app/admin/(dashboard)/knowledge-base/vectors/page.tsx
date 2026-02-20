'use client';

import { useQuery } from '@tanstack/react-query';
import { RefreshCwIcon, AlertCircleIcon } from 'lucide-react';

import { VectorCollectionCard, type VectorCollectionDto } from '@/components/admin/knowledge-base/vector-collection-card';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

function healthToStatus(health: number): 'healthy' | 'degraded' | 'error' {
  if (health >= 90) return 'healthy';
  if (health >= 70) return 'degraded';
  return 'error';
}

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
              {collections.length > 0 ? collections[0].dimensions : '—'}
            </div>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
            <div className="text-sm text-gray-600 dark:text-zinc-400">Avg Health</div>
            <div className={`text-2xl font-bold ${avgHealth >= 90 ? 'text-green-600 dark:text-green-400' : avgHealth >= 70 ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400'}`}>
              {collections.length > 0 ? `${avgHealth}%` : '—'}
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
              collection={{
                name: collection.name,
                vectorCount: collection.vectorCount,
                dimensions: collection.dimensions,
                storage: collection.storage,
                status: { health: healthToStatus(collection.health) },
              }}
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
