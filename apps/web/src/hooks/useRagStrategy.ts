'use client';

import { useMemo, useCallback } from 'react';

import { TIER_STRATEGY_ACCESS, STRATEGIES } from '@/components/rag-dashboard/rag-data';
import type { RagStrategy, UserTier } from '@/components/rag-dashboard/types';

/**
 * Strategy access information for UI display.
 */
export interface StrategyAccessInfo {
  strategy: RagStrategy;
  isAvailable: boolean;
  requiredTier: UserTier | null;
  tierBadge: string | null;
}

/**
 * Hook return type.
 */
export interface UseRagStrategyReturn {
  /**
   * Get all strategies available for a tier.
   */
  getAvailableStrategies: (tier: UserTier) => RagStrategy[];

  /**
   * Check if a specific strategy is available for a tier.
   */
  isStrategyAvailable: (tier: UserTier, strategy: RagStrategy) => boolean;

  /**
   * Get the minimum required tier for a strategy.
   */
  getRequiredTier: (strategy: RagStrategy) => UserTier | null;

  /**
   * Check if a tier has any RAG access.
   */
  hasRagAccess: (tier: UserTier) => boolean;

  /**
   * Get strategy access info for UI display.
   */
  getStrategyAccessInfo: (tier: UserTier, strategy: RagStrategy) => StrategyAccessInfo;

  /**
   * Get all strategies with their access info for a tier.
   */
  getAllStrategiesWithAccess: (tier: UserTier) => StrategyAccessInfo[];

  /**
   * Get strategy data from rag-data.ts.
   */
  getStrategyData: (strategy: RagStrategy) => typeof STRATEGIES[keyof typeof STRATEGIES] | undefined;
}

/**
 * Tier hierarchy for determining minimum required tier.
 * Lower index = lower access level.
 */
const TIER_HIERARCHY: UserTier[] = ['Anonymous', 'User', 'Editor', 'Admin', 'Premium'];

/**
 * All available RAG strategies.
 */
const ALL_STRATEGIES: RagStrategy[] = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'];

/**
 * Hook for RAG strategy access control based on user tier.
 *
 * Implements the architecture principle: Tier → Available Strategies → Model Selection
 * The tier determines WHICH strategies are available, NOT which model to use.
 *
 * @see apps/web/src/components/rag-dashboard/rag-data.ts for TIER_STRATEGY_ACCESS
 */
export function useRagStrategy(): UseRagStrategyReturn {
  /**
   * Get available strategies for a tier.
   */
  const getAvailableStrategies = useCallback((tier: UserTier): RagStrategy[] => {
    const tierAccess = TIER_STRATEGY_ACCESS.find((t) => t.tier === tier);

    if (!tierAccess || !tierAccess.hasRagAccess) {
      return [];
    }

    return tierAccess.availableStrategies;
  }, []);

  /**
   * Check if a strategy is available for a tier.
   */
  const isStrategyAvailable = useCallback(
    (tier: UserTier, strategy: RagStrategy): boolean => {
      const available = getAvailableStrategies(tier);
      return available.includes(strategy);
    },
    [getAvailableStrategies]
  );

  /**
   * Get the minimum required tier for a strategy.
   * Returns the lowest tier that has access to the strategy.
   */
  const getRequiredTier = useCallback((strategy: RagStrategy): UserTier | null => {
    for (const tier of TIER_HIERARCHY) {
      const tierAccess = TIER_STRATEGY_ACCESS.find((t) => t.tier === tier);

      if (tierAccess?.availableStrategies.includes(strategy)) {
        return tier as UserTier;
      }
    }

    return null; // Strategy not available to any tier
  }, []);

  /**
   * Check if a tier has RAG access.
   */
  const hasRagAccess = useCallback((tier: UserTier): boolean => {
    const tierAccess = TIER_STRATEGY_ACCESS.find((t) => t.tier === tier);
    return tierAccess?.hasRagAccess ?? false;
  }, []);

  /**
   * Get strategy access info for UI display.
   */
  const getStrategyAccessInfo = useCallback(
    (tier: UserTier, strategy: RagStrategy): StrategyAccessInfo => {
      const isAvailable = isStrategyAvailable(tier, strategy);
      const requiredTier = isAvailable ? null : getRequiredTier(strategy);

      return {
        strategy,
        isAvailable,
        requiredTier,
        tierBadge: requiredTier ? `Requires ${requiredTier}` : null,
      };
    },
    [isStrategyAvailable, getRequiredTier]
  );

  /**
   * Get all strategies with their access info for a tier.
   */
  const getAllStrategiesWithAccess = useCallback(
    (tier: UserTier): StrategyAccessInfo[] => {
      return ALL_STRATEGIES.map((strategy) => getStrategyAccessInfo(tier, strategy));
    },
    [getStrategyAccessInfo]
  );

  /**
   * Get strategy data from rag-data.ts.
   */
  const getStrategyData = useCallback(
    (strategy: RagStrategy) => {
      // eslint-disable-next-line security/detect-object-injection -- Type-safe: strategy is RagStrategy enum
      return STRATEGIES[strategy];
    },
    []
  );

  return useMemo(
    () => ({
      getAvailableStrategies,
      isStrategyAvailable,
      getRequiredTier,
      hasRagAccess,
      getStrategyAccessInfo,
      getAllStrategiesWithAccess,
      getStrategyData,
    }),
    [
      getAvailableStrategies,
      isStrategyAvailable,
      getRequiredTier,
      hasRagAccess,
      getStrategyAccessInfo,
      getAllStrategiesWithAccess,
      getStrategyData,
    ]
  );
}
