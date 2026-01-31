/**
 * BadgeDetailSheet Component (Issue #2747)
 *
 * Side sheet displaying detailed badge information.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

'use client';

import { motion } from 'framer-motion';

import { Switch } from '@/components/ui/forms/switch';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { BadgeTier, getTierIcon, type UserBadgeDto } from '@/types/badges';

export interface BadgeDetailSheetProps {
  /** Badge to display (null to hide sheet) */
  badge: UserBadgeDto | null;

  /** Callback when sheet is closed */
  onClose: () => void;

  /** Callback when display toggle changes */
  onToggleDisplay?: (badgeId: string, isDisplayed: boolean) => void;

  /** Optional share handler */
  onShare?: (badge: UserBadgeDto) => void;
}

/**
 * Helper: Get confetti configuration based on tier
 * @internal Reserved for future confetti celebration feature
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
function getConfettiConfig(tier: BadgeTier) {
  const configs = {
    [BadgeTier.Diamond]: {
      elementCount: 200,
      colors: ['#06b6d4', '#8b5cf6', '#a855f7', '#3b82f6'],
    },
    [BadgeTier.Platinum]: {
      elementCount: 150,
      colors: ['#cbd5e1', '#94a3b8', '#64748b'],
    },
    [BadgeTier.Gold]: {
      elementCount: 150,
      colors: ['#facc15', '#eab308', '#f59e0b'],
    },
    [BadgeTier.Silver]: {
      elementCount: 100,
      colors: ['#d1d5db', '#9ca3af', '#6b7280'],
    },
    [BadgeTier.Bronze]: {
      elementCount: 100,
      colors: ['#d97706', '#f59e0b', '#b45309'],
    },
  };
  // eslint-disable-next-line security/detect-object-injection -- Safe: tier is a BadgeTier enum value
  return configs[tier];
}

/**
 * Helper: Get tier glow color
 * @internal Reserved for future glow effect feature
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
function getTierGlow(tier: string): string {
  const glows: Record<string, string> = {
    Diamond: 'rgba(6, 182, 212, 0.8)',
    Platinum: 'rgba(203, 213, 225, 0.6)',
    Gold: 'rgba(250, 204, 21, 0.7)',
    Silver: 'rgba(209, 213, 219, 0.5)',
    Bronze: 'rgba(217, 119, 6, 0.6)',
  };
  // eslint-disable-next-line security/detect-object-injection -- Safe: tier is from known BadgeTier enum, with fallback
  return glows[tier] || 'rgba(0, 0, 0, 0.3)';
}

/**
 * Badge Detail Sheet with visibility toggle
 *
 * @example
 * ```tsx
 * <BadgeDetailSheet
 *   badge={selectedBadge}
 *   onClose={() => setSelectedBadge(null)}
 *   onToggleDisplay={(id, display) => toggleMutation({ id, isDisplayed: display })}
 *   onShare={(badge) => shareToSocial(badge)}
 * />
 * ```
 */
export function BadgeDetailSheet({
  badge,
  onClose,
  onToggleDisplay,
  onShare,
}: BadgeDetailSheetProps){
  if (!badge) return <></>;

  const earnedDate = new Date(badge.earnedAt).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });

  return (
    <Dialog open={!!badge} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Badge Details</DialogTitle>
        </DialogHeader>

        <div className="space-y-6">
          {/* Badge Icon */}
          <motion.div
            initial={{ scale: 0.9, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            transition={{ type: 'spring', stiffness: 200, damping: 15 }}
            className="flex justify-center"
          >
            <div className="relative h-24 w-24 rounded-full bg-gradient-to-br from-cyan-500 to-purple-500 p-2">
              <div className="flex h-full w-full items-center justify-center rounded-full bg-background">
                {badge.iconUrl ? (
                  /* eslint-disable-next-line @next/next/no-img-element -- External user-provided URL, Next.js Image optimization not applicable */
                  <img
                    src={badge.iconUrl}
                    alt={badge.name}
                    className="h-16 w-16 object-contain"
                  />
                ) : (
                  <span className="text-5xl">{getTierIcon(badge.tier)}</span>
                )}
              </div>
            </div>
          </motion.div>

          {/* Badge Info */}
          <div className="text-center">
            <div className="mb-2 flex items-center justify-center gap-2">
              <span className="text-lg">{getTierIcon(badge.tier)}</span>
              <h3 className="text-foreground text-xl font-semibold">{badge.name}</h3>
            </div>
            <p className="text-muted-foreground text-sm">{badge.description}</p>
          </div>

          {/* Metadata */}
          <div className="bg-muted/50 space-y-3 rounded-lg p-4">
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground text-sm">Tier</span>
              <span className="text-foreground flex items-center gap-1 text-sm font-medium">
                {getTierIcon(badge.tier)}
                {badge.tier}
              </span>
            </div>

            <div className="flex items-center justify-between">
              <span className="text-muted-foreground text-sm">Earned</span>
              <span className="text-foreground text-sm font-medium">{earnedDate}</span>
            </div>

            {badge.category && (
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground text-sm">Category</span>
                <span className="text-foreground text-sm font-medium">{badge.category}</span>
              </div>
            )}

            {/* Visibility Toggle */}
            {onToggleDisplay && (
              <div className="flex items-center justify-between border-t pt-3">
                <span className="text-foreground text-sm font-medium">
                  Display on profile
                </span>
                <Switch
                  checked={badge.isDisplayed}
                  onCheckedChange={(checked) => onToggleDisplay(badge.id, checked)}
                />
              </div>
            )}
          </div>

          {/* Actions */}
          <div className="flex gap-3">
            <Button onClick={onClose} variant="outline" className="flex-1">
              Close
            </Button>
            {onShare && (
              <Button onClick={() => onShare(badge)} className="flex-1">
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  className="mr-2 h-4 w-4"
                >
                  <path d="M13 4.5a2.5 2.5 0 11.702 1.737L6.97 9.604a2.518 2.518 0 010 .792l6.733 3.367a2.5 2.5 0 11-.671 1.341l-6.733-3.367a2.5 2.5 0 110-3.475l6.733-3.366A2.52 2.52 0 0113 4.5z" />
                </svg>
                Share Badge
              </Button>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

BadgeDetailSheet.displayName = 'BadgeDetailSheet';
