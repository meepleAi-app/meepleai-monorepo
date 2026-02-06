/**
 * TopConsumersTable Component
 * Issue #3692 - Token Management System
 *
 * Table showing top token consumers across all tiers.
 */

'use client';

import { UsersIcon } from 'lucide-react';

import type { TopConsumer } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

export interface TopConsumersTableProps {
  consumers: TopConsumer[];
  loading?: boolean;
}

const tierBadgeColors: Record<string, string> = {
  Free: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-700 dark:text-zinc-400',
  Basic: 'bg-blue-50 text-blue-600 dark:bg-blue-900/20 dark:text-blue-400',
  Pro: 'bg-purple-50 text-purple-600 dark:bg-purple-900/20 dark:text-purple-400',
  Enterprise: 'bg-amber-50 text-amber-600 dark:bg-amber-900/20 dark:text-amber-400',
};

function formatTokens(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return value.toLocaleString();
}

export function TopConsumersTable({ consumers, loading }: TopConsumersTableProps) {
  if (loading) {
    return (
      <div className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 p-6 animate-pulse" data-testid="top-consumers-table">
        <div className="h-5 w-36 bg-muted/30 rounded mb-4" />
        <div className="space-y-3">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-8 bg-muted/20 rounded" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div
      className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 overflow-hidden"
      data-testid="top-consumers-table"
    >
      <div className="px-6 py-4 border-b border-zinc-200/50 dark:border-zinc-700/50 flex items-center gap-2">
        <UsersIcon className="h-4 w-4 text-zinc-500 dark:text-zinc-400" />
        <h3 className="text-sm font-medium text-zinc-600 dark:text-zinc-300">
          Top Consumers
        </h3>
      </div>

      {consumers.length === 0 ? (
        <div className="px-6 py-8 text-center text-sm text-zinc-400 dark:text-zinc-500">
          No consumer data available
        </div>
      ) : (
        <div className="divide-y divide-zinc-200/50 dark:divide-zinc-700/50">
          {consumers.map((consumer, i) => (
            <div
              key={consumer.userId}
              className="flex items-center gap-3 px-6 py-3 hover:bg-zinc-50/30 dark:hover:bg-zinc-800/30 transition-colors"
              data-testid={`consumer-row-${i}`}
            >
              {/* Rank */}
              <span className="text-xs font-medium text-zinc-400 dark:text-zinc-500 w-5 tabular-nums">
                #{i + 1}
              </span>

              {/* User info */}
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 truncate">
                  {consumer.displayName}
                </p>
                <p className="text-xs text-zinc-500 dark:text-zinc-400 truncate">
                  {consumer.email}
                </p>
              </div>

              {/* Tier badge */}
              <span className={cn(
                'inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-medium',
                tierBadgeColors[consumer.tier] || 'bg-zinc-100 text-zinc-600'
              )}>
                {consumer.tier}
              </span>

              {/* Usage */}
              <div className="text-right w-20">
                <p className="text-sm font-mono font-medium text-zinc-900 dark:text-zinc-100 tabular-nums">
                  {formatTokens(consumer.tokensUsed)}
                </p>
                <p className="text-[10px] text-zinc-400 dark:text-zinc-500 tabular-nums">
                  {consumer.percentOfTierLimit.toFixed(0)}% of limit
                </p>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
