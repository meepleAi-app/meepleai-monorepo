'use client';

import React, { forwardRef, useCallback } from 'react';

import { motion } from 'framer-motion';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import type { RetrievalStrategyCardProps, MetricTier } from './types';

/**
 * Color mapping for strategy cards.
 */
const STRATEGY_COLORS: Record<string, string> = {
  blue: 'border-blue-500/50 bg-blue-500/5',
  purple: 'border-purple-500/50 bg-purple-500/5',
  green: 'border-green-500/50 bg-green-500/5',
  orange: 'border-orange-500/50 bg-orange-500/5',
  cyan: 'border-cyan-500/50 bg-cyan-500/5',
  red: 'border-red-500/50 bg-red-500/5',
};

const STRATEGY_SELECTED_COLORS: Record<string, string> = {
  blue: 'ring-2 ring-blue-500 border-blue-500',
  purple: 'ring-2 ring-purple-500 border-purple-500',
  green: 'ring-2 ring-green-500 border-green-500',
  orange: 'ring-2 ring-orange-500 border-orange-500',
  cyan: 'ring-2 ring-cyan-500 border-cyan-500',
  red: 'ring-2 ring-red-500 border-red-500',
};

const STRATEGY_HOVER_GLOW: Record<string, string> = {
  blue: 'hover:shadow-[0_0_20px_hsla(221,83%,53%,0.3)]',
  purple: 'hover:shadow-[0_0_20px_hsla(262,83%,62%,0.3)]',
  green: 'hover:shadow-[0_0_20px_hsla(142,76%,36%,0.3)]',
  orange: 'hover:shadow-[0_0_20px_hsla(25,95%,53%,0.3)]',
  cyan: 'hover:shadow-[0_0_20px_hsla(186,78%,42%,0.3)]',
  red: 'hover:shadow-[0_0_20px_hsla(0,72%,51%,0.3)]',
};

const ICON_COLORS: Record<string, string> = {
  blue: 'text-blue-500',
  purple: 'text-purple-500',
  green: 'text-green-500',
  orange: 'text-orange-500',
  cyan: 'text-cyan-500',
  red: 'text-red-500',
};

/**
 * Badge variants for metric tiers.
 */
const METRIC_TIER_VARIANTS: Record<MetricTier, { label: string; variant: 'default' | 'secondary' | 'outline' | 'destructive' }> = {
  low: { label: 'Low', variant: 'secondary' },
  medium: { label: 'Med', variant: 'default' },
  high: { label: 'High', variant: 'outline' },
  variable: { label: 'Var', variant: 'destructive' },
};

/**
 * RetrievalStrategyCard Component
 *
 * Displays a clickable card for a RAG retrieval strategy with:
 * - Strategy name and icon
 * - Short description
 * - Metric badges (latency, accuracy, cost)
 * - Hover effects with colored glow
 * - Keyboard navigation support
 */
export const RetrievalStrategyCard = forwardRef<HTMLDivElement, RetrievalStrategyCardProps>(
  ({ strategy, isSelected = false, onClick, className }, ref) => {
    const handleClick = useCallback(() => {
      onClick?.(strategy);
    }, [onClick, strategy]);

    const handleKeyDown = useCallback(
      (e: React.KeyboardEvent) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          handleClick();
        }
      },
      [handleClick]
    );

    const cardClasses = cn(
      'relative cursor-pointer transition-all duration-300',
      'border-2',
      STRATEGY_COLORS[strategy.color] || STRATEGY_COLORS.blue,
      STRATEGY_HOVER_GLOW[strategy.color] || STRATEGY_HOVER_GLOW.blue,
      isSelected && (STRATEGY_SELECTED_COLORS[strategy.color] || STRATEGY_SELECTED_COLORS.blue),
      !isSelected && 'hover:scale-[1.02]',
      className
    );

    return (
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
        whileHover={{ scale: isSelected ? 1 : 1.02 }}
        whileTap={{ scale: 0.98 }}
      >
        <Card
          ref={ref}
          className={cardClasses}
          onClick={handleClick}
          onKeyDown={handleKeyDown}
          role="button"
          tabIndex={0}
          aria-selected={isSelected}
          aria-label={`${strategy.name} retrieval strategy`}
        >
          <CardContent className="p-4">
            {/* Header with icon and name */}
            <div className="flex items-start justify-between mb-3">
              <div className="flex items-center gap-3">
                <div
                  className={cn(
                    'flex items-center justify-center w-12 h-12 rounded-lg text-2xl',
                    STRATEGY_COLORS[strategy.color] || STRATEGY_COLORS.blue
                  )}
                >
                  {strategy.icon}
                </div>
                <div>
                  <h3
                    className={cn(
                      'font-semibold text-sm',
                      ICON_COLORS[strategy.color] || ICON_COLORS.blue
                    )}
                  >
                    {strategy.name}
                  </h3>
                  {strategy.tags.includes('recommended') && (
                    <Badge variant="default" className="mt-1 text-[10px] px-1.5 py-0">
                      Recommended
                    </Badge>
                  )}
                </div>
              </div>
            </div>

            {/* Description */}
            <p className="text-xs text-muted-foreground mb-4 line-clamp-2 min-h-[32px]">
              {strategy.shortDescription}
            </p>

            {/* Metric badges */}
            <div className="flex flex-wrap gap-2">
              <Badge
                variant={METRIC_TIER_VARIANTS[strategy.metrics.latency].variant}
                className="text-[10px] px-2 py-0.5"
              >
                ⏱️ {METRIC_TIER_VARIANTS[strategy.metrics.latency].label}
              </Badge>
              <Badge
                variant={METRIC_TIER_VARIANTS[strategy.metrics.accuracy].variant}
                className="text-[10px] px-2 py-0.5"
              >
                🎯 {METRIC_TIER_VARIANTS[strategy.metrics.accuracy].label}
              </Badge>
              <Badge
                variant={METRIC_TIER_VARIANTS[strategy.metrics.costTier].variant}
                className="text-[10px] px-2 py-0.5"
              >
                💰 {METRIC_TIER_VARIANTS[strategy.metrics.costTier].label}
              </Badge>
            </div>

            {/* Selected indicator */}
            {isSelected && (
              <motion.div
                initial={{ opacity: 0, scale: 0.8 }}
                animate={{ opacity: 1, scale: 1 }}
                className="absolute top-2 right-2"
              >
                <Badge
                  variant="default"
                  className={cn(
                    'text-[10px] px-2 py-0.5',
                    strategy.color === 'green' && 'bg-green-500',
                    strategy.color === 'blue' && 'bg-blue-500',
                    strategy.color === 'purple' && 'bg-purple-500',
                    strategy.color === 'orange' && 'bg-orange-500',
                    strategy.color === 'cyan' && 'bg-cyan-500',
                    strategy.color === 'red' && 'bg-red-500'
                  )}
                >
                  Selected
                </Badge>
              </motion.div>
            )}
          </CardContent>
        </Card>
      </motion.div>
    );
  }
);

RetrievalStrategyCard.displayName = 'RetrievalStrategyCard';
