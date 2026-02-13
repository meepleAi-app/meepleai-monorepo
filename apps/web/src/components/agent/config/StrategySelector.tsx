/**
 * Strategy Selector - RAG strategy selection component
 * Issue #3: Refactored to use backend API (Issue #8)
 *
 * Displays all 12 available strategies from backend with filtering by user tier.
 * Fetches from GET /api/v1/rag/strategies (created in Issue #8).
 */

'use client';

import React from 'react';
import { Check, Loader2, AlertCircle } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useRagStrategies } from '@/hooks/queries/useRagStrategies';
import type { RagStrategyDto } from '@/lib/api/rag-strategies.api';

export interface StrategySelectorProps {
  /** Currently selected strategy name */
  value?: string;
  /** Callback when strategy changes (optional - uses internal store if not provided) */
  onChange?: (strategyName: string) => void;
  /** User's subscription tier for filtering (Issue #9) */
  userTier?: 'Free' | 'Basic' | 'Pro' | 'Enterprise';
  /** Whether the selector is disabled */
  disabled?: boolean;
  /** Optional placeholder text */
  placeholder?: string;
  /** Optional className for styling */
  className?: string;
}

/**
 * Filter strategies based on user tier (Issue #9)
 */
function filterByTier(
  strategies: RagStrategyDto[],
  tier: 'Free' | 'Basic' | 'Pro' | 'Enterprise' | undefined
): RagStrategyDto[] {
  if (!tier || tier === 'Enterprise') {
    // Enterprise gets all strategies
    return strategies;
  }

  return strategies.filter(s => {
    // Custom always requires Enterprise (admin)
    if (s.requiresAdmin) return false;

    switch (tier) {
      case 'Free':
        // Only simplest (complexity 0-1: Fast, Balanced)
        return s.complexity <= 1;

      case 'Basic':
        // Simple + medium (0-5: Fast, Balanced, SentenceWindow)
        // + selected advanced (9-10: StepBack, QueryExpansion)
        return s.complexity <= 5 ||
               s.name === 'StepBack' ||
               s.name === 'QueryExpansion';

      case 'Pro':
        // All except Custom (already filtered by requiresAdmin)
        return true;

      default:
        return s.complexity <= 1;
    }
  });
}

export function StrategySelector({
  value: controlledValue,
  onChange,
  userTier,
  disabled = false,
  placeholder = 'Select RAG strategy...',
  className,
}: StrategySelectorProps) {
  const { data: allStrategies, isLoading, error } = useRagStrategies();

  // Use internal state if not controlled (backward compatibility)
  const [internalValue, setInternalValue] = React.useState<string | undefined>();

  const value = controlledValue ?? internalValue;
  const handleChange = React.useCallback((newValue: string) => {
    if (onChange) {
      onChange(newValue);
    } else {
      setInternalValue(newValue);
    }
  }, [onChange]);

  // Filter by user tier
  const strategies = allStrategies ? filterByTier(allStrategies, userTier) : [];

  // Find selected strategy
  const selectedStrategy = strategies.find(s => s.name === value);

  if (error) {
    return (
      <div className="space-y-2">
        <label className="block text-sm font-medium">
          RAG Strategy
          <span className="ml-1 text-red-500">*</span>
        </label>
        <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg flex items-center gap-2">
          <AlertCircle className="h-4 w-4 text-red-600 dark:text-red-400" />
          <p className="text-sm text-red-700 dark:text-red-300">
            Failed to load strategies. Please try again.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <label className="block text-sm font-medium">
        RAG Strategy
        <span className="ml-1 text-red-500">*</span>
      </label>

      <Select
        value={value}
        onValueChange={handleChange}
        disabled={disabled || isLoading}
      >
        <SelectTrigger className={className} aria-label="Select RAG strategy">
          {isLoading ? (
            <div className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-muted-foreground">Loading strategies...</span>
            </div>
          ) : selectedStrategy ? (
            <span>{selectedStrategy.displayName}</span>
          ) : (
            <SelectValue placeholder={placeholder} />
          )}
        </SelectTrigger>

        <SelectContent>
          {strategies.length > 0 ? (
            strategies.map((strategy) => {
              const isSelected = value === strategy.name;
              const isRestricted = userTier && !filterByTier([strategy], userTier).length;

              return (
                <SelectItem
                  key={strategy.name}
                  value={strategy.name}
                  disabled={isRestricted}
                  className="cursor-pointer"
                >
                  <div className="flex flex-col gap-1 py-1">
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-medium">
                        {strategy.displayName}
                        {isRestricted && (
                          <span className="ml-2 text-xs text-muted-foreground">
                            🔒 {userTier === 'Free' ? 'Basic+' : 'Pro+'}
                          </span>
                        )}
                      </span>
                      <div className="flex items-center gap-1">
                        <span className="text-xs text-muted-foreground">
                          {strategy.estimatedTokens.toLocaleString()}t
                        </span>
                        {isSelected && (
                          <Check className="h-4 w-4 text-green-600 dark:text-green-400" />
                        )}
                      </div>
                    </div>
                    <span className="text-xs text-muted-foreground">
                      {strategy.description}
                    </span>
                    <span className="text-xs text-muted-foreground italic">
                      {strategy.useCase}
                    </span>
                  </div>
                </SelectItem>
              );
            })
          ) : (
            <div className="p-3 text-sm text-muted-foreground text-center">
              No strategies available
            </div>
          )}
        </SelectContent>
      </Select>

      {selectedStrategy && (
        <div className="p-2 bg-muted/50 rounded text-xs text-muted-foreground">
          <strong>Complexity {selectedStrategy.complexity}</strong> •{' '}
          ~{(selectedStrategy.estimatedTokens / 1000).toFixed(1)}K tokens/query •{' '}
          {selectedStrategy.useCase}
        </div>
      )}

      {userTier && userTier !== 'Enterprise' && (
        <p className="text-xs text-muted-foreground">
          {userTier === 'Free'
            ? 'Free tier: Fast and Balanced strategies only. '
            : userTier === 'Basic'
              ? 'Basic tier: Simple and selected advanced strategies. '
              : 'Pro tier: All strategies except Custom. '}
          <a href="/pricing" className="text-primary hover:underline">
            Upgrade
          </a>{' '}
          for more options.
        </p>
      )}
    </div>
  );
}
