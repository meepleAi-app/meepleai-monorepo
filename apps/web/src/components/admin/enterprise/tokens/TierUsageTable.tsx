/**
 * TierUsageTable Component
 * Issue #3692 - Token Management System
 *
 * Table showing token usage breakdown per tier (Free, Basic, Pro, Enterprise).
 */

'use client';

import { PencilIcon } from 'lucide-react';

import type { TierUsage } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

export interface TierUsageTableProps {
  tiers: TierUsage[];
  onEditTier?: (tier: string) => void;
  loading?: boolean;
}

function formatTokens(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(0)}K`;
  return value.toLocaleString();
}

const tierColors: Record<string, string> = {
  Free: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-700 dark:text-zinc-300',
  Basic: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300',
  Pro: 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-300',
  Enterprise: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300',
};

export function TierUsageTable({ tiers, onEditTier, loading }: TierUsageTableProps) {
  if (loading) {
    return (
      <div className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 p-6 animate-pulse" data-testid="tier-usage-table">
        <div className="h-5 w-36 bg-muted/30 rounded mb-4" />
        <div className="space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-10 bg-muted/20 rounded" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div
      className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 overflow-hidden"
      data-testid="tier-usage-table"
    >
      <div className="px-6 py-4 border-b border-zinc-200/50 dark:border-zinc-700/50">
        <h3 className="text-sm font-medium text-zinc-600 dark:text-zinc-300">
          Token Pool per Tier
        </h3>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-zinc-50/50 dark:bg-zinc-800/50">
              <th className="px-6 py-3 text-left text-xs font-medium text-zinc-500 dark:text-zinc-400 uppercase tracking-wider">
                Tier
              </th>
              <th className="px-6 py-3 text-right text-xs font-medium text-zinc-500 dark:text-zinc-400 uppercase tracking-wider">
                Limit/Month
              </th>
              <th className="px-6 py-3 text-right text-xs font-medium text-zinc-500 dark:text-zinc-400 uppercase tracking-wider">
                Current Usage
              </th>
              <th className="px-6 py-3 text-right text-xs font-medium text-zinc-500 dark:text-zinc-400 uppercase tracking-wider">
                Users
              </th>
              <th className="px-6 py-3 text-right text-xs font-medium text-zinc-500 dark:text-zinc-400 uppercase tracking-wider">
                Usage %
              </th>
              {onEditTier && (
                <th className="px-6 py-3 text-right text-xs font-medium text-zinc-500 dark:text-zinc-400 uppercase tracking-wider">
                  Action
                </th>
              )}
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-200/50 dark:divide-zinc-700/50">
            {tiers.map((tier) => (
              <tr
                key={tier.tier}
                className="hover:bg-zinc-50/30 dark:hover:bg-zinc-800/30 transition-colors"
                data-testid={`tier-row-${tier.tier}`}
              >
                <td className="px-6 py-3">
                  <span className={cn(
                    'inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium',
                    tierColors[tier.tier] || 'bg-zinc-100 text-zinc-700'
                  )}>
                    {tier.tier}
                  </span>
                </td>
                <td className="px-6 py-3 text-right font-mono text-xs text-zinc-700 dark:text-zinc-300 tabular-nums">
                  {tier.limitPerMonth === 0 ? 'Unlimited' : formatTokens(tier.limitPerMonth)}
                </td>
                <td className="px-6 py-3 text-right font-mono text-xs text-zinc-700 dark:text-zinc-300 tabular-nums">
                  {formatTokens(tier.currentUsage)}
                </td>
                <td className="px-6 py-3 text-right font-mono text-xs text-zinc-700 dark:text-zinc-300 tabular-nums">
                  {tier.userCount.toLocaleString()}
                </td>
                <td className="px-6 py-3 text-right">
                  <div className="flex items-center justify-end gap-2">
                    <div className="w-16 h-1.5 bg-zinc-200/50 dark:bg-zinc-700/50 rounded-full overflow-hidden">
                      <div
                        className={cn(
                          'h-full rounded-full',
                          tier.usagePercent >= 90 ? 'bg-red-500' : tier.usagePercent >= 70 ? 'bg-amber-500' : 'bg-emerald-500'
                        )}
                        style={{ width: `${Math.min(tier.usagePercent, 100)}%` }}
                      />
                    </div>
                    <span className="text-xs text-zinc-500 dark:text-zinc-400 tabular-nums w-10 text-right">
                      {tier.usagePercent.toFixed(0)}%
                    </span>
                  </div>
                </td>
                {onEditTier && (
                  <td className="px-6 py-3 text-right">
                    <button
                      onClick={() => onEditTier(tier.tier)}
                      className="inline-flex items-center gap-1 text-xs text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300 transition-colors"
                      data-testid={`edit-tier-${tier.tier}`}
                    >
                      <PencilIcon className="h-3 w-3" />
                      Edit
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
