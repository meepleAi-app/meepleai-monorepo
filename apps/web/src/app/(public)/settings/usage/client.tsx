/* eslint-disable security/detect-object-injection -- Safe Record access with provider name keys */
/**
 * PersonalUsagePageClient - Issue #3080
 *
 * Client component for user's personal AI usage page.
 * Shows usage statistics, cost breakdown, and provider details.
 */

'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Calendar, TrendingUp, DollarSign, Activity, RefreshCw } from 'lucide-react';
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
} from 'recharts';

import { LoadingButton } from '@/components/loading/LoadingButton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

type UserAiUsageResponse = {
  userId: string;
  period: {
    startDate: string;
    endDate: string;
    days: number;
  };
  totalCost: number;
  costsByProvider: Record<string, number>;
  dailyAverage: number;
};

// Colors from MeepleAI design system
const PROVIDER_COLORS: Record<string, string> = {
  'OpenRouter': '#d2691e',
  'Ollama': '#8b5cf6',
  'Default': '#666666',
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
  } = useQuery<UserAiUsageResponse>({
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

  const providerChartData = usage?.costsByProvider
    ? Object.entries(usage.costsByProvider).map(([name, value]) => ({
        name,
        value,
        color: PROVIDER_COLORS[name] ?? PROVIDER_COLORS['Default'],
      }))
    : [];

  const topProvider = usage?.costsByProvider
    ? Object.entries(usage.costsByProvider).reduce(
        (a, b) => (a[1] > b[1] ? a : b),
        ['N/A', 0] as [string, number]
      )
    : null;

  if (error) {
    return (
      <div className="container max-w-4xl py-8">
        <Link href="/settings" className="mb-6 flex items-center gap-2 text-muted-foreground hover:text-foreground">
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
        <Link href="/settings" className="mb-4 flex items-center gap-2 text-muted-foreground hover:text-foreground">
          <ArrowLeft className="h-4 w-4" />
          Back to Settings
        </Link>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="font-['Quicksand',sans-serif] text-2xl font-bold">
              Your AI Usage
            </h1>
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

            <LoadingButton
              onClick={() => refetch()}
              isLoading={isLoading}
              variant="outline"
            >
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
              icon={DollarSign}
              label="Total Cost"
              value={formatCost(usage.totalCost)}
              subValue={usage.totalCost === 0 ? 'Free tier usage' : `Last ${usage.period.days} days`}
            />

            <UsageStat
              icon={TrendingUp}
              label="Daily Average"
              value={formatCost(usage.dailyAverage)}
            />

            <UsageStat
              icon={Activity}
              label="Top Provider"
              value={topProvider ? topProvider[0] : 'N/A'}
              subValue={topProvider && topProvider[1] > 0 ? formatCost(topProvider[1]) : undefined}
            />

            <UsageStat
              icon={Calendar}
              label="Period"
              value={`${usage.period.days} days`}
              subValue={`${usage.period.startDate.split('T')[0]} - ${usage.period.endDate.split('T')[0]}`}
            />
          </div>

          {/* Cost by Provider Chart */}
          {providerChartData.length > 0 && (
            <div className="mb-6 rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                Cost by Provider
              </h3>
              <div className="grid gap-6 lg:grid-cols-2">
                {/* Pie Chart */}
                <ResponsiveContainer width="100%" height={250}>
                  <PieChart>
                    <Pie
                      data={providerChartData}
                      cx="50%"
                      cy="50%"
                      innerRadius={50}
                      outerRadius={80}
                      dataKey="value"
                      nameKey="name"
                      label={({ name, value }) => `${name}: ${formatCost(value)}`}
                    >
                      {providerChartData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <Tooltip formatter={(value: number) => formatCost(value)} />
                    <Legend />
                  </PieChart>
                </ResponsiveContainer>

                {/* Bar Chart */}
                <ResponsiveContainer width="100%" height={250}>
                  <BarChart data={providerChartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis tickFormatter={(value) => formatCost(value)} />
                    <Tooltip formatter={(value: number) => formatCost(value)} />
                    <Bar dataKey="value" fill="#d2691e" radius={[4, 4, 0, 0]}>
                      {providerChartData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          )}

          {/* Provider List */}
          {providerChartData.length > 0 && (
            <div className="rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                Provider Details
              </h3>
              <div className="space-y-3">
                {providerChartData.map(({ name, value, color }) => (
                  <div key={name} className="flex items-center justify-between rounded-lg bg-muted/50 p-3">
                    <div className="flex items-center gap-3">
                      <div
                        className="h-4 w-4 rounded-full"
                        style={{ backgroundColor: color }}
                      />
                      <span className="font-medium">{name}</span>
                    </div>
                    <div className="text-right">
                      <p className="font-bold">{formatCost(value)}</p>
                      <p className="text-xs text-muted-foreground">
                        {usage.totalCost > 0 ? `${((value / usage.totalCost) * 100).toFixed(1)}%` : '0%'}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Empty State */}
          {providerChartData.length === 0 && (
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
            Data from {usage.period.startDate.split('T')[0]} to {usage.period.endDate.split('T')[0]}
          </div>
        </>
      )}
    </div>
  );
}
