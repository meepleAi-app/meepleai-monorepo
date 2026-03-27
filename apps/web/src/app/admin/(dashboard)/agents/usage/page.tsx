'use client';

/**
 * Admin — OpenRouter Usage & Costs page
 * Issues #5077-#5083: KPI cards, timeline chart, cost breakdown,
 * rate limit gauge, free quota indicator, and recent requests table.
 * Tabs: OpenRouter | Token Balance | Chat Log
 */

import { Suspense, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, RefreshCw } from 'lucide-react';
import { useSearchParams } from 'next/navigation';

import { ChatHistoryFilters } from '@/components/admin/agents/chat-history-filters';
import { ChatHistoryTable } from '@/components/admin/agents/chat-history-table';
import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';
import { CostBreakdownPanel } from '@/components/admin/usage/CostBreakdownPanel';
import { FreeQuotaIndicator } from '@/components/admin/usage/FreeQuotaIndicator';
import { KpiCards } from '@/components/admin/usage/KpiCards';
import { RateLimitGauge } from '@/components/admin/usage/RateLimitGauge';
import {
  RecentRequestsTable,
  type RecentRequestsFilters,
} from '@/components/admin/usage/RecentRequestsTable';
import { RequestTimelineChart } from '@/components/admin/usage/RequestTimelineChart';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { isNotFoundError } from '@/lib/api/core/errors';
import { HttpClient } from '@/lib/api/core/httpClient';

import { TokenBalanceTab } from './token-balance-tab';

// ─── Module-level client (stable reference, avoids re-creation on every render) ─

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

// ─── Page ────────────────────────────────────────────────────────────────────

