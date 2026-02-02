'use client';

import React, { forwardRef } from 'react';

import { Lock, Info, Zap, Scale, Target, Search, Users, Settings } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import type { StrategyData } from './rag-data';
import type { RagStrategy, UserTier } from './types';

/**
 * Props for StrategyCard component.
 */
export interface StrategyCardProps {
  /**
   * Strategy data from rag-data.ts.
   */
  strategy: StrategyData;

  /**
   * Whether the strategy is available for the current user tier.
   */
  isAvailable: boolean;

  /**
   * Whether this strategy is currently selected.
   */
  isSelected?: boolean;

  /**
   * The tier required to access this strategy (only shown when unavailable).
   */
  requiredTier?: UserTier | null;

  /**
   * Callback when the card is clicked.
   */
  onSelect?: (strategy: RagStrategy) => void;

  /**
   * Callback when upgrade is requested (for locked strategies).
   */
  onUpgradeClick?: (strategy: RagStrategy, requiredTier: UserTier) => void;

  /**
   * Additional CSS classes.
   */
  className?: string;

  /**
   * Whether to show detailed info (tokens, cost, latency).
   */
  showDetails?: boolean;
}

/**
 * Icon mapping for strategies.
 */
const STRATEGY_ICONS: Record<RagStrategy, React.ComponentType<{ className?: string }>> = {
  FAST: Zap,
  BALANCED: Scale,
  PRECISE: Target,
  EXPERT: Search,
  CONSENSUS: Users,
  CUSTOM: Settings,
};

/**
 * Color mapping for strategies (matching rag-data.ts).
 */
const STRATEGY_COLORS: Record<RagStrategy, string> = {
  FAST: 'border-green-500/50 bg-green-500/5',
  BALANCED: 'border-blue-500/50 bg-blue-500/5',
  PRECISE: 'border-purple-500/50 bg-purple-500/5',
  EXPERT: 'border-orange-500/50 bg-orange-500/5',
  CONSENSUS: 'border-red-500/50 bg-red-500/5',
  CUSTOM: 'border-yellow-500/50 bg-yellow-500/5',
};

const STRATEGY_ICON_COLORS: Record<RagStrategy, string> = {
  FAST: 'text-green-500',
  BALANCED: 'text-blue-500',
  PRECISE: 'text-purple-500',
  EXPERT: 'text-orange-500',
  CONSENSUS: 'text-red-500',
  CUSTOM: 'text-yellow-500',
};

const STRATEGY_SELECTED_COLORS: Record<RagStrategy, string> = {
  FAST: 'ring-2 ring-green-500 border-green-500',
  BALANCED: 'ring-2 ring-blue-500 border-blue-500',
  PRECISE: 'ring-2 ring-purple-500 border-purple-500',
  EXPERT: 'ring-2 ring-orange-500 border-orange-500',
  CONSENSUS: 'ring-2 ring-red-500 border-red-500',
  CUSTOM: 'ring-2 ring-yellow-500 border-yellow-500',
};

/**
 * StrategyCard Component
 *
 * Displays a RAG strategy card with tier-based access control.
 * Shows disabled state with upgrade message when strategy is unavailable.
 * Supports keyboard navigation for accessibility.
 */
