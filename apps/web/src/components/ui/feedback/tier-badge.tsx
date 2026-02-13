/**
 * Tier Badge Component
 * Epic #4068 - Issue #4179
 *
 * Displays user subscription tier with color-coded styling
 */

'use client';

import React from 'react';
import { Crown } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { UserTier } from '@/types/permissions';

interface TierBadgeProps {
  tier: UserTier;
  className?: string;
  showIcon?: boolean;
}

const TIER_CONFIG: Record<UserTier, { label: string; color: string; bgColor: string }> = {
  free: {
    label: 'Free',
    color: 'hsl(0 0% 45%)',       // Gray
    bgColor: 'hsl(0 0% 96%)'
  },
  normal: {
    label: 'Normal',
    color: 'hsl(221 83% 53%)',    // Blue
    bgColor: 'hsl(221 83% 95%)'
  },
  premium: {
    label: 'Premium',
    color: 'hsl(262 83% 58%)',    // Purple
    bgColor: 'hsl(262 83% 95%)'
  },
  pro: {
    label: 'Pro',
    color: 'hsl(262 83% 58%)',    // Purple (alias for premium)
    bgColor: 'hsl(262 83% 95%)'
  },
  enterprise: {
    label: 'Enterprise',
    color: 'hsl(38 92% 50%)',     // Gold
    bgColor: 'hsl(38 92% 95%)'
  }
};

/**
 * TierBadge displays the user's subscription tier
 * Color-coded: Gray (Free), Blue (Normal), Purple (Pro), Gold (Enterprise)
 */
export function TierBadge({ tier, className, showIcon = true }: TierBadgeProps) {
  const config = TIER_CONFIG[tier];

  return (
    <div
      className={cn(
        'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-semibold',
        'border backdrop-blur-sm',
        className
      )}
      style={{
        color: config.color,
        backgroundColor: config.bgColor,
        borderColor: config.color
      }}
      role="status"
      aria-label={`Subscription tier: ${config.label}`}
    >
      {showIcon && tier !== 'free' && <Crown className="w-3 h-3" />}
      <span>{config.label}</span>
    </div>
  );
}
