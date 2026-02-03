'use client';

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { AlertCircle, ChevronDown, Info, ShieldAlert } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { useRagStrategy } from '@/hooks/useRagStrategy';
import { cn } from '@/lib/utils';

import { STRATEGIES } from './rag-data';
import { StrategyCard } from './StrategyCard';

import type { RagStrategy, UserTier } from './types';

/**
 * Props for StrategySelector component.
 */
export interface StrategySelectorProps {
  /**
   * Current user tier for access control.
   */
  userTier: UserTier;

  /**
   * Currently selected strategy.
   */
  selectedStrategy: RagStrategy | null;

  /**
   * Callback when a strategy is selected.
   */
  onStrategySelect: (strategy: RagStrategy) => void;

  /**
   * Callback when upgrade is requested for a locked strategy.
   */
  onUpgradeRequest?: (strategy: RagStrategy, requiredTier: UserTier) => void;

  /**
   * Whether to show as compact dropdown or expanded cards.
   */
  variant?: 'cards' | 'dropdown';

  /**
   * Whether to show strategy details (tokens, latency, accuracy).
   */
  showDetails?: boolean;

  /**
   * Whether the selector is disabled.
   */
  disabled?: boolean;

  /**
   * Additional CSS classes.
   */
  className?: string;

  /**
   * Title for the selector.
   */
  title?: string;

  /**
   * Description for the selector.
   */
  description?: string;
}

/**
 * All available strategies in order.
 */
const ALL_STRATEGIES: RagStrategy[] = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'];

/**
 * StrategySelector Component
 *
 * A tier-aware strategy selector that shows only available strategies for the user's tier.
 * Locked strategies are displayed with upgrade messaging.
 *
 * Implements the architecture principle: Tier → Available Strategies
 *
 * Features:
 * - Tier-based filtering of available strategies
 * - Disabled strategies with tooltip explanations
 * - Visual indicators for max available strategy
 * - Upgrade messaging for locked strategies
 * - Keyboard navigation support
 * - Two display variants: cards or dropdown
 */
