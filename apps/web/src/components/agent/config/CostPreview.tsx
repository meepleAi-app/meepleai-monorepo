/**
 * Cost Preview - Display estimated cost before agent launch
 * Issue #3376
 *
 * Shows cost breakdown based on selected strategy and model
 */

'use client';

import { Calculator, AlertTriangle, TrendingUp } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useAgentStore } from '@/stores/agentStore';

import type { ModelTier } from './ModelTierSelector';
import type { RagStrategy } from './StrategySelector';

interface CostEstimate {
  perQuery: number;
  estimatedDaily: number;
  estimatedMonthly: number;
}

// Cost multipliers by strategy
const strategyMultipliers: Record<RagStrategy, number> = {
  FAST: 1.0,
  BALANCED: 1.5,
  PRECISE: 2.5,
  EXPERT: 5.0,
  CONSENSUS: 10.0,
  CUSTOM: 3.0,
};

// Base costs by model tier (per 1K tokens)
const tierBaseCosts: Record<ModelTier, number> = {
  free: 0.0005,
  normal: 0.001,
  premium: 0.005,
  custom: 0.003,
};

interface CostPreviewProps {
  className?: string;
  estimatedQueriesPerDay?: number;
}

export function CostPreview({ className, estimatedQueriesPerDay = 10 }: CostPreviewProps) {
  const { selectedStrategyId, selectedTierId, selectedModelId } = useAgentStore();

  // Calculate cost estimate
  const calculateCost = (): CostEstimate | null => {
    if (!selectedStrategyId || !selectedTierId) {
      return null;
    }

    const baseCost = tierBaseCosts[selectedTierId as ModelTier] || 0.001;
    const multiplier = strategyMultipliers[selectedStrategyId as RagStrategy] || 1.0;

    const perQuery = baseCost * multiplier;
    const estimatedDaily = perQuery * estimatedQueriesPerDay;
    const estimatedMonthly = estimatedDaily * 30;

    return {
      perQuery,
      estimatedDaily,
      estimatedMonthly,
    };
  };

  const cost = calculateCost();

  // Determine warning level
  const getWarningLevel = (monthly: number): 'low' | 'medium' | 'high' | null => {
    if (monthly < 1) return 'low';
    if (monthly < 10) return 'medium';
    if (monthly >= 10) return 'high';
    return null;
  };

  const warningLevel = cost ? getWarningLevel(cost.estimatedMonthly) : null;

  if (!selectedStrategyId && !selectedTierId && !selectedModelId) {
    return null;
  }

  return (
    <div
      className={cn(
        'rounded-lg border p-4',
        warningLevel === 'high'
          ? 'border-red-500/50 bg-red-500/10'
          : warningLevel === 'medium'
            ? 'border-yellow-500/50 bg-yellow-500/10'
            : 'border-slate-700 bg-slate-800/50',
        className
      )}
    >
      <div className="flex items-center gap-2 mb-3">
        <Calculator className="h-4 w-4 text-slate-400" />
        <span className="text-sm font-medium text-slate-200">Cost Preview</span>
        {warningLevel === 'high' && (
          <AlertTriangle className="h-4 w-4 text-red-400 ml-auto" />
        )}
      </div>

      {cost ? (
        <div className="space-y-2">
          <div className="flex justify-between items-center">
            <span className="text-sm text-slate-400">Per query</span>
            <span className="text-lg font-bold text-white">
              ${cost.perQuery.toFixed(4)}
            </span>
          </div>

          <div className="flex justify-between items-center text-sm">
            <span className="text-slate-500">
              Est. daily ({estimatedQueriesPerDay} queries)
            </span>
            <span className="text-slate-300">${cost.estimatedDaily.toFixed(2)}</span>
          </div>

          <div className="flex justify-between items-center text-sm">
            <span className="text-slate-500">Est. monthly</span>
            <span
              className={cn(
                'font-medium',
                warningLevel === 'high'
                  ? 'text-red-400'
                  : warningLevel === 'medium'
                    ? 'text-yellow-400'
                    : 'text-green-400'
              )}
            >
              ${cost.estimatedMonthly.toFixed(2)}
            </span>
          </div>

          {warningLevel === 'high' && (
            <div className="mt-3 flex items-start gap-2 text-xs text-red-300">
              <AlertTriangle className="h-3 w-3 mt-0.5 flex-shrink-0" />
              <span>
                High estimated cost. Consider using BALANCED strategy or a lower-tier model.
              </span>
            </div>
          )}

          {warningLevel === 'medium' && (
            <div className="mt-3 flex items-start gap-2 text-xs text-yellow-300">
              <TrendingUp className="h-3 w-3 mt-0.5 flex-shrink-0" />
              <span>
                Moderate cost. Monitor usage to stay within budget.
              </span>
            </div>
          )}
        </div>
      ) : (
        <div className="text-sm text-slate-500 italic">
          Select strategy and model tier to see cost estimate
        </div>
      )}
    </div>
  );
}
