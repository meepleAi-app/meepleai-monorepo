/**
 * TokenBalanceCard Component
 * Issue #3692 - Token Management System
 *
 * Hero card showing OpenRouter balance, usage percentage,
 * and projected depletion date.
 */

'use client';

import { WalletIcon, TrendingDownIcon, AlertTriangleIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface TokenBalanceCardProps {
  currentBalance: number;
  totalBudget: number;
  currency: string;
  usagePercent: number;
  projectedDaysUntilDepletion: number | null;
  loading?: boolean;
}

export function TokenBalanceCard({
  currentBalance,
  totalBudget,
  currency,
  usagePercent,
  projectedDaysUntilDepletion,
  loading,
}: TokenBalanceCardProps) {
  const isLow = usagePercent >= 80;
  const isCritical = usagePercent >= 95;

  if (loading) {
    return (
      <div className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 p-6 animate-pulse" data-testid="token-balance-card">
        <div className="h-5 w-32 bg-muted/30 rounded mb-4" />
        <div className="h-10 w-48 bg-muted/30 rounded mb-3" />
        <div className="h-3 w-full bg-muted/20 rounded-full mb-3" />
        <div className="h-4 w-40 bg-muted/20 rounded" />
      </div>
    );
  }

  const currencySymbol = currency === 'EUR' ? '€' : currency === 'USD' ? '$' : currency;

  return (
    <div
      className={cn(
        'rounded-2xl backdrop-blur-md border p-6 relative overflow-hidden',
        isCritical
          ? 'bg-red-50/70 dark:bg-red-900/20 border-red-200/50 dark:border-red-700/50'
          : isLow
            ? 'bg-amber-50/70 dark:bg-amber-900/20 border-amber-200/50 dark:border-amber-700/50'
            : 'bg-white/70 dark:bg-zinc-800/70 border-zinc-200/50 dark:border-zinc-700/50'
      )}
      data-testid="token-balance-card"
    >
      {/* Header */}
      <div className="flex items-center gap-2 mb-3">
        <WalletIcon className="h-5 w-5 text-zinc-500 dark:text-zinc-400" />
        <h3 className="text-sm font-medium text-zinc-600 dark:text-zinc-300">
          OpenRouter Balance
        </h3>
        {isCritical && (
          <AlertTriangleIcon className="h-4 w-4 text-red-500 ml-auto" />
        )}
      </div>

      {/* Balance display */}
      <div className="mb-4">
        <span className="text-3xl font-bold text-zinc-900 dark:text-zinc-100 tabular-nums" data-testid="token-balance-amount">
          {currencySymbol}{currentBalance.toLocaleString('it-IT', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}
        </span>
        <span className="text-lg text-zinc-400 dark:text-zinc-500 ml-1">
          / {currencySymbol}{totalBudget.toLocaleString('it-IT', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}
        </span>
      </div>

      {/* Progress bar */}
      <div className="w-full h-2.5 bg-zinc-200/50 dark:bg-zinc-700/50 rounded-full mb-3 overflow-hidden">
        <div
          className={cn(
            'h-full rounded-full transition-all duration-500',
            isCritical
              ? 'bg-red-500'
              : isLow
                ? 'bg-amber-500'
                : 'bg-emerald-500'
          )}
          style={{ width: `${Math.min(usagePercent, 100)}%` }}
          data-testid="token-usage-bar"
        />
      </div>

      {/* Footer stats */}
      <div className="flex items-center justify-between text-sm">
        <span className={cn(
          'font-medium tabular-nums',
          isCritical ? 'text-red-600 dark:text-red-400' : isLow ? 'text-amber-600 dark:text-amber-400' : 'text-zinc-600 dark:text-zinc-400'
        )} data-testid="token-usage-percent">
          {usagePercent.toFixed(1)}% used
        </span>
        {projectedDaysUntilDepletion !== null && (
          <span className="flex items-center gap-1 text-zinc-500 dark:text-zinc-400" data-testid="token-depletion-projection">
            <TrendingDownIcon className="h-3.5 w-3.5" />
            ~{projectedDaysUntilDepletion} days remaining
          </span>
        )}
      </div>
    </div>
  );
}
