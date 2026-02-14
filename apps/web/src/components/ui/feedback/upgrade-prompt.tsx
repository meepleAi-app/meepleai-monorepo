/**
 * Upgrade Prompt Component
 * Epic #4068 - Issue #4179
 *
 * Shows upgrade CTA when user tries to access tier-locked features
 */

'use client';

import React from 'react';

import { Crown, ArrowRight, Sparkles } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { UserTier } from '@/types/permissions';

interface UpgradePromptProps {
  /** Required tier for the locked feature */
  requiredTier: Exclude<UserTier, 'free'>;
  /** Feature name that triggered the prompt */
  featureName?: string;
  /** Variant: inline (in-card) | modal (overlay) */
  variant?: 'inline' | 'modal';
  /** Optional callback when upgrade button clicked */
  onUpgrade?: () => void;
  /** Additional CSS classes */
  className?: string;
}

const TIER_LABELS: Record<Exclude<UserTier, 'free'>, string> = {
  normal: 'Normal',
  premium: 'Premium',
  pro: 'Pro',
  enterprise: 'Enterprise'
};

const TIER_BENEFITS: Record<Exclude<UserTier, 'free'>, string[]> = {
  normal: ['100 games', '500MB storage', 'Drag & drop'],
  premium: ['500 games', '5GB storage', 'Bulk actions', 'Agent creation'],
  pro: ['500 games', '5GB storage', 'Bulk actions', 'Agent creation'], // Alias
  enterprise: ['Unlimited games', 'Unlimited storage', 'Advanced analytics', 'Priority support']
};

/**
 * Inline variant - compact prompt shown within card
 */
function InlineUpgradePrompt({ requiredTier, featureName, onUpgrade, className }: UpgradePromptProps) {
  return (
    <div
      className={cn(
        'flex items-center gap-2 px-3 py-2 rounded-lg',
        'bg-gradient-to-r from-amber-50 to-amber-100/80',
        'border border-amber-200/60',
        'backdrop-blur-sm',
        className
      )}
      role="alert"
      aria-live="polite"
    >
      <Crown className="w-4 h-4 text-amber-600 flex-shrink-0" />
      <div className="flex-1 min-w-0">
        <p className="text-xs font-medium text-amber-900">
          {featureName ? `${featureName} requires` : 'Upgrade to'}{' '}
          <span className="font-bold">{TIER_LABELS[requiredTier]}</span>
        </p>
      </div>
      <button
        onClick={onUpgrade}
        className={cn(
          'inline-flex items-center gap-1 px-2 py-1 rounded-md',
          'text-xs font-semibold text-amber-900',
          'bg-amber-200/60 hover:bg-amber-300/80',
          'transition-colors duration-200',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-400'
        )}
        aria-label={`Upgrade to ${TIER_LABELS[requiredTier]}`}
      >
        Upgrade
        <ArrowRight className="w-3 h-3" />
      </button>
    </div>
  );
}

/**
 * Modal variant - prominent overlay prompt
 */
function ModalUpgradePrompt({ requiredTier, featureName, onUpgrade, className }: UpgradePromptProps) {
  return (
    <div
      className={cn(
        'absolute inset-0 z-30',
        'flex items-center justify-center',
        'bg-black/40 backdrop-blur-sm',
        'rounded-xl',
        'opacity-0 group-hover:opacity-100',
        'transition-opacity duration-300',
        className
      )}
      role="dialog"
      aria-modal="true"
      aria-labelledby="upgrade-prompt-title"
    >
      <div className="bg-card/95 backdrop-blur-md rounded-lg p-6 max-w-xs border border-border/50 shadow-xl">
        <div className="flex items-center gap-2 mb-3">
          <div className="flex items-center justify-center w-10 h-10 rounded-full bg-gradient-to-br from-amber-400 to-amber-600">
            <Crown className="w-5 h-5 text-white" />
          </div>
          <h3
            id="upgrade-prompt-title"
            className="text-lg font-bold text-foreground"
          >
            {TIER_LABELS[requiredTier]} Required
          </h3>
        </div>

        {featureName && (
          <p className="text-sm text-muted-foreground mb-4">
            <span className="font-semibold text-foreground">{featureName}</span> is available on{' '}
            {TIER_LABELS[requiredTier]} tier and above.
          </p>
        )}

        <div className="mb-4">
          <p className="text-xs font-semibold text-muted-foreground mb-2">Includes:</p>
          <ul className="space-y-1">
            {TIER_BENEFITS[requiredTier].map((benefit, i) => (
              <li key={i} className="flex items-center gap-2 text-xs text-foreground">
                <Sparkles className="w-3 h-3 text-amber-500 flex-shrink-0" />
                <span>{benefit}</span>
              </li>
            ))}
          </ul>
        </div>

        <button
          onClick={onUpgrade}
          className={cn(
            'w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg',
            'text-sm font-bold text-white',
            'bg-gradient-to-r from-amber-500 to-amber-600',
            'hover:from-amber-600 hover:to-amber-700',
            'shadow-md hover:shadow-lg',
            'transition-all duration-200',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-400'
          )}
          aria-label={`Upgrade to ${TIER_LABELS[requiredTier]} tier`}
        >
          <Crown className="w-4 h-4" />
          Upgrade to {TIER_LABELS[requiredTier]}
          <ArrowRight className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}

/**
 * Main UpgradePrompt component with variant support
 */
export function UpgradePrompt(props: UpgradePromptProps) {
  const { variant = 'inline' } = props;

  return variant === 'modal' ? (
    <ModalUpgradePrompt {...props} />
  ) : (
    <InlineUpgradePrompt {...props} />
  );
}
