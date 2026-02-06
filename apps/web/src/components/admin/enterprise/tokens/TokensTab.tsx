/**
 * TokensTab Component
 * Issue #3692 - Token Management System
 *
 * Main token management dashboard for the Resources > Tokens tab.
 * Combines: BalanceCard, ConsumptionChart, TierUsageTable,
 * TopConsumers, and AddCreditsModal.
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { PlusIcon, DownloadIcon, RefreshCwIcon } from 'lucide-react';

import { api } from '@/lib/api';
import type {
  TokenBalance,
  TokenConsumptionData,
  TierUsage,
  TopConsumer,
} from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

import { AddCreditsModal } from './AddCreditsModal';
import { ConsumptionChart } from './ConsumptionChart';
import { TierUsageTable } from './TierUsageTable';
import { TokenBalanceCard } from './TokenBalanceCard';
import { TopConsumersTable } from './TopConsumersTable';

export function TokensTab() {
  const [balance, setBalance] = useState<TokenBalance | null>(null);
  const [consumption, setConsumption] = useState<TokenConsumptionData | null>(null);
  const [tierUsage, setTierUsage] = useState<TierUsage[]>([]);
  const [topConsumers, setTopConsumers] = useState<TopConsumer[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreditsModal, setShowCreditsModal] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  const fetchData = useCallback(async () => {
    setError(null);
    try {
      const [balanceData, consumptionData, tierData, consumerData] = await Promise.allSettled([
        api.admin.getTokenBalance(),
        api.admin.getTokenConsumption(30),
        api.admin.getTokenTierUsage(),
        api.admin.getTopConsumers(10),
      ]);

      if (balanceData.status === 'fulfilled') setBalance(balanceData.value);
      if (consumptionData.status === 'fulfilled') setConsumption(consumptionData.value);
      if (tierData.status === 'fulfilled') setTierUsage(tierData.value.tiers);
      if (consumerData.status === 'fulfilled') setTopConsumers(consumerData.value.consumers);

      // Check if all failed
      const allFailed = [balanceData, consumptionData, tierData, consumerData]
        .every((r) => r.status === 'rejected');
      if (allFailed) {
        setError('Unable to load token data. Backend endpoints may not be available yet.');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load token data');
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

  const handleAddCredits = async (amount: number, note?: string) => {
    await api.admin.addTokenCredits({ amount, currency: 'EUR', note });
    await fetchData();
  };

  // Use mock data when API is not available
  const displayBalance = balance ?? {
    currentBalance: 450,
    totalBudget: 1000,
    currency: 'EUR',
    usagePercent: 45,
    projectedDaysUntilDepletion: 12,
    lastUpdated: new Date().toISOString(),
  };

  const displayConsumption = consumption ?? {
    points: generateMockConsumptionPoints(30),
    totalTokens: 156000,
    totalCost: 23.40,
    avgDailyTokens: 5200,
    avgDailyCost: 0.78,
  };

  const displayTiers = tierUsage.length > 0 ? tierUsage : [
    { tier: 'Free' as const, limitPerMonth: 10000, currentUsage: 2300000, userCount: 2500, usagePercent: 23 },
    { tier: 'Basic' as const, limitPerMonth: 50000, currentUsage: 1200000, userCount: 300, usagePercent: 48 },
    { tier: 'Pro' as const, limitPerMonth: 200000, currentUsage: 800000, userCount: 45, usagePercent: 67 },
    { tier: 'Enterprise' as const, limitPerMonth: 0, currentUsage: 1500000, userCount: 2, usagePercent: 0 },
  ];

  const displayConsumers = topConsumers.length > 0 ? topConsumers : [
    { userId: 'user-1', displayName: 'Alice Demo', email: 'alice@example.com', tier: 'Pro' as const, tokensUsed: 185000, percentOfTierLimit: 92.5 },
    { userId: 'user-2', displayName: 'Bob Test', email: 'bob@example.com', tier: 'Pro' as const, tokensUsed: 142000, percentOfTierLimit: 71 },
    { userId: 'user-3', displayName: 'Charlie Dev', email: 'charlie@example.com', tier: 'Basic' as const, tokensUsed: 48000, percentOfTierLimit: 96 },
    { userId: 'user-4', displayName: 'Diana Admin', email: 'diana@example.com', tier: 'Enterprise' as const, tokensUsed: 1200000, percentOfTierLimit: 0 },
    { userId: 'user-5', displayName: 'Eve User', email: 'eve@example.com', tier: 'Free' as const, tokensUsed: 9800, percentOfTierLimit: 98 },
  ];

  return (
    <div className="space-y-6" data-testid="tokens-tab">
      {/* Action bar */}
      <div className="flex items-center justify-end gap-2">
        <button
          onClick={handleRefresh}
          disabled={refreshing}
          className="inline-flex items-center gap-2 rounded-lg border border-zinc-200/50 dark:border-zinc-700/50 px-3 py-2 text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
          data-testid="refresh-tokens"
        >
          <RefreshCwIcon className={cn('h-4 w-4', refreshing && 'animate-spin')} />
          Refresh
        </button>
        <button
          onClick={() => setShowCreditsModal(true)}
          className="inline-flex items-center gap-2 rounded-lg bg-amber-500 hover:bg-amber-600 px-4 py-2 text-sm font-medium text-white transition-colors"
          data-testid="add-credits-btn"
        >
          <PlusIcon className="h-4 w-4" />
          Add Credits
        </button>
        <button
          className="inline-flex items-center gap-2 rounded-lg border border-zinc-200/50 dark:border-zinc-700/50 px-3 py-2 text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
          data-testid="export-report"
        >
          <DownloadIcon className="h-4 w-4" />
          Export Report
        </button>
      </div>

      {/* Error banner */}
      {error && !loading && (
        <div className="rounded-lg border border-amber-200/50 bg-amber-50/50 dark:bg-amber-900/10 dark:border-amber-700/50 p-4 text-sm text-amber-700 dark:text-amber-300" data-testid="tokens-error">
          {error}
        </div>
      )}

      {/* Balance + Consumption (2-column on large screens) */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <TokenBalanceCard
          currentBalance={displayBalance.currentBalance}
          totalBudget={displayBalance.totalBudget}
          currency={displayBalance.currency}
          usagePercent={displayBalance.usagePercent}
          projectedDaysUntilDepletion={displayBalance.projectedDaysUntilDepletion}
          loading={loading}
        />
        <ConsumptionChart
          points={displayConsumption.points}
          totalTokens={displayConsumption.totalTokens}
          avgDailyTokens={displayConsumption.avgDailyTokens}
          avgDailyCost={displayConsumption.avgDailyCost}
          loading={loading}
        />
      </div>

      {/* Tier table (full width) */}
      <TierUsageTable
        tiers={displayTiers}
        onEditTier={() => {}}
        loading={loading}
      />

      {/* Top consumers */}
      <TopConsumersTable
        consumers={displayConsumers}
        loading={loading}
      />

      {/* Add Credits Modal */}
      <AddCreditsModal
        open={showCreditsModal}
        onClose={() => setShowCreditsModal(false)}
        onSubmit={handleAddCredits}
      />
    </div>
  );
}

/**
 * Generate mock consumption data points for development
 */
function generateMockConsumptionPoints(days: number) {
  const points = [];
  for (let i = days - 1; i >= 0; i--) {
    const date = new Date();
    date.setDate(date.getDate() - i);
    const baseTokens = 4000 + Math.random() * 3000;
    points.push({
      date: date.toISOString().slice(0, 10),
      tokens: Math.round(baseTokens),
      cost: parseFloat((baseTokens * 0.00015).toFixed(4)),
    });
  }
  return points;
}
