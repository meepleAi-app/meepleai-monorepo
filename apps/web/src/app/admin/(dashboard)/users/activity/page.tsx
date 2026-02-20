import { Suspense } from 'react';

import { type Metadata } from 'next';

import { ActivityFilters } from '@/components/admin/users/activity-filters';
import { ActivityTable } from '@/components/admin/users/activity-table';

export const metadata: Metadata = {
  title: 'Activity Log',
  description: 'Monitor user actions and system events',
};

function CardSkeleton({ height = 'h-[400px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function UserActivityPage() {
  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          User Activity Log
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Monitor user actions and system events
        </p>
      </div>

      {/* Filters */}
      <Suspense fallback={<CardSkeleton height="h-[120px]" />}>
        <ActivityFilters />
      </Suspense>

      {/* Activity Table */}
      <Suspense fallback={<CardSkeleton height="h-[600px]" />}>
        <ActivityTable />
      </Suspense>
    </div>
  );
}
