'use client';

/**
 * MetricsKpiCards Component
 * Issue #3382: Agent Metrics Dashboard
 *
 * Displays 4 KPI cards for key agent metrics:
 * - Total Invocations
 * - Average Latency
 * - Total Cost
 * - Average Confidence
 */

import { Activity, Clock, DollarSign, Target } from 'lucide-react';

import type { AgentMetrics } from '@/app/(authenticated)/admin/agents/metrics/client';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';


// ============================================================================
// Types
// ============================================================================

interface MetricsKpiCardsProps {
  metrics?: AgentMetrics;
  isLoading: boolean;
}

interface KpiItemProps {
  icon: React.ElementType;
  iconColor: string;
  label: string;
  value: string;
  subValue?: string;
}

// ============================================================================
// Sub-Components
// ============================================================================

function KpiItem({ icon: Icon, iconColor, label, value, subValue }: KpiItemProps) {
  return (
    <Card className="relative overflow-hidden">
      <CardContent className="p-4">
        <div className="flex items-start gap-3">
          <div
            className={cn(
              'flex h-10 w-10 shrink-0 items-center justify-center rounded-xl',
              iconColor
            )}
          >
            <Icon className="h-5 w-5 text-white" />
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-2xl font-bold tracking-tight">{value}</p>
            <p className="text-sm text-muted-foreground">{label}</p>
            {subValue && (
              <p className="mt-1 text-xs text-muted-foreground">{subValue}</p>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function KpiSkeleton() {
  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex items-start gap-3">
          <Skeleton className="h-10 w-10 rounded-xl" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-7 w-20" />
            <Skeleton className="h-4 w-24" />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================================================
// Component
// ============================================================================

export function MetricsKpiCards({ metrics, isLoading }: MetricsKpiCardsProps) {
  if (isLoading) {
    return (
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <KpiSkeleton />
        <KpiSkeleton />
        <KpiSkeleton />
        <KpiSkeleton />
      </div>
    );
  }

  const formatNumber = (num: number): string => {
    if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(1)}M`;
    if (num >= 1_000) return `${(num / 1_000).toFixed(1)}K`;
    return num.toLocaleString();
  };

  const formatCost = (cost: number): string => {
    if (cost >= 1000) return `$${(cost / 1000).toFixed(2)}K`;
    if (cost >= 1) return `$${cost.toFixed(2)}`;
    return `$${cost.toFixed(4)}`;
  };

  const formatLatency = (ms: number): string => {
    if (ms >= 1000) return `${(ms / 1000).toFixed(2)}s`;
    return `${Math.round(ms)}ms`;
  };

  const formatConfidence = (score: number): string => {
    return `${(score * 100).toFixed(0)}%`;
  };

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <KpiItem
        icon={Activity}
        iconColor="bg-blue-500"
        label="Total Invocations"
        value={formatNumber(metrics?.totalInvocations ?? 0)}
        subValue={metrics?.totalTokensUsed ? `${formatNumber(metrics.totalTokensUsed)} tokens` : undefined}
      />

      <KpiItem
        icon={Clock}
        iconColor="bg-amber-500"
        label="Avg Latency"
        value={formatLatency(metrics?.avgLatencyMs ?? 0)}
        subValue="Response time"
      />

      <KpiItem
        icon={DollarSign}
        iconColor="bg-emerald-500"
        label="Total Cost"
        value={formatCost(metrics?.totalCost ?? 0)}
        subValue="This period"
      />

      <KpiItem
        icon={Target}
        iconColor="bg-purple-500"
        label="Avg Confidence"
        value={formatConfidence(metrics?.avgConfidenceScore ?? 0)}
        subValue="Response accuracy"
      />
    </div>
  );
}
