/**
 * BadgeGrid Component (Issue #2747)
 *
 * Displays user badges grouped by tier with grid layout.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

'use client';

import React from 'react';

import { BadgeTier, getTierIcon, getTierOrder, type UserBadgeDto } from '@/types/badges';
import { cn } from '@/lib/utils';

export interface BadgeGridProps {
  /** Array of user badges to display */
  badges: UserBadgeDto[];

  /** Whether to show hidden badges (default: false) */
  showHidden?: boolean;

  /** Optional click handler for badge items */
  onBadgeClick?: (badge: UserBadgeDto) => void;

  /** Optional CSS class */
  className?: string;
}

/**
 * Badge Grid component with tier-based grouping
 *
 * @example
 * ```tsx
 * <BadgeGrid
 *   badges={badges}
 *   showHidden={false}
 *   onBadgeClick={(badge) => console.log(badge)}
 * />
 * ```
 */
export function BadgeGrid({
  badges,
  showHidden = false,
  onBadgeClick,
  className,
}: BadgeGridProps): JSX.Element {
  // Filter visible badges
  const visibleBadges = showHidden ? badges : badges.filter((b) => b.isDisplayed);

  // Group by tier
  const badgesByTier = visibleBadges.reduce<Record<BadgeTier, UserBadgeDto[]>>(
    (acc, badge) => {
      const tier = badge.tier;
      if (!acc[tier]) {
        acc[tier] = [];
      }
      acc[tier].push(badge);
      return acc;
    },
    {} as Record<BadgeTier, UserBadgeDto[]>
  );

  // Tier order (Diamond first, Bronze last)
  const tierOrder: BadgeTier[] = [
    BadgeTier.Diamond,
    BadgeTier.Platinum,
    BadgeTier.Gold,
    BadgeTier.Silver,
    BadgeTier.Bronze,
  ];

  // Sort tiers by order
  const sortedTiers = tierOrder.filter((tier) => badgesByTier[tier]?.length > 0);

  if (visibleBadges.length === 0) {
    return (
      <div className={cn('flex flex-col items-center justify-center py-12', className)}>
        <p className="text-muted-foreground text-sm">No badges to display</p>
      </div>
    );
  }

  return (
    <div className={cn('space-y-8', className)}>
      {sortedTiers.map((tier) => {
        const tierBadges = badgesByTier[tier];
        if (!tierBadges?.length) return null;

        return (
          <div key={tier} className="space-y-4">
            {/* Tier Header */}
            <h3 className="text-muted-foreground flex items-center gap-2 text-sm font-medium">
              <span className="text-lg">{getTierIcon(tier)}</span>
              <span>
                {tier} Badges ({tierBadges.length})
              </span>
            </h3>

            {/* Badge Grid */}
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
              {tierBadges
                .sort((a, b) => {
                  // Sort by earned date (most recent first)
                  return new Date(b.earnedAt).getTime() - new Date(a.earnedAt).getTime();
                })
                .map((badge) => (
                  <BadgeItem
                    key={badge.id}
                    badge={badge}
                    onClick={onBadgeClick ? () => onBadgeClick(badge) : undefined}
                  />
                ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}

/**
 * Internal: Individual badge item component
 */
interface BadgeItemProps {
  badge: UserBadgeDto;
  onClick?: () => void;
}

function BadgeItem({ badge, onClick }: BadgeItemProps): JSX.Element {
  const tierGradient = getTierGradient(badge.tier);

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={!onClick}
      className={cn(
        'group relative flex flex-col items-center gap-2 rounded-lg border bg-card p-4 text-center transition-all',
        onClick && 'hover:border-primary hover:shadow-md cursor-pointer',
        !onClick && 'cursor-default'
      )}
    >
      {/* Badge Icon */}
      <div
        className={cn(
          'relative h-16 w-16 rounded-full p-1',
          tierGradient
        )}
      >
        <div className="flex h-full w-full items-center justify-center rounded-full bg-background">
          {badge.iconUrl ? (
            <img
              src={badge.iconUrl}
              alt={badge.name}
              className="h-12 w-12 object-contain"
            />
          ) : (
            <span className="text-2xl">{getTierIcon(badge.tier)}</span>
          )}
        </div>
      </div>

      {/* Hidden indicator */}
      {!badge.isDisplayed && (
        <div className="absolute right-2 top-2 rounded-full bg-muted p-1">
          <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 20 20"
            fill="currentColor"
            className="text-muted-foreground h-3 w-3"
          >
            <path
              fillRule="evenodd"
              d="M3.28 2.22a.75.75 0 00-1.06 1.06l14.5 14.5a.75.75 0 101.06-1.06l-1.745-1.745a10.029 10.029 0 003.3-4.38 1.651 1.651 0 000-1.185A10.004 10.004 0 009.999 3a9.956 9.956 0 00-4.744 1.194L3.28 2.22zM7.752 6.69l1.092 1.092a2.5 2.5 0 013.374 3.373l1.091 1.092a4 4 0 00-5.557-5.557z"
              clipRule="evenodd"
            />
            <path d="M10.748 13.93l2.523 2.523a9.987 9.987 0 01-3.27.547c-4.258 0-7.894-2.66-9.337-6.41a1.651 1.651 0 010-1.186A10.007 10.007 0 012.839 6.02L6.07 9.252a4 4 0 004.678 4.678z" />
          </svg>
        </div>
      )}

      {/* Badge Name */}
      <p className="text-foreground line-clamp-2 text-xs font-medium">
        {badge.name}
      </p>
    </button>
  );
}

/**
 * Helper: Get tier gradient class
 */
function getTierGradient(tier: BadgeTier): string {
  const gradients: Record<BadgeTier, string> = {
    [BadgeTier.Diamond]: 'bg-gradient-to-br from-cyan-500 to-purple-500',
    [BadgeTier.Platinum]: 'bg-gradient-to-br from-slate-300 to-slate-400',
    [BadgeTier.Gold]: 'bg-gradient-to-br from-yellow-400 to-yellow-600',
    [BadgeTier.Silver]: 'bg-gradient-to-br from-gray-300 to-gray-400',
    [BadgeTier.Bronze]: 'bg-gradient-to-br from-amber-600 to-amber-800',
  };
  return gradients[tier];
}

BadgeGrid.displayName = 'BadgeGrid';
