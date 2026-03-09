/**
 * Play Records History Page
 *
 * Displays user's play history with filtering and search.
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { Plus } from 'lucide-react';
import dynamic from 'next/dynamic';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

const PlayHistory = dynamic(
  () => import('@/components/play-records/PlayHistory').then(m => ({ default: m.PlayHistory })),
  {
    ssr: false,
    loading: () => (
      <div className="grid gap-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-3">
        {[...Array(6)].map((_, i) => (
          <div key={i} className="h-48 bg-muted animate-pulse rounded-lg" />
        ))}
      </div>
    ),
  }
);

export default function PlayRecordsPage() {
  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Play History</h1>
          <p className="text-muted-foreground mt-1">Track and review your game sessions</p>
        </div>
        <Button asChild>
          <Link href="/play-records/new">
            <Plus className="w-4 h-4 mr-2" />
            New Session
          </Link>
        </Button>
      </div>

      {/* Play History Component */}
      <PlayHistory />
    </div>
  );
}
