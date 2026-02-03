/**
 * PersonalUsageCard Component - Issue #3080
 *
 * Displays user's personal AI usage statistics.
 * Uses /users/me/ai-usage endpoint from Issue #3074.
 */

'use client';

import { useQuery } from '@tanstack/react-query';
import { Activity, DollarSign, TrendingUp, Calendar } from 'lucide-react';

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

type PersonalUsageCardProps = {
  className?: string;
  days?: number;
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
    <div className="flex items-center gap-3 rounded-lg bg-muted/50 p-3">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10">
        <Icon className="h-5 w-5 text-primary" />
      </div>
      <div>
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className="text-lg font-semibold">{value}</p>
        {subValue && <p className="text-xs text-muted-foreground">{subValue}</p>}
      </div>
    </div>
  );
}

export function PersonalUsageCard({ className, days = 30 }: PersonalUsageCardProps) {
  const {
    data: usage,
    isLoading,
    error,
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
    staleTime: 5 * 60 * 1000, // 5 minutes
  });

  if (isLoading) {
    return (
      <div className={`animate-pulse rounded-xl border bg-card p-6 ${className ?? ''}`}>
        <div className="mb-4 h-6 w-32 rounded bg-muted" />
        <div className="grid gap-3 md:grid-cols-2">
          {[1, 2, 3, 4].map(i => (
            <div key={i} className="h-20 rounded-lg bg-muted" />
          ))}
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={`rounded-xl border border-destructive/50 bg-destructive/10 p-6 ${className ?? ''}`}>
        <p className="text-destructive">Unable to load usage data</p>
      </div>
    );
  }

  if (!usage) {
    return null;
  }

  const formatCost = (cost: number) => {
    if (cost < 0.01) return '< $0.01';
    return `$${cost.toFixed(2)}`;
  };

  const providerEntries = Object.entries(usage.costsByProvider);
  const topProvider = providerEntries.length > 0
    ? providerEntries.reduce((a, b) => (a[1] > b[1] ? a : b))
    : null;

  return (
    <div className={`rounded-xl border bg-card p-6 shadow-sm ${className ?? ''}`}>
      <div className="mb-4 flex items-center justify-between">
        <h3 className="font-['Quicksand',sans-serif] text-lg font-bold">
          Your AI Usage
        </h3>
        <span className="text-sm text-muted-foreground">
          Last {usage.period.days} days
        </span>
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        <UsageStat
          icon={DollarSign}
          label="Total Cost"
          value={formatCost(usage.totalCost)}
          subValue={usage.totalCost === 0 ? 'Free tier usage' : undefined}
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
          subValue={topProvider ? formatCost(topProvider[1]) : undefined}
        />

        <UsageStat
          icon={Calendar}
          label="Period"
          value={`${usage.period.days} days`}
          subValue={`${usage.period.startDate.split('T')[0]} - ${usage.period.endDate.split('T')[0]}`}
        />
      </div>

      {providerEntries.length > 1 && (
        <div className="mt-4 border-t pt-4">
          <p className="mb-2 text-sm font-medium text-muted-foreground">Cost by Provider</p>
          <div className="flex flex-wrap gap-2">
            {providerEntries.map(([provider, cost]) => (
              <span
                key={provider}
                className="rounded-full bg-muted px-3 py-1 text-sm"
              >
                {provider}: {formatCost(cost)}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
