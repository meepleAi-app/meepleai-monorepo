import { Suspense } from 'react';

import { type Metadata } from 'next';

import { SharedGamesBlock } from '@/components/admin/dashboard/shared-games-block';
import { StatsOverview } from '@/components/admin/dashboard/stats-overview';
import { UserManagementBlock } from '@/components/admin/dashboard/user-management-block';

export const metadata: Metadata = {
  title: 'Overview',
  description: 'Admin dashboard overview with platform stats and quick actions',
};

function CardSkeleton({ height = 'h-[180px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function OverviewPage() {
  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Dashboard Overview
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Platform stats, approvals, and user management at a glance
        </p>
      </div>

      {/* Stats */}
      <Suspense
        fallback={
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {Array.from({ length: 4 }).map((_, i) => (
              <CardSkeleton key={i} />
            ))}
          </div>
        }
      >
        <StatsOverview />
      </Suspense>

      {/* Approval Queue */}
      <Suspense fallback={<CardSkeleton height="h-[400px]" />}>
        <SharedGamesBlock />
      </Suspense>

      {/* User Management */}
      <Suspense fallback={<CardSkeleton height="h-[400px]" />}>
        <UserManagementBlock />
      </Suspense>
    </div>
  );
}
