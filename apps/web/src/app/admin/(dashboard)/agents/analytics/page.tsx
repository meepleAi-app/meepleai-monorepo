'use client';

/**
 * Agent Analytics Page
 *
 * Displays real agent metrics from the API: KPI cards, usage chart,
 * cost breakdown, top agents table, and top queries.
 * Pattern adapted from (authenticated)/admin/agents/metrics/client.tsx.
 */

import { useState, useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';
import { format, subDays } from 'date-fns';
import { Activity, Target, BarChart3, TrendingUp, RefreshCw } from 'lucide-react';

import type { AgentMetrics, TopAgent } from '@/app/(authenticated)/admin/agents/metrics/client';
import { CostBreakdownChart } from '@/components/admin/agents/CostBreakdownChart';
import { MetricsKpiCards } from '@/components/admin/agents/MetricsKpiCards';
import { TopAgentsTable } from '@/components/admin/agents/TopAgentsTable';
import { UsageChart } from '@/components/admin/agents/UsageChart';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';

// ============================================================================
// Types
// ============================================================================

type DateRange = '7d' | '30d' | '90d';

// ============================================================================
// API Functions
// ============================================================================

async function fetchAgentMetrics(startDate: string, endDate: string): Promise<AgentMetrics> {
  const params = new URLSearchParams({ startDate, endDate });
  const response = await fetch(`/api/v1/admin/agents/metrics?${params}`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error('Failed to fetch agent metrics');
  }

  return response.json();
}

async function fetchTopAgents(
  limit: number,
  sortBy: string,
  startDate: string,
  endDate: string
): Promise<TopAgent[]> {
  const params = new URLSearchParams({
    limit: limit.toString(),
    sortBy,
    startDate,
    endDate,
  });
  const response = await fetch(`/api/v1/admin/agents/metrics/top?${params}`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error('Failed to fetch top agents');
  }

  return response.json();
}

// ============================================================================
// Component
// ============================================================================

export default function AgentAnalyticsPage() {
  const [dateRange, setDateRange] = useState<DateRange>('7d');
  const [sortBy, setSortBy] = useState<'invocations' | 'cost' | 'confidence'>('invocations');

  // Calculate date range
  const { startDate, endDate } = useMemo(() => {
    const end = new Date();
    const days = dateRange === '7d' ? 7 : dateRange === '90d' ? 90 : 30;
    const start = subDays(end, days);
    return {
      startDate: format(start, 'yyyy-MM-dd'),
      endDate: format(end, 'yyyy-MM-dd'),
    };
  }, [dateRange]);

  // Fetch metrics
  const {
    data: metrics,
    isLoading: metricsLoading,
    error: metricsError,
    refetch: refetchMetrics,
  } = useQuery({
    queryKey: ['agentMetrics', startDate, endDate],
    queryFn: () => fetchAgentMetrics(startDate, endDate),
    staleTime: 60_000,
  });

  // Fetch top agents
  const {
    data: topAgents,
    isLoading: topAgentsLoading,
    error: topAgentsError,
    refetch: refetchTopAgents,
  } = useQuery({
    queryKey: ['topAgents', sortBy, startDate, endDate],
    queryFn: () => fetchTopAgents(10, sortBy, startDate, endDate),
    staleTime: 60_000,
  });

  const handleRefresh = () => {
    refetchMetrics();
    refetchTopAgents();
  };

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
        <div className="flex items-center gap-2">
          {(['7d', '30d', '90d'] as const).map(range => (
            <button
              key={range}
              onClick={() => setDateRange(range)}
              className={`px-4 py-2 rounded-lg font-semibold transition-all ${
                dateRange === range
                  ? 'bg-amber-100 dark:bg-amber-900/30 text-amber-900 dark:text-amber-300'
                  : 'bg-white/80 dark:bg-zinc-800/80 hover:bg-white dark:hover:bg-zinc-800 text-gray-700 dark:text-zinc-300 font-medium'
              }`}
            >
              {range}
            </button>
          ))}
          <Button variant="outline" size="icon" onClick={handleRefresh}>
            <RefreshCw className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Error State */}
      {(metricsError || topAgentsError) && (
        <Card className="border-destructive">
          <CardContent className="py-4">
            <p className="text-destructive">Failed to load metrics. Please try again.</p>
          </CardContent>
        </Card>
      )}

      {/* KPI Cards */}
      <MetricsKpiCards metrics={metrics} isLoading={metricsLoading} />

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Usage Over Time */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm font-semibold">
              <TrendingUp className="h-4 w-4 text-blue-500" />
              Usage Over Time
            </CardTitle>
          </CardHeader>
          <CardContent>
            {metricsLoading ? (
              <Skeleton className="h-64 w-full" />
            ) : metrics?.usageOverTime ? (
              <UsageChart data={metrics.usageOverTime} />
            ) : (
              <div className="flex h-64 items-center justify-center text-muted-foreground">
                No data available
              </div>
            )}
          </CardContent>
        </Card>

        {/* Cost Breakdown */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm font-semibold">
              <BarChart3 className="h-4 w-4 text-emerald-500" />
              Cost Breakdown by Model
            </CardTitle>
          </CardHeader>
          <CardContent>
            {metricsLoading ? (
              <Skeleton className="h-64 w-full" />
            ) : metrics?.costBreakdown ? (
              <CostBreakdownChart data={metrics.costBreakdown} />
            ) : (
              <div className="flex h-64 items-center justify-center text-muted-foreground">
                No data available
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Top Agents Table */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-sm font-semibold">
            <Activity className="h-4 w-4 text-purple-500" />
            Top Agents
          </CardTitle>
          <Select value={sortBy} onValueChange={(v: string) => setSortBy(v as typeof sortBy)}>
            <SelectTrigger className="w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="invocations">By Usage</SelectItem>
              <SelectItem value="cost">By Cost</SelectItem>
              <SelectItem value="confidence">By Confidence</SelectItem>
            </SelectContent>
          </Select>
        </CardHeader>
        <CardContent>
          {topAgentsLoading ? (
            <div className="space-y-3">
              {[1, 2, 3].map(i => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : topAgents && topAgents.length > 0 ? (
            <TopAgentsTable agents={topAgents} />
          ) : (
            <div className="flex h-32 items-center justify-center text-muted-foreground">
              No agents found
            </div>
          )}
        </CardContent>
      </Card>

      {/* Top Queries */}
      {metrics?.topQueries && metrics.topQueries.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm font-semibold">
              <Target className="h-4 w-4 text-amber-500" />
              Top Queries
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {metrics.topQueries.map((query, idx) => (
                <div key={idx} className="flex items-center justify-between rounded-lg border p-3">
                  <span className="truncate text-sm">{query.query}</span>
                  <Badge variant="secondary">{query.count}</Badge>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
