/**
 * KPICardsGrid Component - Issue #2785
 *
 * Grid layout for KPI cards with responsive columns:
 * - Mobile: 1 column
 * - Tablet: 2 columns
 * - Desktop: 4 columns
 *
 * Includes helper function to map DashboardMetrics to KPICardData.
 *
 * Part of Epic #2783 - Admin Dashboard Redesign
 */

'use client';

import { Users, Activity, Gamepad2, Zap } from 'lucide-react';

import type { DashboardMetrics } from '@/lib/api';
import { cn } from '@/lib/utils';

import { KPICard, type KPICardData } from './KPICard';

// ============================================================================
// Types
// ============================================================================

export interface KPICardsGridProps {
  /** Array of KPI card data to display */
  cards: KPICardData[];
  /** Additional CSS classes */
  className?: string;
  /** Test ID for testing */
  'data-testid'?: string;
}

export interface BuildKPICardsOptions {
  /** Number of games pending approval */
  pendingGamesCount?: number;
  /** User trend percentage (vs last period) */
  userTrendPercent?: number;
  /** Games trend count (new this week) */
  gamesTrendCount?: number;
  /** Estimated AI cost in EUR */
  estimatedAiCostEur?: number;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Calculate trend percentage from time series data
 *
 * @param trendData Array of {date, value} points
 * @param periodDays Number of days to compare (default: 7)
 * @returns Percentage change or undefined if insufficient data
 */
export function calculateTrendPercent(
  trendData: Array<{ date: string; value: number }> | undefined,
  periodDays: number = 7
): number | undefined {
  if (!trendData || trendData.length < periodDays * 2) {
    return undefined;
  }

  // Get current period (last N days)
  const currentPeriod = trendData.slice(-periodDays);
  // Get previous period (N days before that)
  const previousPeriod = trendData.slice(-periodDays * 2, -periodDays);

  const currentSum = currentPeriod.reduce((sum, point) => sum + point.value, 0);
  const previousSum = previousPeriod.reduce((sum, point) => sum + point.value, 0);

  if (previousSum === 0) {
    return currentSum > 0 ? 100 : 0;
  }

  return Math.round(((currentSum - previousSum) / previousSum) * 100);
}

/**
 * Estimate AI cost based on token usage
 *
 * Pricing assumptions (approximate):
 * - Claude: $0.015 per 1K tokens (average)
 * - GPT-4: $0.03 per 1K tokens (average)
 * - Local: $0 per token
 *
 * Using blended rate of ~$0.012 per 1K tokens
 *
 * @param totalTokens Total tokens used
 * @returns Estimated cost in EUR
 */
export function estimateAiCost(totalTokens: number): number {
  const costPerThousandTokens = 0.012; // USD
  const usdToEur = 0.92; // Approximate exchange rate
  const costUsd = (totalTokens / 1000) * costPerThousandTokens;
  return Math.round(costUsd * usdToEur * 100) / 100; // Round to 2 decimals
}

/**
 * Build KPI cards array from dashboard metrics
 *
 * Maps DashboardMetrics to the 4 main KPI cards:
 * 1. Utenti Totali - with trend %
 * 2. Sessioni Attive - real-time
 * 3. Giochi in Catalogo - with pending badge
 * 4. Richieste AI Oggi - with cost estimate
 *
 * @param metrics Dashboard metrics from API
 * @param options Additional options for trends and badges
 * @returns Array of KPICardData
 */
export function buildKPICards(
  metrics: DashboardMetrics | null,
  options: BuildKPICardsOptions = {}
): KPICardData[] {
  const {
    pendingGamesCount = 0,
    userTrendPercent,
    gamesTrendCount,
    estimatedAiCostEur,
  } = options;

  if (!metrics) {
    // Return placeholder cards when no data
    return [
      {
        title: 'Utenti Totali',
        value: '-',
        icon: <Users className="h-5 w-5" />,
      },
      {
        title: 'Sessioni Attive',
        value: '-',
        icon: <Activity className="h-5 w-5" />,
        subtitle: 'in tempo reale',
      },
      {
        title: 'Giochi in Catalogo',
        value: '-',
        icon: <Gamepad2 className="h-5 w-5" />,
      },
      {
        title: 'Richieste AI Oggi',
        value: '-',
        icon: <Zap className="h-5 w-5" />,
      },
    ];
  }

  // Calculate AI cost if not provided
  const aiCost = estimatedAiCostEur ?? estimateAiCost(metrics.totalTokensUsed);

  return [
    {
      title: 'Utenti Totali',
      value: metrics.totalUsers.toLocaleString('it-IT'),
      trend: userTrendPercent,
      trendLabel: userTrendPercent !== undefined ? 'vs mese scorso' : undefined,
      icon: <Users className="h-5 w-5" />,
    },
    {
      title: 'Sessioni Attive',
      value: metrics.activeSessions.toLocaleString('it-IT'),
      icon: <Activity className="h-5 w-5" />,
      subtitle: 'in tempo reale',
    },
    {
      title: 'Giochi in Catalogo',
      value: metrics.totalGames.toLocaleString('it-IT'),
      trend: gamesTrendCount,
      trendLabel: gamesTrendCount !== undefined ? 'nuovi questa settimana' : undefined,
      icon: <Gamepad2 className="h-5 w-5" />,
      badge: pendingGamesCount > 0 ? `${pendingGamesCount} in attesa` : undefined,
      badgeVariant: pendingGamesCount > 0 ? 'warning' : undefined,
    },
    {
      title: 'Richieste AI Oggi',
      value: metrics.apiRequestsToday.toLocaleString('it-IT'),
      icon: <Zap className="h-5 w-5" />,
      subtitle: `~€${aiCost.toFixed(2)} stimati`,
    },
  ];
}

// ============================================================================
// Component
// ============================================================================

export function KPICardsGrid({
  cards,
  className,
  'data-testid': testId,
}: KPICardsGridProps) {
  return (
    <div
      className={cn(
        'grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4',
        className
      )}
      data-testid={testId}
    >
      {cards.map((card, index) => (
        <KPICard
          key={`${card.title}-${index}`}
          {...card}
          data-testid={testId ? `${testId}-card-${index}` : undefined}
        />
      ))}
    </div>
  );
}

export default KPICardsGrid;
