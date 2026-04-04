/**
 * CollectionLimitIndicator - Tier-Based Collection Limits Display
 * Issue #4183 - Collection Limit UI & Progress Indicators
 *
 * Displays game count and storage quota progress bars with tier-based limits.
 * Shows warning icons when approaching limits (>75%) and upgrade CTA when >90%.
 */

'use client';

import React from 'react';

import { AlertTriangle, TrendingUp } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import {
  COLLECTION_LIMITS,
  formatGameCount,
  formatStorage,
  getLimitColor,
  type UserTier,
} from '@/lib/constants/collection-limits';
import { cn } from '@/lib/utils';

import { Progress } from '../feedback/progress';

// ============================================================================
// Types
// ============================================================================

export interface CollectionStats {
  /** Current number of games in collection */
  gameCount: number;
  /** Current storage usage in MB */
  storageMB: number;
}

export interface CollectionLimitIndicatorProps {
  /** User's current tier */
  tier: UserTier;
  /** Current collection statistics */
  stats: CollectionStats;
  /** Callback when upgrade button clicked */
  onUpgrade?: () => void;
  /** Custom className */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export const CollectionLimitIndicator = React.memo(function CollectionLimitIndicator({
  tier,
  stats,
  onUpgrade,
  className,
}: CollectionLimitIndicatorProps) {
  const limits = COLLECTION_LIMITS[tier];
  const isEnterprise = tier === 'Enterprise';

  // Calculate percentages
  const gamePercent = isEnterprise ? 0 : (stats.gameCount / limits.maxGames) * 100;
  const storagePercent = isEnterprise ? 0 : (stats.storageMB / limits.storageMB) * 100;

  // Determine colors
  const gameColor = getLimitColor(gamePercent);
  const storageColor = getLimitColor(storagePercent);

  // Warning states
  const showGameWarning = gamePercent >= 75;
  const showStorageWarning = storagePercent >= 75;
  const showUpgradeCTA = gamePercent >= 90 || storagePercent >= 90;

  return (
    <div
      className={cn('space-y-4 p-4 rounded-lg border border-border bg-card', className)}
      data-testid="collection-limit-indicator"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-foreground">Your Collection ({tier} Tier)</h3>
        {showUpgradeCTA && !isEnterprise && onUpgrade && (
          <Button
            onClick={onUpgrade}
            size="sm"
            variant="outline"
            className="gap-1.5"
            data-testid="upgrade-cta"
          >
            <TrendingUp className="w-3.5 h-3.5" />
            Upgrade to {tier === 'Free' ? 'Normal' : tier === 'Normal' ? 'Pro' : 'Enterprise'}
          </Button>
        )}
      </div>

      {/* Games Progress */}
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <div className="space-y-2" data-testid="games-progress">
              <div className="flex items-center justify-between text-xs">
                <span className="flex items-center gap-1.5 text-muted-foreground">
                  Games
                  {showGameWarning && (
                    <AlertTriangle
                      className="w-3.5 h-3.5 text-yellow-600 dark:text-yellow-500"
                      data-testid="game-warning-icon"
                    />
                  )}
                </span>
                <span className="font-semibold text-foreground">
                  {formatGameCount(stats.gameCount)}/
                  {isEnterprise ? '∞' : formatGameCount(limits.maxGames)}
                </span>
              </div>
              <Progress
                value={isEnterprise ? 0 : gamePercent}
                className={cn(
                  'h-2',
                  gameColor === 'green' && '[&>div]:bg-green-500',
                  gameColor === 'yellow' && '[&>div]:bg-yellow-500',
                  gameColor === 'red' && '[&>div]:bg-red-500'
                )}
                aria-label={`Game collection: ${stats.gameCount} of ${isEnterprise ? 'unlimited' : limits.maxGames} games`}
              />
              {showGameWarning && !isEnterprise && (
                <p className="text-[10px] text-yellow-600 dark:text-yellow-500">
                  Approaching limit
                </p>
              )}
            </div>
          </TooltipTrigger>
          <TooltipContent>
            <p className="text-xs">
              {isEnterprise
                ? 'Unlimited games in Enterprise tier'
                : `${stats.gameCount} games used of ${limits.maxGames} limit (${Math.round(gamePercent)}%)`}
            </p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>

      {/* Storage Progress */}
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <div className="space-y-2" data-testid="storage-progress">
              <div className="flex items-center justify-between text-xs">
                <span className="flex items-center gap-1.5 text-muted-foreground">
                  Storage
                  {showStorageWarning && (
                    <AlertTriangle
                      className="w-3.5 h-3.5 text-yellow-600 dark:text-yellow-500"
                      data-testid="storage-warning-icon"
                    />
                  )}
                </span>
                <span className="font-semibold text-foreground">
                  {formatStorage(stats.storageMB)}/
                  {isEnterprise ? '∞' : formatStorage(limits.storageMB)}
                </span>
              </div>
              <Progress
                value={isEnterprise ? 0 : storagePercent}
                className={cn(
                  'h-2',
                  storageColor === 'green' && '[&>div]:bg-green-500',
                  storageColor === 'yellow' && '[&>div]:bg-yellow-500',
                  storageColor === 'red' && '[&>div]:bg-red-500'
                )}
                aria-label={`Storage usage: ${formatStorage(stats.storageMB)} of ${isEnterprise ? 'unlimited' : formatStorage(limits.storageMB)}`}
              />
              {showStorageWarning && !isEnterprise && (
                <p className="text-[10px] text-yellow-600 dark:text-yellow-500">
                  Approaching limit
                </p>
              )}
            </div>
          </TooltipTrigger>
          <TooltipContent>
            <p className="text-xs">
              {isEnterprise
                ? 'Unlimited storage in Enterprise tier'
                : `${formatStorage(stats.storageMB)} used of ${formatStorage(limits.storageMB)} limit (${Math.round(storagePercent)}%)`}
            </p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    </div>
  );
});

CollectionLimitIndicator.displayName = 'CollectionLimitIndicator';
