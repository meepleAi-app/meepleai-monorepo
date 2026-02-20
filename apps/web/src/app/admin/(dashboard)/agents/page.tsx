'use client';

/**
 * Agents Overview Page
 * Provides agent metrics dashboard with KPI cards, usage charts,
 * cost breakdown, and top agents table.
 * Reuses the MetricsClient component from the existing agents metrics page.
 */

import { useState, useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';
import { format, subDays } from 'date-fns';
import {
  Activity,
  Target,
  BarChart3,
  TrendingUp,
  RefreshCw,
  BotIcon,
} from 'lucide-react';
import Link from 'next/link';

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

// Types from existing agent metrics
interface AgentMetrics {
  totalInvocations: number;
  totalTokensUsed: number;
  totalCost: number;
  avgLatencyMs: number;
  avgConfidenceScore: number;
  userSatisfactionRate: number;
  topQueries: Array<{ query: string; count: number }>;
  costBreakdown: Array<{ category: string; cost: number; invocations: number; tokens: number }>;
  usageOverTime: Array<{ date: string; count: number; cost: number; tokens: number }>;
}

interface TopAgent {
  typologyId: string;
  typologyName: string;
  invocations: number;
  cost: number;
  avgConfidence: number;
  avgLatencyMs: number;
}

type DateRange = '7d' | '30d' | '90d';

async function fetchAgentMetrics(startDate: string, endDate: string): Promise<AgentMetrics> {
  const params = new URLSearchParams({ startDate, endDate });
  const response = await fetch(`/api/v1/admin/agents/metrics?${params}`, {
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to fetch agent metrics');
  return response.json();
}

async function fetchTopAgents(
  limit: number,
  sortBy: string,
  startDate: string,
  endDate: string
): Promise<TopAgent[]> {
  const params = new URLSearchParams({ limit: limit.toString(), sortBy, startDate, endDate });
  const response = await fetch(`/api/v1/admin/agents/metrics/top?${params}`, {
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to fetch top agents');
  return response.json();
}

export default function AgentsPage() {
  const [dateRange, setDateRange] = useState<DateRange>('30d');
  const [sortBy, setSortBy] = useState<'invocations' | 'cost' | 'confidence'>('invocations');

  const { startDate, endDate } = useMemo(() => {
    const end = new Date();
    const days = dateRange === '7d' ? 7 : dateRange === '90d' ? 90 : 30;
    const start = subDays(end, days);
    return {
      startDate: format(start, 'yyyy-MM-dd'),
      endDate: format(end, 'yyyy-MM-dd'),
    };
  }, [dateRange]);

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

  const {
    data: topAgents,
    isLoading: topAgentsLoading,
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
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            AI Agents
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Monitor agent usage, costs, and performance
          </p>
        </div>

        <div className="flex items-center gap-3">
          <Select value={dateRange} onValueChange={(v: string) => setDateRange(v as DateRange)}>
            <SelectTrigger className="w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="7d">Last 7 days</SelectItem>
              <SelectItem value="30d">Last 30 days</SelectItem>
              <SelectItem value="90d">Last 90 days</SelectItem>
            </SelectContent>
          </Select>

          <Button variant="outline" size="icon" onClick={handleRefresh}>
            <RefreshCw className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Quick Links */}
      <div className="flex flex-wrap gap-2">
        <Link href="/admin/agents/builder">
          <Button variant="outline" size="sm" className="gap-2">
            <BotIcon className="h-4 w-4" />
            Agent Builder
          </Button>
        </Link>
        <Link href="/admin/agents/pipeline">
          <Button variant="outline" size="sm" className="gap-2">
            Pipeline Explorer
          </Button>
        </Link>
        <Link href="/admin/agents/debug">
          <Button variant="outline" size="sm" className="gap-2">
            Debug Console
          </Button>
        </Link>
      </div>

      {/* Error State */}
      {metricsError && (
        <Card className="border-destructive">
          <CardContent className="py-4">
            <p className="text-destructive">
              Failed to load metrics. Please try again.
            </p>
          </CardContent>
        </Card>
      )}

      {/* KPI Cards */}
      <MetricsKpiCards metrics={metrics} isLoading={metricsLoading} />

      {/* Charts Row */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
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

        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
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
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
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
              {[1, 2, 3].map((i) => (
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
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm font-semibold">
              <Target className="h-4 w-4 text-amber-500" />
              Top Queries
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {metrics.topQueries.map((query, idx) => (
                <div
                  key={idx}
                  className="flex items-center justify-between rounded-lg border border-slate-200/60 dark:border-zinc-700/40 p-3"
                >
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
