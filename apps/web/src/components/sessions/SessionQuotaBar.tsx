/* eslint-disable security/detect-object-injection */
/**
 * SessionQuotaBar Component (Issue #3075)
 *
 * Displays session quota usage with visual progress bar and tier information.
 * Shows warning when user approaches or reaches session limit.
 * Used in dashboard and session creation flow.
 */

import { AlertCircle, ChevronRight, Infinity as InfinityIcon, Loader2 } from 'lucide-react';
import Link from 'next/link';

import { Progress } from '@/components/ui/feedback/progress';
import { cn } from '@/lib/utils';

export interface SessionQuotaBarProps {
  /** Number of currently active sessions */
  currentSessions: number;
  /** Maximum allowed sessions for user's tier */
  maxSessions: number;
  /** User's current tier */
  userTier: string;
  /** Remaining slots available */
  remainingSlots: number;
  /** Whether user can create a new session */
  canCreateNew: boolean;
  /** Whether user has unlimited sessions (admin) */
  isUnlimited: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Show upgrade link to next tier (default: true) */
  showUpgradeLink?: boolean;
  /** Show loading state */
  isLoading?: boolean;
  /** Compact mode for smaller displays */
  compact?: boolean;
}

const TIER_LABELS: Record<string, string> = {
  free: 'Free',
  normal: 'Normal',
  premium: 'Premium',
};

const TIER_UPGRADE_INFO: Record<string, { next: string; benefit: string }> = {
  free: { next: 'Normal', benefit: 'sessioni illimitate' },
  normal: { next: 'Premium', benefit: 'tutte le funzionalita' },
};

/**
 * Calculate percentage of quota used
 */
function calculatePercentage(current: number, max: number, isUnlimited: boolean): number {
  if (isUnlimited || max === 0) return 0;
  return Math.round((current / max) * 100);
}

export function SessionQuotaBar({
  currentSessions,
  maxSessions,
  userTier,
  remainingSlots,
  canCreateNew,
  isUnlimited,
  className,
  showUpgradeLink = true,
  isLoading = false,
  compact = false,
}: SessionQuotaBarProps) {
  const percentageUsed = calculatePercentage(currentSessions, maxSessions, isUnlimited);
  const isNearLimit = !isUnlimited && percentageUsed >= 80;
  const isAtLimit = !canCreateNew && !isUnlimited;
  const tierLabel = TIER_LABELS[userTier.toLowerCase()] || userTier;
  const canUpgrade = userTier.toLowerCase() !== 'premium' && !isUnlimited;
  const upgradeInfo = TIER_UPGRADE_INFO[userTier.toLowerCase()];

  if (isLoading) {
    return (
      <div
        className={cn(
          'rounded-lg border bg-card p-4 text-card-foreground shadow-sm',
          compact && 'p-3',
          className
        )}
      >
        <div className="flex items-center gap-2">
          <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          <span className="text-sm text-muted-foreground">Caricamento quota sessioni...</span>
        </div>
      </div>
    );
  }

  // Unlimited users get a simplified display
  if (isUnlimited) {
    return (
      <div
        className={cn(
          'rounded-lg border bg-card p-4 text-card-foreground shadow-sm',
          compact && 'p-3',
          className
        )}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <InfinityIcon className="h-4 w-4 text-primary" />
            <span className={cn('font-medium', compact ? 'text-sm' : 'text-sm')}>
              {currentSessions} sessioni attive
            </span>
          </div>
          <span className="text-xs text-muted-foreground">Sessioni illimitate</span>
        </div>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'rounded-lg border bg-card text-card-foreground shadow-sm',
        compact ? 'p-3' : 'p-4',
        isAtLimit && 'border-destructive/50 bg-destructive/5',
        isNearLimit && !isAtLimit && 'border-yellow-500/50 bg-yellow-50/50 dark:bg-yellow-950/20',
        className
      )}
    >
      {/* Header with count */}
      <div className={cn('flex items-center justify-between', compact ? 'mb-1.5' : 'mb-2')}>
        <h3 className={cn('font-medium', compact ? 'text-xs' : 'text-sm')}>
          Sessioni attive: {currentSessions}/{maxSessions}
        </h3>
        {isAtLimit && (
          <div
            className={cn('flex items-center gap-1 text-destructive', compact ? 'text-[10px]' : 'text-xs')}
          >
            <AlertCircle className={cn(compact ? 'h-2.5 w-2.5' : 'h-3 w-3')} />
            <span>Limite raggiunto</span>
          </div>
        )}
      </div>

      {/* Progress bar */}
      <Progress
        value={percentageUsed}
        className={cn(
          compact ? 'h-1.5' : 'h-2',
          isAtLimit && 'bg-destructive/20',
          isNearLimit && !isAtLimit && 'bg-yellow-200 dark:bg-yellow-900'
        )}
      />

      {/* Footer with tier and upgrade info */}
      <div
        className={cn(
          'flex items-center justify-between text-muted-foreground',
          compact ? 'mt-1.5 text-[10px]' : 'mt-2 text-xs'
        )}
      >
        <span>
          Piano {tierLabel}
          {remainingSlots > 0 && ` \u2022 ${remainingSlots} slot disponibili`}
        </span>

        {canUpgrade && showUpgradeLink && upgradeInfo && (
          <Link
            href="/account/subscription"
            className={cn(
              'flex items-center gap-1 font-medium transition-colors hover:text-foreground',
              isNearLimit && 'text-yellow-700 dark:text-yellow-400',
              isAtLimit && 'text-destructive'
            )}
          >
            {compact ? (
              <>
                Upgrade
                <ChevronRight className="h-2.5 w-2.5" />
              </>
            ) : (
              <>
                Passa a {upgradeInfo.next}
                <ChevronRight className="h-3 w-3" />
              </>
            )}
          </Link>
        )}
      </div>

      {/* Warning messages (only in non-compact mode) */}
      {!compact && (
        <>
          {isNearLimit && !isAtLimit && (
            <div className="mt-3 rounded-md bg-yellow-100 p-2 text-xs text-yellow-800 dark:bg-yellow-950 dark:text-yellow-200">
              <p>Stai per raggiungere il limite di sessioni attive.</p>
            </div>
          )}

          {isAtLimit && (
            <div className="mt-3 rounded-md bg-destructive/10 p-2 text-xs text-destructive">
              <p>
                Hai raggiunto il limite di {maxSessions} sessioni attive.{' '}
                {canUpgrade && upgradeInfo ? (
                  <>
                    <Link href="/account/subscription" className="font-medium underline">
                      Passa a {upgradeInfo.next}
                    </Link>{' '}
                    o termina una sessione esistente per iniziarne una nuova.
                  </>
                ) : (
                  'Termina una sessione esistente per iniziarne una nuova.'
                )}
              </p>
            </div>
          )}
        </>
      )}
    </div>
  );
}
