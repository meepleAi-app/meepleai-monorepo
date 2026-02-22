'use client';

/**
 * Cost breakdown panel.
 * Issue #5080: Admin usage page — cost breakdown by model, source, and tier.
 *
 * Shows a donut chart for top models + a small table for source/tier breakdown.
 */

import { useState } from 'react';

import {
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
} from 'recharts';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import type { UsageCostsDto } from '@/lib/api/schemas/admin-knowledge-base.schemas';

// ─── Types ───────────────────────────────────────────────────────────────────

export type CostPeriod = '1d' | '7d' | '30d';

interface CostBreakdownPanelProps {
  data: UsageCostsDto | null | undefined;
  period: CostPeriod;
  onPeriodChange: (period: CostPeriod) => void;
  isLoading?: boolean;
}

// ─── Constants ───────────────────────────────────────────────────────────────

const DONUT_COLORS = [
  '#6366f1',
  '#f59e0b',
  '#10b981',
  '#3b82f6',
  '#ec4899',
  '#8b5cf6',
  '#14b8a6',
  '#f97316',
  '#84cc16',
  '#06b6d4',
];

const PERIODS: { value: CostPeriod; label: string }[] = [
  { value: '1d', label: '1d' },
  { value: '7d', label: '7d' },
  { value: '30d', label: '30d' },
];

// ─── Helpers ─────────────────────────────────────────────────────────────────

function shortModelId(modelId: string): string {
  const parts = modelId.split('/');
  return parts.length > 1 ? parts[parts.length - 1] : modelId;
}

function formatUsd(val: number): string {
  if (val >= 1) return `$${val.toFixed(2)}`;
  return `$${val.toFixed(4)}`;
}

// ─── Component ───────────────────────────────────────────────────────────────

export function CostBreakdownPanel({
  data,
  period,
  onPeriodChange,
  isLoading,
}: CostBreakdownPanelProps) {
  const [activeTab, setActiveTab] = useState<'model' | 'source' | 'tier'>('model');

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

  const donutData = (data?.byModel ?? []).map((m, i) => ({
    name: shortModelId(m.modelId),
    value: m.costUsd,
    color: DONUT_COLORS[i % DONUT_COLORS.length],
    requests: m.requests,
  }));

  const tableRows: { label: string; costUsd: number; requests: number }[] =
    activeTab === 'model'
      ? (data?.byModel ?? []).map((m) => ({
          label: shortModelId(m.modelId),
          costUsd: m.costUsd,
          requests: m.requests,
        }))
      : activeTab === 'source'
        ? (data?.bySource ?? []).map((s) => ({
            label: s.source,
            costUsd: s.costUsd,
            requests: s.requests,
          }))
        : (data?.byTier ?? []).map((t) => ({
            label: t.tier,
            costUsd: t.costUsd,
            requests: t.requests,
          }));

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-medium">Cost Breakdown</CardTitle>
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
            {formatUsd(data.totalCostUsd)} total · {data.totalRequests.toLocaleString()} requests
          </p>
        )}
      </CardHeader>
      <CardContent>
        {!data || data.byModel.length === 0 ? (
          <div className="flex h-48 items-center justify-center text-sm text-muted-foreground">
            No cost data in this period
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {/* Donut */}
            <ResponsiveContainer width="100%" height={180}>
              <PieChart>
                <Pie
                  data={donutData}
                  cx="50%"
                  cy="50%"
                  innerRadius={45}
                  outerRadius={70}
                  dataKey="value"
                  nameKey="name"
                  paddingAngle={2}
                >
                  {donutData.map((entry, i) => (
                    <Cell key={entry.name} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip
                  formatter={(val: number) => [formatUsd(val), 'Cost']}
                  contentStyle={{
                    fontSize: 12,
                    borderRadius: 8,
                    border: '1px solid hsl(var(--border))',
                    background: 'hsl(var(--popover))',
                    color: 'hsl(var(--popover-foreground))',
                  }}
                />
                <Legend
                  iconType="circle"
                  iconSize={8}
                  wrapperStyle={{ fontSize: 10 }}
                />
              </PieChart>
            </ResponsiveContainer>

            {/* Table */}
            <div className="flex flex-col gap-2">
              <div className="flex gap-1">
                {(['model', 'source', 'tier'] as const).map((tab) => (
                  <button
                    key={tab}
                    onClick={() => setActiveTab(tab)}
                    className={`text-xs px-2 py-0.5 rounded-full transition-colors ${
                      activeTab === tab
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    {tab.charAt(0).toUpperCase() + tab.slice(1)}
                  </button>
                ))}
              </div>
              <div className="overflow-y-auto max-h-36 text-xs space-y-1">
                {tableRows.map((row) => (
                  <div
                    key={row.label}
                    className="flex items-center justify-between rounded px-2 py-1 hover:bg-muted/50"
                  >
                    <span className="truncate max-w-[55%] font-medium" title={row.label}>
                      {row.label}
                    </span>
                    <span className="text-muted-foreground">
                      {formatUsd(row.costUsd)} · {row.requests.toLocaleString()}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
