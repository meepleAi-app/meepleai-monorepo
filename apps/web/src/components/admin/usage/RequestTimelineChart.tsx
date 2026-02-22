'use client';

/**
 * Request timeline area chart.
 * Issue #5078: Admin usage page — request timeline chart.
 *
 * Shows hourly (24h) or daily (7d/30d) request counts stacked by RequestSource.
 */

import {
  Area,
  CartesianGrid,
  ComposedChart,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import type { UsageTimelineDto } from '@/lib/api/schemas/admin-knowledge-base.schemas';

// ─── Types ───────────────────────────────────────────────────────────────────

export type TimelinePeriod = '24h' | '7d' | '30d';

interface RequestTimelineChartProps {
  data: UsageTimelineDto | null | undefined;
  period: TimelinePeriod;
  onPeriodChange: (period: TimelinePeriod) => void;
  isLoading?: boolean;
}

// ─── Constants ───────────────────────────────────────────────────────────────

const SOURCE_SERIES: { key: string; label: string; color: string }[] = [
  { key: 'manual', label: 'Manual', color: '#6366f1' },
  { key: 'ragPipeline', label: 'RAG Pipeline', color: '#f59e0b' },
  { key: 'eventDriven', label: 'Event-Driven', color: '#10b981' },
  { key: 'automatedTest', label: 'Automated Test', color: '#3b82f6' },
  { key: 'agentTask', label: 'Agent Task', color: '#ec4899' },
  { key: 'adminOperation', label: 'Admin', color: '#8b5cf6' },
];

const PERIODS: { value: TimelinePeriod; label: string }[] = [
  { value: '24h', label: '24h' },
  { value: '7d', label: '7d' },
  { value: '30d', label: '30d' },
];

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatBucketLabel(bucketIso: string, groupedByHour: boolean): string {
  const d = new Date(bucketIso);
  if (groupedByHour) {
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  return d.toLocaleDateString([], { month: 'short', day: 'numeric' });
}

// ─── Component ───────────────────────────────────────────────────────────────

export function RequestTimelineChart({
  data,
  period,
  onPeriodChange,
  isLoading,
}: RequestTimelineChartProps) {
  if (isLoading) {
    return (
      <Card>
        <CardHeader className="pb-2">
          <Skeleton className="h-4 w-40" />
        </CardHeader>
        <CardContent>
          <Skeleton className="h-48 w-full" />
        </CardContent>
      </Card>
    );
  }

  const chartData = (data?.buckets ?? []).map((b) => ({
    label: formatBucketLabel(b.bucket, data?.groupedByHour ?? true),
    manual: b.manual,
    ragPipeline: b.ragPipeline,
    eventDriven: b.eventDriven,
    automatedTest: b.automatedTest,
    agentTask: b.agentTask,
    adminOperation: b.adminOperation,
  }));

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-medium">Request Timeline</CardTitle>
          <div className="flex gap-1">
            {PERIODS.map(({ value, label }) => (
              <Button
                key={value}
                variant={period === value ? 'default' : 'ghost'}
                size="sm"
                className="h-6 px-2 text-xs"
                onClick={() => onPeriodChange(value)}
              >
                {label}
              </Button>
            ))}
          </div>
        </div>
        {data && (
          <p className="text-xs text-muted-foreground mt-1">
            {data.totalRequests.toLocaleString()} requests · ${data.totalCostUsd.toFixed(4)} total
          </p>
        )}
      </CardHeader>
      <CardContent>
        {chartData.length === 0 ? (
          <div className="flex h-48 items-center justify-center text-sm text-muted-foreground">
            No requests in this period
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={240}>
            <ComposedChart data={chartData} margin={{ top: 4, right: 4, left: -20, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" className="stroke-muted-foreground/20" />
              <XAxis
                dataKey="label"
                tick={{ fontSize: 11 }}
                tickLine={false}
                axisLine={false}
                interval="preserveStartEnd"
              />
              <YAxis
                tick={{ fontSize: 11 }}
                tickLine={false}
                axisLine={false}
                allowDecimals={false}
              />
              <Tooltip
                contentStyle={{
                  fontSize: 12,
                  borderRadius: 8,
                  border: '1px solid hsl(var(--border))',
                  background: 'hsl(var(--popover))',
                  color: 'hsl(var(--popover-foreground))',
                }}
              />
              <Legend
                wrapperStyle={{ fontSize: 11, paddingTop: 8 }}
                iconType="circle"
                iconSize={8}
              />
              {SOURCE_SERIES.map((s) => (
                <Area
                  key={s.key}
                  type="monotone"
                  dataKey={s.key}
                  name={s.label}
                  stackId="1"
                  stroke={s.color}
                  fill={s.color}
                  fillOpacity={0.6}
                  strokeWidth={1.5}
                  dot={false}
                />
              ))}
            </ComposedChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}
