/**
 * VerificationError Component (Issue #3076)
 *
 * Displays error states for email verification.
 * Handles different error scenarios with appropriate messages and actions.
 *
 * Error Types:
 * - expired: Token has expired, offer to resend
 * - invalid: Token is invalid/malformed
 * - already_verified: Email already verified, redirect to login
 * - not_found: Token not found in database
 * - rate_limited: Too many requests, show cooldown
 * - unknown: Generic error with retry option
 *
 * @example
 * ```tsx
 * <VerificationError
 *   errorType="expired"
 *   onResend={handleResend}
 *   onGoToLogin={handleGoToLogin}
 * />
 * ```
 */

'use client';

import { AlertCircle, Clock, CheckCircle2, XCircle, ShieldAlert, RefreshCw } from 'lucide-react';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import type { EmailVerificationErrorType } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

export interface VerificationErrorProps {
  /** Type of verification error */
  errorType: EmailVerificationErrorType;

  /** Custom error message (overrides default) */
  errorMessage?: string;

  /** Callback when user wants to resend verification */
  onResend?: () => void;

  /** Whether resend operation is in progress */
  isResending?: boolean;

  /** Cooldown seconds for resend button */
  cooldownSeconds?: number;

  /** Callback to navigate to login */
  onGoToLogin?: () => void;

  /** Callback to go back/retry */
  onRetry?: () => void;

  /** Optional test ID */
  'data-testid'?: string;
}

/**
 * Get icon component for error type
 */
function getErrorIcon(errorType: EmailVerificationErrorType) {
  switch (errorType) {
    case 'expired':
      return Clock;
    case 'already_verified':
      return CheckCircle2;
    case 'invalid':
    case 'not_found':
      return XCircle;
    case 'rate_limited':
      return ShieldAlert;
    default:
      return AlertCircle;
  }
}

/**
 * Get icon color class for error type
 */
function getIconColorClass(errorType: EmailVerificationErrorType): string {
  switch (errorType) {
    case 'already_verified':
      return 'text-green-500 dark:text-green-400';
    case 'expired':
      return 'text-amber-500 dark:text-amber-400';
    case 'rate_limited':
      return 'text-orange-500 dark:text-orange-400';
    default:
      return 'text-destructive';
  }
}

/**
 * Get background color class for error type
 */
function getBackgroundClass(errorType: EmailVerificationErrorType): string {
  switch (errorType) {
    case 'already_verified':
      return 'bg-green-500/10 dark:bg-green-500/20';
    case 'expired':
      return 'bg-amber-500/10 dark:bg-amber-500/20';
    case 'rate_limited':
      return 'bg-orange-500/10 dark:bg-orange-500/20';
    default:
      return 'bg-destructive/10 dark:bg-destructive/20';
  }
}

export function VerificationError({
  errorType,
  errorMessage,
  onResend,
  isResending = false,
  cooldownSeconds = 0,
  onGoToLogin,
  onRetry,
  'data-testid': testId,
}: VerificationErrorProps) {
  const { t } = useTranslation();

  const Icon = getErrorIcon(errorType);
  const iconColorClass = getIconColorClass(errorType);
  const backgroundClass = getBackgroundClass(errorType);

  // Get translated messages based on error type
  const title = t(`auth.emailVerification.error.${errorType}.title`);
  const description = errorMessage || t(`auth.emailVerification.error.${errorType}.description`);

  // Determine which actions to show
  const showResend = ['expired', 'unknown'].includes(errorType) && onResend;
  const showLogin = errorType === 'already_verified' && onGoToLogin;
  const showRetry = ['invalid', 'not_found', 'unknown'].includes(errorType) && onRetry;
  const canResend = cooldownSeconds === 0 && !isResending;

  return (
    <div
      className="flex flex-col items-center text-center space-y-6"
      data-testid={testId}
      role="alert"
      aria-live="assertive"
    >
      {/* Icon */}
      <div
        className={cn(
          'w-16 h-16 rounded-full flex items-center justify-center',
          backgroundClass
        )}
      >
        <Icon className={cn('w-8 h-8', iconColorClass)} aria-hidden="true" />
      </div>

      {/* Title and Description */}
      <div className="space-y-2">
        <h2 className="text-2xl font-bold text-foreground">{title}</h2>
        <p className="text-muted-foreground">{description}</p>
      </div>

      {/* Actions */}
      <div className="w-full space-y-3">
        {/* Resend Button (for expired/unknown errors) */}
        {showResend && (
          <LoadingButton
            variant="outline"
            className="w-full"
            onClick={onResend}
            disabled={!canResend}
            isLoading={isResending}
            loadingText={t('auth.emailVerification.pending.resending')}
            data-testid="resend-verification-button"
          >
            <RefreshCw className="w-4 h-4 mr-2" aria-hidden="true" />
            {cooldownSeconds > 0
              ? t('auth.emailVerification.pending.resendCooldown', { seconds: cooldownSeconds })
              : t('auth.emailVerification.error.resendButton')}
          </LoadingButton>
        )}

        {/* Go to Login Button (for already_verified) */}
        {showLogin && (
          <Button className="w-full" onClick={onGoToLogin} data-testid="go-to-login-button">
            {t('auth.emailVerification.error.goToLogin')}
          </Button>
        )}

        {/* Retry Button (for invalid/not_found errors) */}
        {showRetry && (
          <Button
            variant="outline"
            className="w-full"
            onClick={onRetry}
            data-testid="retry-button"
          >
            {t('auth.emailVerification.error.tryAgain')}
          </Button>
        )}

        {/* Rate Limited - Show cooldown message */}
        {errorType === 'rate_limited' && cooldownSeconds > 0 && (
          <p className="text-sm text-muted-foreground">
            {t('auth.emailVerification.pending.cooldownMessage', { seconds: cooldownSeconds })}
          </p>
        )}
      </div>

      {/* Help Text */}
      <p className="text-xs text-muted-foreground">
        {t('auth.emailVerification.error.helpText')}
      </p>
    </div>
  );
}
