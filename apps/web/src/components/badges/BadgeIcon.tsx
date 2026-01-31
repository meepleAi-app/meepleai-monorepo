/**
 * BadgeIcon Component
 * Issue #2746: Frontend - Contributor Display su SharedGame
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 *
 * Displays a badge icon with tier-based visual effects and tooltip.
 */

import type { BadgeSummaryDto, BadgeTier } from '@/lib/api';
import { cn } from '@/lib/utils';

interface BadgeIconProps {
  badge: BadgeSummaryDto;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

const sizeClasses = {
  sm: 'w-6 h-6',
  md: 'w-8 h-8',
  lg: 'w-12 h-12',
} as const;

/**
 * Get tier-based ring and glow effects
 * Higher tiers get more prominent visual styling
 */
function getTierGlow(tier: BadgeTier): string {
  const glows = {
    Bronze: 'ring-1 ring-amber-600/30 hover:ring-amber-600/50',
    Silver: 'ring-1 ring-gray-400/30 hover:ring-gray-400/50',
    Gold: 'ring-2 ring-yellow-400/50 shadow-yellow-400/20 shadow-lg hover:ring-yellow-400/70',
    Platinum:
      'ring-2 ring-gray-300/50 shadow-gray-300/20 shadow-lg hover:ring-gray-300/70 hover:shadow-xl',
    Diamond:
      'ring-2 ring-cyan-400/50 shadow-cyan-400/30 shadow-lg animate-pulse hover:ring-cyan-400/70 hover:shadow-2xl',
  };
  // eslint-disable-next-line security/detect-object-injection -- Safe: tier is a typed BadgeTier enum value
  return glows[tier] || '';
}

/**
 * Get tier display color for text
 */
function getTierColor(tier: BadgeTier): string {
  const colors = {
    Bronze: 'text-amber-600',
    Silver: 'text-gray-400',
    Gold: 'text-yellow-500',
    Platinum: 'text-gray-300',
    Diamond: 'text-cyan-400',
  };
  // eslint-disable-next-line security/detect-object-injection -- Safe: tier is a typed BadgeTier enum value
  return colors[tier] || 'text-gray-500';
}

export function BadgeIcon({ badge, size = 'md', className }: BadgeIconProps) {
  const tierGlow = getTierGlow(badge.tier);
  const tierColor = getTierColor(badge.tier);

  return (
    <div className={cn('group relative inline-block', className)}>
      {/* Badge Icon */}
      <div
        className={cn(
          'relative rounded-full bg-gradient-to-br from-background to-muted overflow-hidden transition-all duration-300',
          // eslint-disable-next-line security/detect-object-injection -- Safe: size is typed 'sm' | 'md' | 'lg'
          sizeClasses[size],
          tierGlow
        )}
      >
        {badge.iconUrl ? (
          /* eslint-disable-next-line @next/next/no-img-element -- External user-provided URL, Next.js Image optimization not applicable */
          <img
            src={badge.iconUrl}
            alt={badge.name}
            className="w-full h-full object-cover"
            loading="lazy"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center">
            <span className={cn('text-xs font-bold', tierColor)}>{badge.tier[0]}</span>
          </div>
        )}
      </div>

      {/* Tooltip */}
      <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-2 bg-popover text-popover-foreground text-sm rounded-md shadow-lg opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none whitespace-nowrap z-10 border">
        <div className="flex flex-col gap-1">
          <p className="font-semibold">{badge.name}</p>
          <p className={cn('text-xs', tierColor)}>{badge.tier} tier</p>
        </div>
        {/* Tooltip arrow */}
        <div className="absolute top-full left-1/2 -translate-x-1/2 -mt-px border-4 border-transparent border-t-popover" />
      </div>
    </div>
  );
}
