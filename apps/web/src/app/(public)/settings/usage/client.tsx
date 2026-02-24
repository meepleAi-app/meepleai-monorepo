/**
 * PersonalUsagePageClient - Issue #3080, enhanced in Issue #3338
 *
 * Client component for user's personal AI usage page.
 * Shows detailed usage statistics including token counts, cost breakdown,
 * model usage, operation breakdown, and daily usage time series.
 */

'use client';

import { useState, useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Calendar, DollarSign, Activity, RefreshCw, Zap, Hash } from 'lucide-react';
import Link from 'next/link';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Legend,
  AreaChart,
  Area,
} from 'recharts';

import { LoadingButton } from '@/components/loading/LoadingButton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import type { UserAiUsageDto } from '@/lib/api/schemas';

// Colors from MeepleAI design system
const MODEL_COLORS: string[] = [
  '#d2691e', // Orange
  '#8b5cf6', // Purple
  '#16a34a', // Green
  '#dc2626', // Red
  '#2563eb', // Blue
  '#eab308', // Yellow
  '#ec4899', // Pink
  '#666666', // Gray
];

const OPERATION_COLORS: Record<string, string> = {
  chat: '#d2691e',
  rag_query: '#8b5cf6',
  embedding: '#16a34a',
  explain: '#2563eb',
  qa: '#ec4899',
  unknown: '#666666',
};

function UsageStat({
  icon: Icon,
  label,
  value,
  subValue,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
  subValue?: string;
}) {
  return (
    <div className="flex items-center gap-3 rounded-lg border bg-card p-4 shadow-sm">
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
        <Icon className="h-6 w-6 text-primary" />
      </div>
      <div>
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className="text-xl font-bold">{value}</p>
        {subValue && <p className="text-xs text-muted-foreground">{subValue}</p>}
      </div>
    </div>
  );
}

