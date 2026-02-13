/**
 * Collection Limit Indicator Component
 * Epic #4068 - Issue #4183
 *
 * Displays collection limits for current user tier with upgrade CTA
 */

'use client';

import React from 'react';

import { Crown, ArrowRight } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { UserTier } from '@/types/permissions';

import { CollectionProgressBar } from './collection-progress-bar';

interface CollectionLimitIndicatorProps {
  /** User's current tier */
  tier: UserTier;
  /** Current game count */
  currentGames: number;
  /** Current storage usage in MB */
  currentStorageMB: number;
  /** Maximum games allowed for tier */
  maxGames: number;
  /** Maximum storage in MB for tier */
  maxStorageMB: number;
  /** Optional upgrade callback */
  onUpgrade?: () => void;
  /** Additional CSS classes */
  className?: string;
}

const TIER_LABELS: Record<UserTier, string> = {
  free: 'Free',
  normal: 'Normal',
  premium: 'Premium',
  pro: 'Pro',
  enterprise: 'Enterprise'
};

/**
 * CollectionLimitIndicator shows tier-based collection limits
 * with progress bars and upgrade prompts when approaching limits
 */
export function CollectionLimitIndicator({
  tier,
  currentGames,
  currentStorageMB,
  maxGames,
  maxStorageMB,
  onUpgrade,
  className
}: CollectionLimitIndicatorProps) {
  const gamesPercentage = (currentGames / maxGames) * 100;
  const storagePercentage = (currentStorageMB / maxStorageMB) * 100;
  const approachingLimit = gamesPercentage >= 75 || storagePercentage >= 75;
  const criticalLimit = gamesPercentage >= 90 || storagePercentage >= 90;

  return (
    <div
      className={cn(
        'rounded-lg border bg-card p-6 space-y-6',
        'backdrop-blur-sm',
        className
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-bold text-foreground">
            Collection Limits
          </h3>
          <p className="text-sm text-muted-foreground">
            {TIER_LABELS[tier]} Tier
          </p>
        </div>
        {tier !== 'enterprise' && (
          <Crown className="w-6 h-6 text-muted-foreground" />
        )}
      </div>

      {/* Games Limit */}
      <CollectionProgressBar
        current={currentGames}
        max={maxGames}
        label="Games"
        unit="games"
        showWarning
      />

      {/* Storage Limit */}
      <CollectionProgressBar
        current={currentStorageMB}
        max={maxStorageMB}
        label="Storage"
        unit="MB"
        showWarning
      />

      {/* Upgrade CTA (when approaching/critical) */}
      {criticalLimit && tier !== 'enterprise' && onUpgrade && (
        <div className="pt-4 border-t">
          <button
            onClick={onUpgrade}
            className={cn(
              'w-full inline-flex items-center justify-center gap-2',
              'px-4 py-3 rounded-lg',
              'text-sm font-bold text-white',
              'bg-gradient-to-r from-amber-500 to-amber-600',
              'hover:from-amber-600 hover:to-amber-700',
              'shadow-md hover:shadow-lg',
              'transition-all duration-200',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-400'
            )}
          >
            <Crown className="w-4 h-4" />
            Upgrade to {tier === 'free' ? 'Normal' : tier === 'normal' ? 'Pro' : 'Enterprise'}
            <ArrowRight className="w-4 h-4" />
          </button>
        </div>
      )}

      {approachingLimit && !criticalLimit && tier !== 'enterprise' && (
        <div className="pt-4 border-t">
          <button
            onClick={onUpgrade}
            className={cn(
              'w-full inline-flex items-center justify-center gap-2',
              'px-4 py-2 rounded-lg',
              'text-sm font-semibold',
              'border-2 border-amber-400 text-amber-700',
              'hover:bg-amber-50',
              'transition-colors duration-200',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-400'
            )}
          >
            Learn about upgrades
            <ArrowRight className="w-4 h-4" />
          </button>
        </div>
      )}
    </div>
  );
}
