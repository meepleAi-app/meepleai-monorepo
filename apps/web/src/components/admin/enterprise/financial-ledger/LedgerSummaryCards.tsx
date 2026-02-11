/**
 * LedgerSummaryCards Component
 * Issue #3722 - Manual Ledger CRUD
 *
 * Displays income, expense, and net balance KPI cards for a date range.
 */

'use client';

import { TrendingUpIcon, TrendingDownIcon, ScaleIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface LedgerSummaryCardsProps {
  totalIncome: number;
  totalExpense: number;
  netBalance: number;
  loading: boolean;
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 2,
  }).format(value);
}

export function LedgerSummaryCards({
  totalIncome,
  totalExpense,
  netBalance,
  loading,
}: LedgerSummaryCardsProps) {
  const cards = [
    {
      label: 'Total Income',
      value: totalIncome,
      icon: TrendingUpIcon,
      color: 'text-emerald-600 dark:text-emerald-400',
      bgColor: 'bg-emerald-50 dark:bg-emerald-900/20',
      testId: 'summary-income',
    },
    {
      label: 'Total Expense',
      value: totalExpense,
      icon: TrendingDownIcon,
      color: 'text-red-600 dark:text-red-400',
      bgColor: 'bg-red-50 dark:bg-red-900/20',
      testId: 'summary-expense',
    },
    {
      label: 'Net Balance',
      value: netBalance,
      icon: ScaleIcon,
      color: netBalance >= 0
        ? 'text-emerald-600 dark:text-emerald-400'
        : 'text-red-600 dark:text-red-400',
      bgColor: netBalance >= 0
        ? 'bg-emerald-50 dark:bg-emerald-900/20'
        : 'bg-red-50 dark:bg-red-900/20',
      testId: 'summary-net-balance',
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-4" data-testid="ledger-summary-cards">
      {cards.map((card) => (
        <div
          key={card.testId}
          className="rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 p-5"
          data-testid={card.testId}
        >
          {loading ? (
            <div className="animate-pulse space-y-3">
              <div className="h-4 w-24 bg-muted/30 rounded" />
              <div className="h-7 w-32 bg-muted/20 rounded" />
            </div>
          ) : (
            <>
              <div className="flex items-center gap-2 mb-2">
                <div className={cn('rounded-lg p-1.5', card.bgColor)}>
                  <card.icon className={cn('h-4 w-4', card.color)} />
                </div>
                <span className="text-sm text-zinc-500 dark:text-zinc-400">{card.label}</span>
              </div>
              <p className={cn('text-2xl font-bold', card.color)}>
                {formatCurrency(card.value)}
              </p>
            </>
          )}
        </div>
      ))}
    </div>
  );
}