export function PersonalUsagePageClient() {
  const [days, setDays] = useState('30');

  const {
    data: usage,
    isLoading,
    error,
    refetch,
  } = useQuery<UserAiUsageDto>({
    queryKey: ['user-ai-usage', days],
    queryFn: async () => {
      const response = await fetch(`/api/v1/users/me/ai-usage?days=${days}`, {
        credentials: 'include',
      });
      if (!response.ok) {
        throw new Error('Failed to fetch AI usage');
      }
      return response.json();
    },
    staleTime: 5 * 60 * 1000,
  });

  const formatCost = (cost: number) => {
    if (cost < 0.01) return '< $0.01';
    return `$${cost.toFixed(2)}`;
  };

  const formatTokens = (tokens: number) => {
    if (tokens >= 1000000) return `${(tokens / 1000000).toFixed(1)}M`;
    if (tokens >= 1000) return `${(tokens / 1000).toFixed(1)}K`;
    return tokens.toString();
  };

  // Transform model usage data for charts
  const modelChartData = useMemo(() => {
    if (!usage?.byModel) return [];
    return usage.byModel.map((item, index) => ({
      name: item.model.split('/').pop() ?? item.model, // Show last part of model name
      fullName: item.model,
      tokens: item.tokens,
      cost: item.cost,
      color: MODEL_COLORS[index % MODEL_COLORS.length],
    }));
  }, [usage]);

  // Transform operation usage data for charts
  const operationChartData = useMemo(() => {
    if (!usage?.byOperation) return [];
    return usage.byOperation.map(item => ({
      name: item.operation,
      count: item.count,
      tokens: item.tokens,
      color: OPERATION_COLORS[item.operation] ?? OPERATION_COLORS['unknown'],
    }));
  }, [usage]);

  // Transform daily usage data for time series chart
  const dailyUsageChartData = useMemo(() => {
    if (!usage?.dailyUsage) return [];
    return usage.dailyUsage.map(item => ({
      date: item.date,
      tokens: item.tokens,
    }));
  }, [usage]);

  const topModel = modelChartData.length > 0 ? modelChartData[0] : null;

  if (error) {
    return (
      <div className="container max-w-4xl py-8">
        <Link
          href="/settings"
          className="mb-6 flex items-center gap-2 text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Settings
        </Link>
        <div className="rounded-xl border border-destructive/50 bg-destructive/10 p-6">
          <p className="text-destructive">Unable to load usage data. Please try again later.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="container max-w-4xl py-8">
      {/* Header */}
      <div className="mb-6">
        <Link
          href="/settings"
          className="mb-4 flex items-center gap-2 text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Settings
        </Link>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="font-['Quicksand',sans-serif] text-2xl font-bold">Your AI Usage</h1>
            <p className="text-muted-foreground">
              Track your AI usage and costs across different providers
            </p>
          </div>

          <div className="flex items-center gap-3">
            <Select value={days} onValueChange={setDays}>
              <SelectTrigger className="w-[140px]">
                <Calendar className="mr-2 h-4 w-4" />
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="7">Last 7 days</SelectItem>
                <SelectItem value="30">Last 30 days</SelectItem>
                <SelectItem value="90">Last 90 days</SelectItem>
                <SelectItem value="365">Last year</SelectItem>
              </SelectContent>
            </Select>

            <LoadingButton onClick={() => refetch()} isLoading={isLoading} variant="outline">
              <RefreshCw className={`mr-2 h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
              Refresh
            </LoadingButton>
          </div>
        </div>
      </div>

      {/* Loading State */}
      {isLoading && (
        <div className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
            {[1, 2, 3, 4].map(i => (
              <div key={i} className="h-24 animate-pulse rounded-lg border bg-muted" />
            ))}
          </div>
          <div className="h-[300px] animate-pulse rounded-xl border bg-muted" />
        </div>
      )}

      {/* Stats Cards */}
      {usage && (
        <>
          <div className="mb-6 grid gap-4 md:grid-cols-2 lg:grid-cols-4">
            <UsageStat
              icon={Zap}
              label="Total Tokens"
              value={formatTokens(usage.totalTokens)}
              subValue={usage.totalTokens === 0 ? 'No usage yet' : `Last ${days} days`}
            />

            <UsageStat
              icon={DollarSign}
              label="Total Cost"
              value={formatCost(usage.totalCostUsd)}
              subValue={usage.totalCostUsd === 0 ? 'Free tier' : undefined}
            />

            <UsageStat
              icon={Hash}
              label="Requests"
              value={usage.requestCount.toString()}
              subValue={topModel ? `Top: ${topModel.name}` : undefined}
            />

            <UsageStat
              icon={Calendar}
              label="Period"
              value={`${days} days`}
              subValue={`${usage.period.from} - ${usage.period.to}`}
            />
          </div>

          {/* Daily Usage Time Series */}
          {dailyUsageChartData.length > 0 && (
            <div className="mb-6 rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                Daily Token Usage
              </h3>
              <ResponsiveContainer width="100%" height={200}>
                <AreaChart data={dailyUsageChartData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis
                    dataKey="date"
                    tickFormatter={value => value.slice(-5)} // Show MM-DD
                    fontSize={12}
                  />
                  <YAxis tickFormatter={value => formatTokens(value)} fontSize={12} />
                  <Tooltip
                    formatter={(value: number | undefined) => [formatTokens(value ?? 0), 'Tokens']}
                    labelFormatter={label => `Date: ${label}`}
                  />
                  <Area
                    type="monotone"
                    dataKey="tokens"
                    stroke="#d2691e"
                    fill="#d2691e"
                    fillOpacity={0.3}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}

          {/* Usage by Model */}
          {modelChartData.length > 0 && (
            <div className="mb-6 rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                Usage by Model
              </h3>
              <div className="grid gap-6 lg:grid-cols-2">
                {/* Pie Chart */}
                <ResponsiveContainer width="100%" height={250}>
                  <PieChart>
                    <Pie
                      data={modelChartData}
                      cx="50%"
                      cy="50%"
                      innerRadius={50}
                      outerRadius={80}
                      dataKey="tokens"
                      nameKey="name"
                      label={({ name, value }) => `${name}: ${formatTokens(Number(value) || 0)}`}
                    >
                      {modelChartData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <Tooltip
                      formatter={(value: number | undefined) => [
                        formatTokens(value ?? 0),
                        'Tokens',
                      ]}
                    />
                    <Legend />
                  </PieChart>
                </ResponsiveContainer>

                {/* Bar Chart */}
                <ResponsiveContainer width="100%" height={250}>
                  <BarChart data={modelChartData} layout="vertical">
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis type="number" tickFormatter={value => formatTokens(value)} />
                    <YAxis type="category" dataKey="name" width={80} fontSize={12} />
                    <Tooltip formatter={(value: number | undefined) => formatTokens(value ?? 0)} />
                    <Bar dataKey="tokens" fill="#d2691e" radius={[0, 4, 4, 0]}>
                      {modelChartData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          )}

          {/* Usage by Operation */}
          {operationChartData.length > 0 && (
            <div className="mb-6 rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                Usage by Operation
              </h3>
              <div className="space-y-3">
                {operationChartData.map(({ name, count, tokens, color }) => (
                  <div
                    key={name}
                    className="flex items-center justify-between rounded-lg bg-muted/50 p-3"
                  >
                    <div className="flex items-center gap-3">
                      <div className="h-4 w-4 rounded-full" style={{ backgroundColor: color }} />
                      <span className="font-medium capitalize">{name.replace('_', ' ')}</span>
                    </div>
                    <div className="text-right">
                      <p className="font-bold">{count} requests</p>
                      <p className="text-xs text-muted-foreground">{formatTokens(tokens)} tokens</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Model Details List */}
          {modelChartData.length > 0 && (
            <div className="rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                Model Details
              </h3>
              <div className="space-y-3">
                {modelChartData.map(({ name, fullName, tokens, cost, color }) => (
                  <div
                    key={fullName}
                    className="flex items-center justify-between rounded-lg bg-muted/50 p-3"
                  >
                    <div className="flex items-center gap-3">
                      <div className="h-4 w-4 rounded-full" style={{ backgroundColor: color }} />
                      <div>
                        <span className="font-medium">{name}</span>
                        <p className="text-xs text-muted-foreground">{fullName}</p>
                      </div>
                    </div>
                    <div className="text-right">
                      <p className="font-bold">{formatTokens(tokens)}</p>
                      <p className="text-xs text-muted-foreground">{formatCost(cost)}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Empty State */}
          {modelChartData.length === 0 && operationChartData.length === 0 && (
            <div className="rounded-xl border bg-card p-12 text-center shadow-sm">
              <Activity className="mx-auto mb-4 h-12 w-12 text-muted-foreground" />
              <h3 className="mb-2 font-['Quicksand',sans-serif] text-lg font-bold">
                No usage data yet
              </h3>
              <p className="text-muted-foreground">
                Start using AI features to see your usage statistics here.
              </p>
            </div>
          )}

          {/* Period Info */}
          <div className="mt-6 text-center text-sm text-muted-foreground">
            Data from {usage.period.from} to {usage.period.to}
          </div>
        </>
      )}
    </div>
  );
}