export const StrategyCard = forwardRef<HTMLDivElement, StrategyCardProps>(
  (
    {
      strategy,
      isAvailable,
      isSelected = false,
      requiredTier,
      onSelect,
      onUpgradeClick,
      className,
      showDetails = true,
    },
    ref
  ) => {
    const strategyId = strategy.id as RagStrategy;
    const Icon = STRATEGY_ICONS[strategyId];

    const handleClick = () => {
      if (isAvailable && onSelect) {
        onSelect(strategyId);
      } else if (!isAvailable && onUpgradeClick && requiredTier) {
        onUpgradeClick(strategyId, requiredTier);
      }
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleClick();
      }
    };

    const cardClasses = cn(
      'relative cursor-pointer transition-all duration-200',
      'border-2',
      STRATEGY_COLORS[strategyId],
      isSelected && STRATEGY_SELECTED_COLORS[strategyId],
      !isAvailable && 'opacity-60 cursor-not-allowed grayscale-[30%]',
      isAvailable && !isSelected && 'hover:scale-[1.02] hover:shadow-md',
      className
    );

    return (
      <TooltipProvider>
        <Card
          ref={ref}
          className={cardClasses}
          onClick={handleClick}
          onKeyDown={handleKeyDown}
          role="button"
          tabIndex={0}
          aria-selected={isSelected}
          aria-disabled={!isAvailable}
          aria-label={`${strategy.name} strategy${!isAvailable ? ` (requires ${requiredTier} tier)` : ''}`}
        >
          <CardContent className="p-4">
            {/* Header */}
            <div className="flex items-start justify-between mb-3">
              <div className="flex items-center gap-2">
                <div
                  className={cn(
                    'p-2 rounded-lg',
                    isAvailable ? STRATEGY_COLORS[strategyId] : 'bg-muted'
                  )}
                >
                  <Icon
                    className={cn(
                      'h-5 w-5',
                      isAvailable ? STRATEGY_ICON_COLORS[strategyId] : 'text-muted-foreground'
                    )}
                  />
                </div>
                <div>
                  <h3 className="font-semibold text-sm flex items-center gap-2">
                    <span>{strategy.icon}</span>
                    {strategy.name}
                  </h3>
                </div>
              </div>

              {/* Lock icon and tier badge for unavailable strategies */}
              {!isAvailable && requiredTier && (
                <Tooltip>
                  <TooltipTrigger asChild>
                    <div className="flex items-center gap-1">
                      <Lock className="h-4 w-4 text-muted-foreground" />
                      <Badge variant="secondary" className="text-xs">
                        {requiredTier}
                      </Badge>
                    </div>
                  </TooltipTrigger>
                  <TooltipContent side="top" className="max-w-xs">
                    <div className="space-y-1">
                      <p className="font-semibold">{strategy.name} Strategy</p>
                      <p className="text-sm">Requires: {requiredTier} tier or higher</p>
                      <p className="text-xs text-muted-foreground">
                        Upgrade your account to access advanced strategies
                      </p>
                    </div>
                  </TooltipContent>
                </Tooltip>
              )}

              {/* Info tooltip for available strategies */}
              {isAvailable && (
                <Tooltip>
                  <TooltipTrigger asChild>
                    <button
                      type="button"
                      className="text-muted-foreground hover:text-foreground"
                      onClick={(e) => e.stopPropagation()}
                      aria-label={`More info about ${strategy.name}`}
                    >
                      <Info className="h-4 w-4" />
                    </button>
                  </TooltipTrigger>
                  <TooltipContent side="top" className="max-w-xs">
                    <div className="space-y-2">
                      <p className="font-semibold">{strategy.name} Strategy</p>
                      <p className="text-sm">{strategy.descriptionEn}</p>
                      <div className="text-xs text-muted-foreground space-y-1">
                        <p>Tokens: ~{strategy.tokens.toLocaleString()}</p>
                        <p>Latency: {strategy.latency.display}</p>
                        <p>Accuracy: {strategy.accuracy.display}</p>
                      </div>
                    </div>
                  </TooltipContent>
                </Tooltip>
              )}
            </div>

            {/* Description */}
            <p className="text-xs text-muted-foreground mb-3 line-clamp-2">
              {strategy.description}
            </p>

            {/* Details section */}
            {showDetails && (
              <div className="grid grid-cols-3 gap-2 text-xs">
                <div className="text-center p-2 rounded bg-muted/50">
                  <div className="text-muted-foreground">Tokens</div>
                  <div className="font-medium">{formatTokens(strategy.tokens)}</div>
                </div>
                <div className="text-center p-2 rounded bg-muted/50">
                  <div className="text-muted-foreground">Latency</div>
                  <div className="font-medium">{strategy.latency.display}</div>
                </div>
                <div className="text-center p-2 rounded bg-muted/50">
                  <div className="text-muted-foreground">Accuracy</div>
                  <div className="font-medium">{strategy.accuracy.display}</div>
                </div>
              </div>
            )}

            {/* Upgrade message for locked strategies */}
            {!isAvailable && requiredTier && (
              <div className="mt-3 p-2 rounded bg-muted/50 text-center">
                <p className="text-xs text-muted-foreground">
                  <Lock className="h-3 w-3 inline-block mr-1" />
                  Upgrade to {requiredTier} to unlock
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      </TooltipProvider>
    );
  }
);

StrategyCard.displayName = 'StrategyCard';

/**
 * Format token count for display.
 */
function formatTokens(tokens: number): string {
  if (tokens >= 10000) {
    return `${(tokens / 1000).toFixed(0)}K`;
  }

  if (tokens >= 1000) {
    return `${(tokens / 1000).toFixed(1)}K`;
  }

  return tokens.toString();
}
