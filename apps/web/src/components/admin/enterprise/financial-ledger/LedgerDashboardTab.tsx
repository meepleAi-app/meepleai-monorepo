/**
 * LedgerDashboardTab Component
 * Issue #3723 - Ledger Dashboard and Visualization
 *
 * Dashboard view with:
 * - KPI cards: Current month balance, Profit margin, MoM growth, Revenue/Costs
 * - Revenue vs Costs chart (12 months)
 * - Category breakdown chart
 * - Recent ledger entries table
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  RefreshCwIcon,
  WalletIcon,
  PercentIcon,
  TrendingUpIcon,
  TrendingDownIcon,
  ArrowUpRightIcon,
  ArrowDownRightIcon,
} from 'lucide-react';

import { api } from '@/lib/api';
import type {
  LedgerDashboardData,
  LedgerEntryDto,
  MonthlyRevenueData,
  CategoryBreakdownItem,
} from '@/lib/api/schemas/financial-ledger.schemas';
import {
  LEDGER_ENTRY_TYPE_MAP,
  LEDGER_CATEGORY_MAP,
} from '@/lib/api/schemas/financial-ledger.schemas';
import { cn } from '@/lib/utils';

import { CategoryBreakdownChart } from './CategoryBreakdownChart';
import { RevenueVsCostsChart } from './RevenueVsCostsChart';

// ========== Deterministic Mock Data ==========
// Uses index-based patterns to avoid hydration mismatch (no Math.random)

const MONTH_LABELS = [
  'Mar 25', 'Apr 25', 'May 25', 'Jun 25',
  'Jul 25', 'Aug 25', 'Sep 25', 'Oct 25',
  'Nov 25', 'Dec 25', 'Jan 26', 'Feb 26',
];

const REVENUE_BASE = [1200, 1350, 1500, 1680, 1750, 1900, 2100, 2250, 2400, 2600, 2800, 3100];
const COSTS_BASE = [800, 850, 900, 920, 980, 1050, 1100, 1150, 1200, 1280, 1350, 1400];

const MOCK_MONTHLY_DATA: MonthlyRevenueData[] = MONTH_LABELS.map((month, i) => ({
  month,
  revenue: REVENUE_BASE[i],
  costs: COSTS_BASE[i],
}));

const MOCK_CATEGORY_BREAKDOWN: CategoryBreakdownItem[] = [
  { category: 'Subscription', amount: 12500, percentage: 40.3, type: 'Income' },
  { category: 'TokenPurchase', amount: 8200, percentage: 26.4, type: 'Income' },
  { category: 'PlatformFee', amount: 5300, percentage: 17.1, type: 'Income' },
  { category: 'Infrastructure', amount: 4800, percentage: 35.8, type: 'Expense' },
  { category: 'TokenUsage', amount: 3600, percentage: 26.9, type: 'Expense' },
  { category: 'Operational', amount: 2800, percentage: 20.9, type: 'Expense' },
  { category: 'Marketing', amount: 1500, percentage: 11.2, type: 'Expense' },
  { category: 'Other', amount: 700, percentage: 5.2, type: 'Expense' },
];

const MOCK_RECENT_ENTRIES: LedgerEntryDto[] = [
  {
    id: 'dash-0000-0000-0000-000000000001',
    date: '2026-02-10T00:00:00Z',
    type: 0, category: 0, amount: 299.99, currency: 'EUR', source: 0,
    description: 'Monthly Pro subscription - Acme Corp',
    metadata: null, createdByUserId: null,
    createdAt: '2026-02-10T10:00:00Z', updatedAt: null,
  },
  {
    id: 'dash-0000-0000-0000-000000000002',
    date: '2026-02-09T00:00:00Z',
    type: 1, category: 7, amount: 45.50, currency: 'EUR', source: 0,
    description: 'Cloud hosting - daily compute',
    metadata: null, createdByUserId: null,
    createdAt: '2026-02-09T05:00:00Z', updatedAt: null,
  },
  {
    id: 'dash-0000-0000-0000-000000000003',
    date: '2026-02-08T00:00:00Z',
    type: 0, category: 1, amount: 150.00, currency: 'EUR', source: 0,
    description: 'Token purchase - user batch',
    metadata: null, createdByUserId: null,
    createdAt: '2026-02-08T14:30:00Z', updatedAt: null,
  },
  {
    id: 'dash-0000-0000-0000-000000000004',
    date: '2026-02-07T00:00:00Z',
    type: 1, category: 2, amount: 12.30, currency: 'EUR', source: 0,
    description: 'LLM token usage - daily aggregate',
    metadata: null, createdByUserId: null,
    createdAt: '2026-02-07T05:00:00Z', updatedAt: null,
  },
  {
    id: 'dash-0000-0000-0000-000000000005',
    date: '2026-02-06T00:00:00Z',
    type: 0, category: 3, amount: 25.00, currency: 'EUR', source: 0,
    description: 'Platform fee - premium features',
    metadata: null, createdByUserId: null,
    createdAt: '2026-02-06T08:00:00Z', updatedAt: null,
  },
];

const MOCK_DASHBOARD: LedgerDashboardData = {
  monthlyData: MOCK_MONTHLY_DATA,
  categoryBreakdown: MOCK_CATEGORY_BREAKDOWN,
  currentMonthBalance: 1700.00,
  currentMonthRevenue: 3100.00,
  currentMonthCosts: 1400.00,
  profitMargin: 54.8,
  profitMarginTrend: 3.2,
  recentEntries: MOCK_RECENT_ENTRIES,
};

// ========== Helpers ==========

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 2,
  }).format(value);
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

// ========== Sub-components ==========

interface KpiCardProps {
  label: string;
  value: string;
  subValue?: string;
  icon: React.ElementType;
  color: string;
  bgColor: string;
  trend?: { value: number; label: string };
  testId: string;
  loading: boolean;
}

function KpiCard({ label, value, subValue, icon: Icon, color, bgColor, trend, testId, loading }: KpiCardProps) {
  return (
    <div
      className="rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 p-5"
      data-testid={testId}
    >
      {loading ? (
        <div className="animate-pulse space-y-3">
          <div className="h-4 w-24 bg-muted/30 rounded" />
          <div className="h-7 w-32 bg-muted/20 rounded" />
        </div>
      ) : (
        <>
          <div className="flex items-center gap-2 mb-2">
            <div className={cn('rounded-lg p-1.5', bgColor)}>
              <Icon className={cn('h-4 w-4', color)} />
            </div>
            <span className="text-sm text-zinc-500 dark:text-zinc-400">{label}</span>
          </div>
          <p className={cn('text-2xl font-bold', color)}>{value}</p>
          {subValue && (
            <p className="text-xs text-zinc-400 dark:text-zinc-500 mt-1">{subValue}</p>
          )}
          {trend && (
            <div className="flex items-center gap-1 mt-2">
              {trend.value >= 0 ? (
                <ArrowUpRightIcon className="h-3.5 w-3.5 text-emerald-500" />
              ) : (
                <ArrowDownRightIcon className="h-3.5 w-3.5 text-red-500" />
              )}
              <span
                className={cn(
                  'text-xs font-medium',
                  trend.value >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'
                )}
              >
                {trend.value >= 0 ? '+' : ''}{trend.value.toFixed(1)}% {trend.label}
              </span>
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ========== Main Component ==========

export function LedgerDashboardTab() {
  const [dashboard, setDashboard] = useState<LedgerDashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const fetchData = useCallback(async () => {
    setError(null);
    try {
      const result = await api.admin.getLedgerDashboard();
      setDashboard(result);
    } catch {
      setError('Unable to load dashboard data. Backend may not be available yet.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleRefresh = async () => {
    setRefreshing(true);
    await fetchData();
  };

  const data = dashboard ?? MOCK_DASHBOARD;

  return (
    <div className="space-y-6" data-testid="ledger-dashboard-tab">
      {/* Header with refresh */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-zinc-900 dark:text-zinc-100">
            Financial Overview
          </h2>
          <p className="text-sm text-zinc-500 dark:text-zinc-400">
            Revenue, costs, and profitability at a glance
          </p>
        </div>
        <button
          onClick={handleRefresh}
          disabled={refreshing}
          className="inline-flex items-center gap-2 rounded-lg border border-zinc-200/50 dark:border-zinc-700/50 px-3 py-2 text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
          data-testid="refresh-dashboard"
        >
          <RefreshCwIcon className={cn('h-4 w-4', refreshing && 'animate-spin')} />
          Refresh
        </button>
      </div>

      {/* Error banner */}
      {error && !loading && (
        <div className="rounded-lg border border-amber-200/50 bg-amber-50/50 dark:bg-amber-900/10 dark:border-amber-700/50 p-4 text-sm text-amber-700 dark:text-amber-300" data-testid="dashboard-error">
          {error}
        </div>
      )}

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4" data-testid="dashboard-kpi-cards">
        <KpiCard
          label="Current Month Balance"
          value={formatCurrency(data.currentMonthBalance)}
          subValue={`Revenue: ${formatCurrency(data.currentMonthRevenue)} | Costs: ${formatCurrency(data.currentMonthCosts)}`}
          icon={WalletIcon}
          color={data.currentMonthBalance >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'}
          bgColor={data.currentMonthBalance >= 0 ? 'bg-emerald-50 dark:bg-emerald-900/20' : 'bg-red-50 dark:bg-red-900/20'}
          testId="kpi-current-month-balance"
          loading={loading}
        />
        <KpiCard
          label="Profit Margin"
          value={`${data.profitMargin.toFixed(1)}%`}
          icon={PercentIcon}
          color={data.profitMargin >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'}
          bgColor={data.profitMargin >= 0 ? 'bg-emerald-50 dark:bg-emerald-900/20' : 'bg-red-50 dark:bg-red-900/20'}
          trend={{ value: data.profitMarginTrend, label: 'vs last month' }}
          testId="kpi-profit-margin"
          loading={loading}
        />
        <KpiCard
          label="Monthly Revenue"
          value={formatCurrency(data.currentMonthRevenue)}
          icon={TrendingUpIcon}
          color="text-emerald-600 dark:text-emerald-400"
          bgColor="bg-emerald-50 dark:bg-emerald-900/20"
          testId="kpi-monthly-revenue"
          loading={loading}
        />
        <KpiCard
          label="Monthly Costs"
          value={formatCurrency(data.currentMonthCosts)}
          icon={TrendingDownIcon}
          color="text-red-600 dark:text-red-400"
          bgColor="bg-red-50 dark:bg-red-900/20"
          testId="kpi-monthly-costs"
          loading={loading}
        />
      </div>

      {/* Charts row */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <RevenueVsCostsChart data={data.monthlyData} loading={loading} />
        </div>
        <div className="lg:col-span-1">
          <CategoryBreakdownChart data={data.categoryBreakdown} loading={loading} />
        </div>
      </div>

      {/* Recent entries table */}
      <div
        className="rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 p-6"
        data-testid="recent-entries-section"
      >
        <h3 className="mb-4 text-lg font-semibold text-zinc-900 dark:text-zinc-100">
          Recent Ledger Entries
        </h3>

        {loading ? (
          <div className="space-y-3">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="animate-pulse flex items-center gap-4">
                <div className="h-4 w-20 bg-muted/30 rounded" />
                <div className="h-4 w-16 bg-muted/20 rounded" />
                <div className="h-4 flex-1 bg-muted/10 rounded" />
                <div className="h-4 w-24 bg-muted/20 rounded" />
              </div>
            ))}
          </div>
        ) : data.recentEntries.length === 0 ? (
          <p className="text-sm text-zinc-500 dark:text-zinc-400 text-center py-6">
            No recent entries
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm" data-testid="recent-entries-table">
              <thead>
                <tr className="border-b border-zinc-200/50 dark:border-zinc-700/50 text-left">
                  <th className="py-2 pr-4 text-xs font-medium text-zinc-500 dark:text-zinc-400">Date</th>
                  <th className="py-2 pr-4 text-xs font-medium text-zinc-500 dark:text-zinc-400">Type</th>
                  <th className="py-2 pr-4 text-xs font-medium text-zinc-500 dark:text-zinc-400">Category</th>
                  <th className="py-2 pr-4 text-xs font-medium text-zinc-500 dark:text-zinc-400">Description</th>
                  <th className="py-2 text-xs font-medium text-zinc-500 dark:text-zinc-400 text-right">Amount</th>
                </tr>
              </thead>
              <tbody>
                {data.recentEntries.map((entry) => {
                  const typeName = LEDGER_ENTRY_TYPE_MAP[entry.type] ?? 'Unknown';
                  const categoryName = LEDGER_CATEGORY_MAP[entry.category] ?? 'Other';
                  const isIncome = entry.type === 0;

                  return (
                    <tr
                      key={entry.id}
                      className="border-b border-zinc-100 dark:border-zinc-800 last:border-0"
                      data-testid={`recent-entry-${entry.id}`}
                    >
                      <td className="py-3 pr-4 text-zinc-600 dark:text-zinc-300 whitespace-nowrap">
                        {formatDate(entry.date)}
                      </td>
                      <td className="py-3 pr-4">
                        <span
                          className={cn(
                            'rounded-full px-2 py-0.5 text-xs font-medium',
                            isIncome
                              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400'
                              : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                          )}
                        >
                          {typeName}
                        </span>
                      </td>
                      <td className="py-3 pr-4 text-zinc-600 dark:text-zinc-300">
                        {categoryName}
                      </td>
                      <td className="py-3 pr-4 text-zinc-500 dark:text-zinc-400 max-w-[200px] truncate">
                        {entry.description ?? '-'}
                      </td>
                      <td
                        className={cn(
                          'py-3 text-right font-medium whitespace-nowrap',
                          isIncome ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'
                        )}
                      >
                        {isIncome ? '+' : '-'}{formatCurrency(entry.amount)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