export function StrategySelector({
  userTier,
  selectedStrategy,
  onStrategySelect,
  onUpgradeRequest,
  variant = 'cards',
  showDetails = true,
  disabled = false,
  className,
  title = 'Select Strategy',
  description = 'Choose a RAG strategy based on your query complexity',
}: StrategySelectorProps) {
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const { getAllStrategiesWithAccess, hasRagAccess, getStrategyData } = useRagStrategy();

  const strategiesWithAccess = useMemo(
    () => getAllStrategiesWithAccess(userTier),
    [getAllStrategiesWithAccess, userTier]
  );

  const availableStrategies = useMemo(
    () => strategiesWithAccess.filter((s) => s.isAvailable).map((s) => s.strategy),
    [strategiesWithAccess]
  );

  const hasAccess = hasRagAccess(userTier);

  // Close dropdown when clicking outside
  useEffect(() => {
    if (!isDropdownOpen) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsDropdownOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isDropdownOpen]);

  // Close dropdown on Escape key
  useEffect(() => {
    if (!isDropdownOpen) return;

    const handleEscapeKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsDropdownOpen(false);
      }
    };

    document.addEventListener('keydown', handleEscapeKey);
    return () => document.removeEventListener('keydown', handleEscapeKey);
  }, [isDropdownOpen]);

  // Close dropdown when disabled
  useEffect(() => {
    if (disabled && isDropdownOpen) {
      setIsDropdownOpen(false);
    }
  }, [disabled, isDropdownOpen]);

  const handleStrategySelect = useCallback(
    (strategy: RagStrategy) => {
      if (!disabled && hasAccess) {
        onStrategySelect(strategy);
      }
    },
    [disabled, hasAccess, onStrategySelect]
  );

  const handleUpgradeClick = useCallback(
    (strategy: RagStrategy, requiredTier: UserTier) => {
      if (onUpgradeRequest) {
        onUpgradeRequest(strategy, requiredTier);
      }
    },
    [onUpgradeRequest]
  );

  // No access state
  if (!hasAccess) {
    return (
      <Card className={cn('border-destructive/50', className)}>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-destructive">
            <ShieldAlert className="h-5 w-5" />
            Authentication Required
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-start gap-3 p-4 rounded-lg bg-destructive/10">
            <AlertCircle className="h-5 w-5 text-destructive flex-shrink-0 mt-0.5" />
            <div className="space-y-2">
              <p className="text-sm font-medium">
                Anonymous users cannot access the RAG system.
              </p>
              <p className="text-xs text-muted-foreground">
                Please sign in to select a strategy and use the RAG capabilities.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  // Dropdown variant
  if (variant === 'dropdown') {
    return (
      <TooltipProvider>
        <div ref={dropdownRef} className={cn('relative', className)}>
          <Button
            variant="outline"
            role="combobox"
            aria-expanded={isDropdownOpen}
            aria-haspopup="listbox"
            disabled={disabled}
            className="w-full justify-between"
            onClick={() => setIsDropdownOpen(!isDropdownOpen)}
          >
            {selectedStrategy ? (
              <span className="flex items-center gap-2">
                <span>{STRATEGIES[selectedStrategy].icon}</span>
                {selectedStrategy}
              </span>
            ) : (
              'Select a strategy...'
            )}
            <ChevronDown
              className={cn(
                'h-4 w-4 transition-transform',
                isDropdownOpen && 'rotate-180'
              )}
            />
          </Button>

          {isDropdownOpen && (
            <div
              className="absolute z-50 w-full mt-1 py-1 bg-popover border rounded-md shadow-lg"
              role="listbox"
              aria-label="RAG Strategies"
            >
              {ALL_STRATEGIES.map((strategyId) => {
                const accessInfo = strategiesWithAccess.find(
                  (s) => s.strategy === strategyId
                );
                const strategyData = STRATEGIES[strategyId];
                const isAvailable = accessInfo?.isAvailable ?? false;

                return (
                  <Tooltip key={strategyId}>
                    <TooltipTrigger asChild>
                      <button
                        role="option"
                        aria-selected={selectedStrategy === strategyId}
                        aria-disabled={!isAvailable}
                        className={cn(
                          'w-full px-3 py-2 text-left text-sm flex items-center justify-between',
                          'transition-colors',
                          isAvailable
                            ? 'hover:bg-accent cursor-pointer'
                            : 'opacity-50 cursor-not-allowed',
                          selectedStrategy === strategyId && 'bg-accent'
                        )}
                        onClick={() => {
                          if (isAvailable) {
                            handleStrategySelect(strategyId);
                            setIsDropdownOpen(false);
                          }
                        }}
                        disabled={!isAvailable}
                      >
                        <span className="flex items-center gap-2">
                          <span>{strategyData.icon}</span>
                          <span>{strategyId}</span>
                        </span>
                        {!isAvailable && accessInfo?.requiredTier && (
                          <Badge variant="secondary" className="text-xs">
                            {accessInfo.requiredTier}
                          </Badge>
                        )}
                      </button>
                    </TooltipTrigger>
                    {!isAvailable && accessInfo?.requiredTier && (
                      <TooltipContent side="right">
                        <p>Requires {accessInfo.requiredTier} tier</p>
                      </TooltipContent>
                    )}
                  </Tooltip>
                );
              })}
            </div>
          )}
        </div>
      </TooltipProvider>
    );
  }

  // Cards variant (default)
  return (
    <Card className={className}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              {title}
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Info className="h-4 w-4 text-muted-foreground cursor-help" />
                  </TooltipTrigger>
                  <TooltipContent side="right" className="max-w-xs">
                    <p className="font-semibold mb-1">Tier-Based Access</p>
                    <p className="text-sm">
                      Your tier ({userTier}) has access to {availableStrategies.length} strategies.
                      Locked strategies require a higher tier.
                    </p>
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            </CardTitle>
            <CardDescription>{description}</CardDescription>
          </div>
          <Badge variant="outline" className="ml-2">
            {userTier} Tier
          </Badge>
        </div>
      </CardHeader>
      <CardContent>
        <div
          className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4"
          role="listbox"
          aria-label="RAG Strategies"
        >
          {ALL_STRATEGIES.map((strategyId) => {
            const accessInfo = strategiesWithAccess.find((s) => s.strategy === strategyId);
            const strategyData = getStrategyData(strategyId);

            if (!strategyData) return null;

            return (
              <StrategyCard
                key={strategyId}
                strategy={strategyData}
                isAvailable={accessInfo?.isAvailable ?? false}
                isSelected={selectedStrategy === strategyId}
                requiredTier={accessInfo?.requiredTier}
                onSelect={handleStrategySelect}
                onUpgradeClick={handleUpgradeClick}
                showDetails={showDetails}
              />
            );
          })}
        </div>

        {/* Summary section */}
        <div className="mt-6 pt-4 border-t flex items-center justify-between text-sm">
          <div className="text-muted-foreground">
            <span className="font-medium text-foreground">{availableStrategies.length}</span> of{' '}
            {ALL_STRATEGIES.length} strategies available
          </div>
          {selectedStrategy && (
            <div className="flex items-center gap-2">
              <span className="text-muted-foreground">Selected:</span>
              <Badge variant="default" className="font-medium">
                {STRATEGIES[selectedStrategy].icon} {selectedStrategy}
              </Badge>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
