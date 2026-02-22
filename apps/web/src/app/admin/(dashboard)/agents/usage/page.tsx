'use client';

/**
 * Admin — OpenRouter Usage & Costs page
 * Issue #5077: layout, KPI cards, and navigation.
 *
 * KPI cards are populated from GET /api/v1/admin/openrouter/status.
 * Charts and table sections are placeholders for issues #5078-#5083.
 */

import { useEffect } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Activity, AlertTriangle, DollarSign, RefreshCw, TrendingUp, Zap } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useSetNavConfig } from '@/hooks/useSetNavConfig';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

// ─── Module-level client (stable reference, avoids re-creation on every render) ─

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatUsd(value: number): string {
  return `$${value.toFixed(4)}`;
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`;
}

// ─── Skeleton ────────────────────────────────────────────────────────────────

function KpiSkeleton() {
  return (
    <Card>
      <CardHeader className="pb-2">
        <Skeleton className="h-4 w-32" />
      </CardHeader>
      <CardContent>
        <Skeleton className="h-8 w-24 mb-1" />
        <Skeleton className="h-3 w-40" />
      </CardContent>
    </Card>
  );
}

// ─── Page ────────────────────────────────────────────────────────────────────

export default function UsagePage() {
  const setNavConfig = useSetNavConfig();

  // Register MiniNav tabs for this section.
  // Hash-based sub-section tabs (#costs, #rate-limits, #free-quota) are omitted
  // because MiniNav's isTabActive matches on pathname, not URL fragments.
  // Dedicated sub-route tabs will be added in issues #5078-#5083.
  useEffect(() => {
    setNavConfig({
      miniNav: [{ id: 'overview', label: 'Overview', href: '/admin/agents/usage' }],
      actionBar: [],
    });
  }, [setNavConfig]);

  const {
    data: status,
    isLoading,
    isError,
    error,
    refetch,
    dataUpdatedAt,
  } = useQuery({
    queryKey: ['admin', 'openrouter', 'status'],
    queryFn: () => adminClient.getOpenRouterStatus(),
    refetchInterval: 30_000, // refresh every 30 s
  });

  return (
    <div className="space-y-8 p-6">
      {/* ── Header ── */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold font-quicksand">Usage &amp; Costs</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Real-time OpenRouter account status, rate-limit utilization, and daily spend.
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
            disabled={isLoading}
            aria-label="Refresh usage data"
          >
            <RefreshCw className={`h-4 w-4 mr-2 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* ── Error banner ── */}
      {isError && (
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
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {/* Today's spend */}
          {isLoading ? (
            <KpiSkeleton />
          ) : (
            <Card>
              <CardHeader className="pb-2 flex flex-row items-center justify-between">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Spend Today
                </CardTitle>
                <DollarSign className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-bold">
                  {status ? formatUsd(status.dailySpendUsd) : '—'}
                </p>
                <p className="text-xs text-muted-foreground mt-1">
                  Balance: {status ? formatUsd(status.balanceUsd) : '—'}
                </p>
              </CardContent>
            </Card>
          )}

          {/* Total requests today */}
          {isLoading ? (
            <KpiSkeleton />
          ) : (
            <Card>
              <CardHeader className="pb-2 flex flex-row items-center justify-between">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Requests Today
                </CardTitle>
                <Activity className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-bold">
                  {status?.todayRequestCount.toLocaleString() ?? '—'}
                </p>
                <p className="text-xs text-muted-foreground mt-1">LLM requests (UTC day)</p>
              </CardContent>
            </Card>
          )}

          {/* RPM utilization */}
          {isLoading ? (
            <KpiSkeleton />
          ) : (
            <Card>
              <CardHeader className="pb-2 flex flex-row items-center justify-between">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  RPM
                </CardTitle>
                <Zap className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="flex items-baseline gap-2">
                  <p className="text-2xl font-bold">{status?.currentRpm ?? '—'}</p>
                  {status && status.limitRpm > 0 && (
                    <span className="text-sm text-muted-foreground">/ {status.limitRpm}</span>
                  )}
                </div>
                <div className="flex items-center gap-2 mt-1">
                  {status && (
                    <>
                      <p className="text-xs text-muted-foreground">
                        {formatPercent(status.utilizationPercent)} utilization
                      </p>
                      {status.isThrottled && (
                        <Badge variant="destructive" className="text-xs px-1 py-0">
                          Throttled
                        </Badge>
                      )}
                    </>
                  )}
                </div>
              </CardContent>
            </Card>
          )}

          {/* Balance */}
          {isLoading ? (
            <KpiSkeleton />
          ) : (
            <Card>
              <CardHeader className="pb-2 flex flex-row items-center justify-between">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Balance
                </CardTitle>
                <TrendingUp className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-bold">
                  {status ? formatUsd(status.balanceUsd) : '—'}
                </p>
                <div className="flex items-center gap-2 mt-1">
                  <p className="text-xs text-muted-foreground">
                    {status?.rateLimitInterval
                      ? `Interval: ${status.rateLimitInterval}`
                      : 'Rate limit interval unknown'}
                  </p>
                  {status?.isFreeTier && (
                    <Badge variant="secondary" className="text-xs px-1 py-0">
                      Free Tier
                    </Badge>
                  )}
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </section>

      {/* ── Charts placeholder (#5078-#5082) ── */}
      <section id="costs" aria-label="Charts">
        <h2 className="text-lg font-medium font-quicksand mb-4">Charts</h2>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Card className="h-64 flex items-center justify-center text-muted-foreground text-sm">
            Timeline chart — Issue #5078
          </Card>
          <Card className="h-64 flex items-center justify-center text-muted-foreground text-sm">
            Cost breakdown chart — Issue #5079
          </Card>
        </div>
      </section>

      {/* ── Rate limits placeholder (#5080-#5081) ── */}
      <section id="rate-limits" aria-label="Rate Limits">
        <h2 className="text-lg font-medium font-quicksand mb-4">Rate Limits</h2>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Card className="h-48 flex items-center justify-center text-muted-foreground text-sm">
            Rate gauge — Issue #5080
          </Card>
          <Card className="h-48 flex items-center justify-center text-muted-foreground text-sm">
            Quota display — Issue #5081
          </Card>
        </div>
      </section>

      {/* ── Requests table placeholder (#5082) ── */}
      <section id="free-quota" aria-label="Recent Requests">
        <h2 className="text-lg font-medium font-quicksand mb-4">Recent Requests</h2>
        <Card className="h-48 flex items-center justify-center text-muted-foreground text-sm">
          Requests table — Issue #5082
        </Card>
      </section>
    </div>
  );
}
