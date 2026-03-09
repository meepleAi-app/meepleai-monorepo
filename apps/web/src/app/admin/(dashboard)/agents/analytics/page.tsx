import { Suspense } from 'react';

import { type Metadata } from 'next';

import { AgentKPICards } from '@/components/admin/agents/agent-kpi-cards';
import { CostBreakdownChart } from '@/components/admin/agents/cost-breakdown-chart';
import { TopQueriesTable } from '@/components/admin/agents/top-queries-table';
import { UsageTrendChart } from '@/components/admin/agents/usage-trend-chart';

export const metadata: Metadata = {
  title: 'Analytics',
  description: 'Monitor AI agent performance and costs',
};

function CardSkeleton({ height = 'h-[400px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function AgentAnalyticsPage() {
  return (
    <div className="space-y-6">
      {/* Page Header with Date Range */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Agent Analytics
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Monitor AI agent performance and costs
          </p>
        </div>
        <div className="flex gap-2">
          <button className="px-4 py-2 bg-amber-100 dark:bg-amber-900/30 text-amber-900 dark:text-amber-300 rounded-lg font-semibold hover:bg-amber-200 dark:hover:bg-amber-900/50 transition-all">
            7d
          </button>
          <button className="px-4 py-2 bg-white/80 dark:bg-zinc-800/80 hover:bg-white dark:hover:bg-zinc-800 text-gray-700 dark:text-zinc-300 rounded-lg font-medium transition-all">
            30d
          </button>
          <button className="px-4 py-2 bg-white/80 dark:bg-zinc-800/80 hover:bg-white dark:hover:bg-zinc-800 text-gray-700 dark:text-zinc-300 rounded-lg font-medium transition-all">
            90d
          </button>
        </div>
      </div>

      {/* KPI Cards */}
      <Suspense
        fallback={
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <CardSkeleton key={i} height="h-[100px]" />
            ))}
          </div>
        }
      >
        <AgentKPICards />
      </Suspense>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Suspense fallback={<CardSkeleton height="h-[300px]" />}>
          <UsageTrendChart />
        </Suspense>
        <Suspense fallback={<CardSkeleton height="h-[300px]" />}>
          <CostBreakdownChart />
        </Suspense>
      </div>

      {/* Top Queries Table */}
      <Suspense fallback={<CardSkeleton />}>
        <TopQueriesTable />
      </Suspense>
    </div>
  );
}
