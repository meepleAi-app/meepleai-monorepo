'use client';

/**
 * KPI cards for the admin usage page.
 * Issue #5078: Admin usage page — KPI cards (spend, requests, RPM, balance).
 *
 * Displays four key metrics from OpenRouter status:
 * Spend Today, Requests Today, RPM (with utilization), and Balance.
 */

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { OpenRouterStatusDto } from '@/lib/api/schemas/admin-knowledge-base.schemas';

// ─── Types ───────────────────────────────────────────────────────────────────

interface KpiCardsProps {
  status: OpenRouterStatusDto | null | undefined;
  isLoading?: boolean;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatSpend(value: number): string {
  // Use 4 decimal places to preserve small values (e.g. $0.0025)
  return `$${value.toFixed(4)}`;
}

function formatBalance(value: number): string {
  return `$${value.toFixed(2)}`;
}

// ─── Component ───────────────────────────────────────────────────────────────

export function KpiCards({ status, isLoading }: KpiCardsProps) {
  if (isLoading) {
    return (
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Card key={i}>
            <CardHeader className="pb-2">
              <Skeleton className="h-4 w-24" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-8 w-16" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      {/* Spend Today */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">Spend Today</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">
            {status ? formatSpend(status.dailySpendUsd) : '—'}
          </div>
        </CardContent>
      </Card>

      {/* Requests Today */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">Requests Today</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">
            {status ? String(status.todayRequestCount) : '—'}
          </div>
        </CardContent>
      </Card>

      {/* RPM */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">RPM</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-baseline gap-1">
            <span className="text-2xl font-bold">
              {status ? String(status.currentRpm) : '—'}
            </span>
            {status && (
              <span className="text-sm text-muted-foreground">/ {status.limitRpm}</span>
            )}
          </div>
          {status && (
            <p className="text-xs text-muted-foreground mt-1">
              {(status.utilizationPercent * 100).toFixed(1)}% utilization
            </p>
          )}
          {status?.isThrottled && (
            <Badge variant="destructive" className="mt-1">Throttled</Badge>
          )}
        </CardContent>
      </Card>

      {/* Balance */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">Balance</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">
            {status ? formatBalance(status.balanceUsd) : '—'}
          </div>
          {status?.isFreeTier && (
            <Badge variant="secondary" className="mt-1">Free Tier</Badge>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
