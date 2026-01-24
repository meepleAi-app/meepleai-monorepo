/**
 * BadgeEarnedModal Component (Issue #2747)
 *
 * Celebratory modal displayed when user earns a new badge.
 * Includes confetti animation and share functionality.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

'use client';

import React, { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
// TODO(#2759): Re-enable confetti after fixing pnpm-lock.yaml
// import Confetti from 'react-confetti';

import { BadgeTier, getCelebratoryTitle, getTierIcon, type BadgeNotificationData } from '@/types/badges';
import { cn } from '@/lib/utils';
import { Dialog, DialogContent } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

export interface BadgeEarnedModalProps {
  /** Badge that was earned (null to hide modal) */
  badge: BadgeNotificationData | null;

  /** Callback when modal is closed */
  onClose: () => void;

  /** Optional share handler */
  onShare?: (badge: BadgeNotificationData) => void;
}

/**
 * Badge Earned Modal with confetti celebration
 *
 * @example
 * ```tsx
 * <BadgeEarnedModal
 *   badge={newBadge}
 *   onClose={() => setNewBadge(null)}
 *   onShare={(badge) => shareToSocial(badge)}
 * />
 * ```
 */
export function BadgeEarnedModal({
  badge,
  onClose,
  onShare,
}: BadgeEarnedModalProps): JSX.Element {
  const [showConfetti, setShowConfetti] = useState(false);
  const [windowSize, setWindowSize] = useState({ width: 0, height: 0 });

  // Track window size for confetti
  useEffect(() => {
    const updateSize = () => {
      setWindowSize({ width: window.innerWidth, height: window.innerHeight });
    };

    updateSize();
    window.addEventListener('resize', updateSize);
    return () => window.removeEventListener('resize', updateSize);
  }, []);

  // Trigger confetti on badge earn
  useEffect(() => {
    if (badge) {
      setShowConfetti(true);
      // Stop confetti after 5 seconds
      const timer = setTimeout(() => setShowConfetti(false), 5000);
      return () => clearTimeout(timer);
    }
  }, [badge]);

  if (!badge) return <></>;

  const confettiConfig = getConfettiConfig(badge.tier);

  return (
    <>
      {/* Confetti Effect */}
      {/* TODO(#2759): Re-enable confetti after fixing pnpm-lock.yaml */}
      {/* {showConfetti && (
        <Confetti
          width={windowSize.width}
          height={windowSize.height}
          numberOfPieces={confettiConfig.elementCount}
          recycle={false}
          gravity={0.3}
          colors={confettiConfig.colors}
        />
      )} */}

      {/* Modal */}
      <Dialog open={!!badge} onOpenChange={(open) => !open && onClose()}>
        <DialogContent className="sm:max-w-md">
          <AnimatePresence>
            <motion.div
              initial={{ opacity: 0, scale: 0.8 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.8 }}
              transition={{ type: 'spring', stiffness: 300, damping: 25 }}
              className="flex flex-col items-center gap-6 py-6"
            >
              {/* Celebratory Title */}
              <motion.div
                initial={{ y: -20, opacity: 0 }}
                animate={{ y: 0, opacity: 1 }}
                transition={{ delay: 0.2 }}
                className="text-center"
              >
                <h2 className="text-foreground text-2xl font-bold">
                  {getCelebratoryTitle(badge.tier)}
                </h2>
              </motion.div>

              {/* Badge Icon with Special Animation */}
              <motion.div
                initial={{ scale: 0 }}
                animate={{ scale: 1, rotate: [0, 10, -10, 0] }}
                transition={{
                  scale: { type: 'spring', stiffness: 200, damping: 15, delay: 0.3 },
                  rotate: { duration: 0.6, delay: 0.5 },
                }}
                className="relative"
              >
                <motion.div
                  className={cn(
                    'relative h-32 w-32 rounded-full p-2',
                    getTierGradient(badge.tier)
                  )}
                  animate={{
                    boxShadow: [
                      `0 0 0px ${getTierGlow(badge.tier)}`,
                      `0 0 40px ${getTierGlow(badge.tier)}`,
                      `0 0 0px ${getTierGlow(badge.tier)}`,
                    ],
                  }}
                  transition={{
                    duration: 1.5,
                    repeat: Infinity,
                    ease: 'easeInOut',
                  }}
                >
                  <div className="flex h-full w-full items-center justify-center rounded-full bg-background">
                    {badge.iconUrl ? (
                      <img
                        src={badge.iconUrl}
                        alt={badge.name}
                        className="h-20 w-20 object-contain"
                      />
                    ) : (
                      <span className="text-6xl">{getTierIcon(badge.tier)}</span>
                    )}
                  </div>
                </motion.div>
              </motion.div>

              {/* Badge Info */}
              <motion.div
                initial={{ y: 20, opacity: 0 }}
                animate={{ y: 0, opacity: 1 }}
                transition={{ delay: 0.6 }}
                className="text-center"
              >
                <h3 className="text-foreground mb-2 text-xl font-semibold">{badge.name}</h3>
                <p className="text-muted-foreground text-sm">{badge.description}</p>
              </motion.div>

              {/* Action Buttons */}
              <motion.div
                initial={{ y: 20, opacity: 0 }}
                animate={{ y: 0, opacity: 1 }}
                transition={{ delay: 0.8 }}
                className="flex w-full gap-3"
              >
                <Button onClick={onClose} variant="outline" className="flex-1">
                  Continue
                </Button>
                {onShare && (
                  <Button
                    onClick={() => onShare(badge)}
                    variant="default"
                    className="flex-1"
                  >
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      viewBox="0 0 20 20"
                      fill="currentColor"
                      className="mr-2 h-4 w-4"
                    >
                      <path d="M13 4.5a2.5 2.5 0 11.702 1.737L6.97 9.604a2.518 2.518 0 010 .792l6.733 3.367a2.5 2.5 0 11-.671 1.341l-6.733-3.367a2.5 2.5 0 110-3.475l6.733-3.366A2.52 2.52 0 0113 4.5z" />
                    </svg>
                    Share
                  </Button>
                )}
              </motion.div>
            </motion.div>
          </AnimatePresence>
        </DialogContent>
      </Dialog>
    </>
  );
}

/**
 * Helper: Get confetti configuration based on tier
 */
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
  return configs[tier];
}

/**
 * Helper: Get tier gradient (reused from BadgeGrid)
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

/**
 * Helper: Get tier glow color
 */
function getTierGlow(tier: BadgeTier): string {
  const glows: Record<BadgeTier, string> = {
    [BadgeTier.Diamond]: 'rgba(6, 182, 212, 0.6)',
    [BadgeTier.Platinum]: 'rgba(203, 213, 225, 0.6)',
    [BadgeTier.Gold]: 'rgba(250, 204, 21, 0.6)',
    [BadgeTier.Silver]: 'rgba(209, 213, 219, 0.5)',
    [BadgeTier.Bronze]: 'rgba(217, 119, 6, 0.5)',
  };
  return glows[tier];
}

BadgeEarnedModal.displayName = 'BadgeEarnedModal';