export default function UsagePage() {
  // ── Tab deep-link ──
  const searchParams = useSearchParams();
  const defaultTab = searchParams.get('tab') ?? 'openrouter';

  // ── Period state ──
  const [timelinePeriod, setTimelinePeriod] = useState<'24h' | '7d' | '30d'>('24h');
  const [costPeriod, setCostPeriod] = useState<'1d' | '7d' | '30d'>('7d');

  // ── Filter state for requests table ──
  const [requestFilters, setRequestFilters] = useState<RecentRequestsFilters>({
    page: 1,
    pageSize: 20,
  });

  // ── Queries ──
  const {
    data: status,
    isLoading: statusLoading,
    isError,
    error,
    refetch,
    dataUpdatedAt,
  } = useQuery({
    queryKey: ['admin', 'openrouter', 'status'],
    queryFn: () => adminClient.getOpenRouterStatus(),
    refetchInterval: 30_000,
    retry: (failureCount, err) => {
      if (isNotFoundError(err)) return false;
      return failureCount < 3;
    },
  });

  const { data: timeline, isLoading: timelineLoading } = useQuery({
    queryKey: ['admin', 'usage', 'timeline', timelinePeriod],
    queryFn: () => adminClient.getUsageTimeline(timelinePeriod),
    refetchInterval: 60_000,
  });

  const { data: costs, isLoading: costsLoading } = useQuery({
    queryKey: ['admin', 'usage', 'costs', costPeriod],
    queryFn: () => adminClient.getUsageCosts(costPeriod),
    refetchInterval: 60_000,
  });

  const { data: freeQuota, isLoading: freeQuotaLoading } = useQuery({
    queryKey: ['admin', 'usage', 'free-quota'],
    queryFn: () => adminClient.getUsageFreeQuota(),
    refetchInterval: 60_000,
  });

  const { data: recentRequests, isLoading: requestsLoading } = useQuery({
    queryKey: ['admin', 'usage', 'requests', requestFilters],
    queryFn: () =>
      adminClient.getRecentRequests({
        source: requestFilters.source,
        model: requestFilters.model,
        successOnly: requestFilters.successOnly,
        page: requestFilters.page,
        pageSize: requestFilters.pageSize,
      }),
    refetchInterval: 30_000,
  });

  return (
    <div className="space-y-6 p-6">
      {/* ── Header ── */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold font-quicksand">Utilizzo &amp; Costi</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Stato account OpenRouter in tempo reale, utilizzo del rate limit e spesa giornaliera.
          </p>
        </div>
        <div className="flex items-center gap-3">
          {dataUpdatedAt > 0 && (
            <span className="text-xs text-muted-foreground">
              Updated {new Date(dataUpdatedAt).toLocaleTimeString()}
            </span>
          )}
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={statusLoading}
            aria-label="Refresh usage data"
          >
            <RefreshCw className={`h-4 w-4 mr-2 ${statusLoading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* ── Tabs ── */}
      <Tabs defaultValue={defaultTab}>
        <TabsList>
          <TabsTrigger value="openrouter">OpenRouter</TabsTrigger>
          <TabsTrigger value="token-balance">Token Balance</TabsTrigger>
          <TabsTrigger value="chat-log">Chat Log</TabsTrigger>
        </TabsList>

        {/* ── OpenRouter tab (existing content) ── */}
        <TabsContent value="openrouter" className="space-y-8 mt-4">
          {/* ── 404 fallback — endpoint not implemented ── */}
          {isNotFoundError(error) && (
            <EmptyFeatureState
              title="Funzionalità non disponibile"
              description="Endpoint usage & costs non ancora implementato nel backend."
            />
          )}

          {/* ── Error banner ── */}
          {isError && !isNotFoundError(error) && (
            <div
              role="alert"
              className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive"
            >
              <AlertTriangle className="h-4 w-4 shrink-0" />
              <span>
                Failed to load usage data:{' '}
                {error instanceof Error ? error.message : 'Unknown error'}
              </span>
            </div>
          )}

          {/* ── Overview KPIs ── */}
          <section id="overview" aria-label="Overview KPIs">
            <h2 className="text-lg font-medium font-quicksand mb-4">Overview</h2>
            <KpiCards status={status} isLoading={statusLoading} />
          </section>

          {/* ── Charts ── */}
          <section id="costs" aria-label="Charts">
            <h2 className="text-lg font-medium font-quicksand mb-4">Charts</h2>
            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
              <RequestTimelineChart
                data={timeline}
                period={timelinePeriod}
                onPeriodChange={setTimelinePeriod}
                isLoading={timelineLoading}
              />
              <CostBreakdownPanel
                data={costs}
                period={costPeriod}
                onPeriodChange={setCostPeriod}
                isLoading={costsLoading}
              />
            </div>
          </section>

          {/* ── Rate Limits ── */}
          <section id="rate-limits" aria-label="Rate Limits">
            <h2 className="text-lg font-medium font-quicksand mb-4">Rate Limits</h2>
            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
              <RateLimitGauge status={status} isLoading={statusLoading} />
              <FreeQuotaIndicator data={freeQuota} isLoading={freeQuotaLoading} />
            </div>
          </section>

          {/* ── Recent Requests ── */}
          <section id="free-quota" aria-label="Recent Requests">
            <h2 className="text-lg font-medium font-quicksand mb-4">Recent Requests</h2>
            <RecentRequestsTable
              data={recentRequests}
              filters={requestFilters}
              onFiltersChange={setRequestFilters}
              isLoading={requestsLoading}
            />
          </section>
        </TabsContent>

        {/* ── Token Balance tab ── */}
        <TabsContent value="token-balance" className="mt-4">
          <TokenBalanceTab />
        </TabsContent>

        {/* ── Chat Log tab ── */}
        <TabsContent value="chat-log" className="mt-4">
          <div className="space-y-6">
            <Suspense fallback={<div className="h-28 animate-pulse rounded-xl bg-muted" />}>
              <ChatHistoryFilters />
            </Suspense>
            <Suspense fallback={<div className="h-[600px] animate-pulse rounded-xl bg-muted" />}>
              <ChatHistoryTable />
            </Suspense>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
