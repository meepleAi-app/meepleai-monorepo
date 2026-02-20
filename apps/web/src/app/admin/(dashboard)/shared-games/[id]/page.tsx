/**
 * Game Detail Page - New Admin Dashboard
 *
 * Route: /admin/shared-games/[id]
 * Displays game details with two tabs: Details and Documents (PDF management).
 */

import { Suspense } from 'react';

import { type Metadata } from 'next';

import { Skeleton } from '@/components/ui/feedback/skeleton';

import { GameDetailClient } from './client';

interface GameDetailPageProps {
  params: Promise<{ id: string }>;
}

export const metadata: Metadata = {
  title: 'Game Detail',
  description: 'View and manage a shared game',
};

function LoadingSkeleton() {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Skeleton className="h-9 w-9 rounded-md" />
        <div className="space-y-2">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-32" />
        </div>
      </div>
      <Skeleton className="h-10 w-80" />
      <div className="grid gap-6 md:grid-cols-3">
        <div className="md:col-span-2 space-y-4">
          <Skeleton className="h-40 w-full rounded-xl" />
          <Skeleton className="h-24 w-full rounded-xl" />
        </div>
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
    </div>
  );
}

export default function GameDetailPage({ params }: GameDetailPageProps) {
  return (
    <Suspense fallback={<LoadingSkeleton />}>
      <GameDetailClient params={params} />
    </Suspense>
  );
}
