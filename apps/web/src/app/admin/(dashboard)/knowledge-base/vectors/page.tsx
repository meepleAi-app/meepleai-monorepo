import { Suspense } from 'react';

import { type Metadata } from 'next';

import { PlusIcon } from 'lucide-react';

import { VectorCollectionCard } from '@/components/admin/knowledge-base/vector-collection-card';
import { Button } from '@/components/ui/button';

export const metadata: Metadata = {
  title: 'Vector Collections',
  description: 'Manage your knowledge base vector stores',
};

const MOCK_COLLECTIONS = [
  { name: 'Game Rules', vectorCount: 42500, dimensions: 384, storage: '3.2 GB', health: 98 },
  { name: 'Strategy Guides', vectorCount: 28300, dimensions: 384, storage: '2.1 GB', health: 95 },
  { name: 'FAQ Database', vectorCount: 9200, dimensions: 384, storage: '1.5 GB', health: 92 },
];

function CardSkeleton({ height = 'h-[200px]' }: { height?: string }) {
  return (
    <div className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`} />
  );
}

export default function VectorCollectionsPage() {
  const totalVectors = MOCK_COLLECTIONS.reduce((sum, c) => sum + c.vectorCount, 0);
  const avgHealth = Math.round(MOCK_COLLECTIONS.reduce((sum, c) => sum + c.health, 0) / MOCK_COLLECTIONS.length);

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
        <Button className="bg-amber-500 hover:bg-amber-600 text-white">
          <PlusIcon className="h-4 w-4 mr-2" />
          Create Collection
        </Button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
          <div className="text-sm text-gray-600 dark:text-zinc-400">Total Collections</div>
          <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">{MOCK_COLLECTIONS.length}</div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
          <div className="text-sm text-gray-600 dark:text-zinc-400">Total Vectors</div>
          <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">{totalVectors.toLocaleString()}</div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
          <div className="text-sm text-gray-600 dark:text-zinc-400">Storage Used</div>
          <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">6.8 GB</div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
          <div className="text-sm text-gray-600 dark:text-zinc-400">Avg Health</div>
          <div className="text-2xl font-bold text-green-600 dark:text-green-400">{avgHealth}%</div>
        </div>
      </div>

      {/* Collections */}
      <Suspense fallback={<div className="grid grid-cols-1 md:grid-cols-3 gap-6">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>}>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {MOCK_COLLECTIONS.map((collection) => (
            <VectorCollectionCard key={collection.name} {...collection} />
          ))}
        </div>
      </Suspense>
    </div>
  );
}
