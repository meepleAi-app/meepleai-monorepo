/**
 * LockedSlotCard - Premium upgrade CTA with locked slot visualization
 * Issue #3247 (FRONT-011)
 *
 * Features:
 * - Premium benefits list
 * - Upgrade button with purple glow + pulse animation
 * - Pricing modal (placeholder for Stripe)
 * - Analytics tracking
 */

'use client';

import { useState } from 'react';

import { Lock, Sparkles, Zap, Clock, Headphones, X } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

interface LockedSlotCardProps {
  /** Number of locked slots */
  lockedCount: number;
  /** Current tier name */
  currentTier?: string;
  /** Callback when user clicks upgrade */
  onUpgradeClick?: () => void;
}

interface PremiumBenefit {
  icon: React.ReactNode;
  title: string;
  description: string;
}

const PREMIUM_BENEFITS: PremiumBenefit[] = [
  {
    icon: <Sparkles className="h-4 w-4 text-purple-400" />,
    title: '+3 additional agent slots',
    description: '5 total concurrent agents',
  },
  {
    icon: <Zap className="h-4 w-4 text-purple-400" />,
    title: 'Priority model access',
    description: 'GPT-4, Claude-3.5 Sonnet',
  },
  {
    icon: <Clock className="h-4 w-4 text-purple-400" />,
    title: 'Extended token quota',
    description: '2000 tokens/week',
  },
  {
    icon: <Headphones className="h-4 w-4 text-purple-400" />,
    title: 'Priority support',
    description: 'Early access to new features',
  },
];

/**
 * Track upgrade CTA analytics event
 */
function trackUpgradeClick(lockedSlotsCount: number, currentTier: string): void {
  // Analytics tracking - integrate with your analytics provider
  if (typeof window !== 'undefined' && 'gtag' in window) {
    (window as { gtag?: (...args: unknown[]) => void }).gtag?.('event', 'upgrade_cta_clicked', {
      source: 'slot_management',
      current_tier: currentTier,
      locked_slots_count: lockedSlotsCount,
    });
  }

  // Console log for development - using warn to satisfy lint
  if (process.env.NODE_ENV === 'development') {
     
    console.warn('[Analytics] upgrade_cta_clicked', {
      source: 'slot_management',
      current_tier: currentTier,
      locked_slots_count: lockedSlotsCount,
    });
  }
}

export function LockedSlotCard({
  lockedCount,
  currentTier = 'free',
  onUpgradeClick,
}: LockedSlotCardProps): React.JSX.Element {
  const [isPricingModalOpen, setIsPricingModalOpen] = useState(false);

  const handleUpgradeClick = () => {
    trackUpgradeClick(lockedCount, currentTier);
    setIsPricingModalOpen(true);
    onUpgradeClick?.();
  };

  return (
    <>
      {/* Locked Slot Card */}
      <div
        className="relative rounded-xl border-2 border-purple-500/30 bg-gradient-to-br from-purple-500/10 via-purple-600/5 to-slate-900 p-5 transition-all hover:border-purple-500/50 hover:shadow-lg hover:shadow-purple-500/10"
        role="region"
        aria-label="Premium upgrade card"
      >
        {/* Lock Icon Header */}
        <div className="flex items-center gap-3 mb-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-purple-500/20 border border-purple-500/30">
            <Lock className="h-5 w-5 text-purple-400" />
          </div>
          <div>
            <h3 className="font-semibold text-white text-sm">
              {lockedCount} Locked Slot{lockedCount !== 1 ? 's' : ''}
            </h3>
            <p className="text-xs text-purple-300/70">Upgrade to unlock</p>
          </div>
        </div>

        {/* Premium Benefits List */}
        <div className="space-y-3 mb-5">
          {PREMIUM_BENEFITS.map((benefit, index) => (
            <div key={index} className="flex items-start gap-2.5">
              <div className="mt-0.5 shrink-0">{benefit.icon}</div>
              <div>
                <p className="text-xs font-medium text-white">{benefit.title}</p>
                <p className="text-[10px] text-slate-400">{benefit.description}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Upgrade Button with Purple Glow + Pulse */}
        <Button
          onClick={handleUpgradeClick}
          className="w-full bg-gradient-to-r from-purple-600 to-purple-500 hover:from-purple-500 hover:to-purple-400 text-white font-bold py-2.5 shadow-lg shadow-purple-500/25 agent-pulse-purple transition-all hover:shadow-purple-500/40 hover:scale-[1.02]"
          aria-label={`Upgrade to premium for ${lockedCount} additional slots`}
        >
          <Sparkles className="h-4 w-4 mr-2" />
          Upgrade to Premium
        </Button>

        {/* Tier Badge */}
        <div className="absolute top-3 right-3">
          <span className="px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider rounded-full bg-slate-800 text-slate-400 border border-slate-700">
            {currentTier}
          </span>
        </div>
      </div>

      {/* Pricing Modal (Placeholder for Stripe) */}
      <Dialog open={isPricingModalOpen} onOpenChange={setIsPricingModalOpen}>
        <DialogContent className="sm:max-w-md bg-slate-900 border-slate-800">
          <DialogHeader>
            <DialogTitle className="text-white flex items-center gap-2">
              <Sparkles className="h-5 w-5 text-purple-400" />
              Premium Tier Coming Soon!
            </DialogTitle>
            <DialogDescription className="text-slate-400">
              Join the waitlist to be notified when Premium is available.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            {/* Pricing Preview */}
            <div className="rounded-lg border border-purple-500/30 bg-purple-500/10 p-4">
              <div className="flex items-baseline gap-2 mb-2">
                <span className="text-3xl font-bold text-white">€9.99</span>
                <span className="text-sm text-slate-400">/month</span>
              </div>
              <p className="text-xs text-slate-300">
                Unlock all premium features and maximize your board game experience.
              </p>
            </div>

            {/* Benefits Summary */}
            <div className="space-y-2">
              {PREMIUM_BENEFITS.map((benefit, index) => (
                <div key={index} className="flex items-center gap-2 text-sm">
                  <span className="text-purple-400">✓</span>
                  <span className="text-slate-300">{benefit.title}</span>
                </div>
              ))}
            </div>

            {/* Waitlist Form Placeholder */}
            <div className="pt-2">
              <Button
                className="w-full bg-purple-600 hover:bg-purple-500 text-white"
                onClick={() => {
                  // TODO: Integrate with waitlist API
                  setIsPricingModalOpen(false);
                }}
              >
                Join Waitlist
              </Button>
              <p className="text-[10px] text-center text-slate-500 mt-2">
                We&apos;ll notify you when Premium launches. No spam, promise.
              </p>
            </div>
          </div>

          {/* Close button */}
          <button
            onClick={() => setIsPricingModalOpen(false)}
            className="absolute top-4 right-4 rounded-sm opacity-70 ring-offset-slate-900 transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-purple-400 focus:ring-offset-2"
            aria-label="Close"
          >
            <X className="h-4 w-4 text-slate-400" />
          </button>
        </DialogContent>
      </Dialog>
    </>
  );
}
