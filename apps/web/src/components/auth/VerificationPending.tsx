/**
 * VerificationPending Component (Issue #3076)
 *
 * Displays "Check your email" message after registration.
 * Features:
 * - Email display (masked for privacy)
 * - Resend verification email button
 * - Cooldown timer for rate limiting
 * - Accessible design (WCAG 2.1 AA)
 *
 * @example
 * ```tsx
 * <VerificationPending
 *   email="user@example.com"
 *   onResend={handleResend}
 *   isResending={false}
 *   cooldownSeconds={0}
 * />
 * ```
 */

'use client';

import { Mail, RefreshCw } from 'lucide-react';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { useTranslation } from '@/hooks/useTranslation';

export interface VerificationPendingProps {
  /** The email address verification was sent to */
  email: string;

  /** Callback when user clicks resend button */
  onResend: () => void;

  /** Whether a resend operation is in progress */
  isResending: boolean;

  /** Seconds remaining before resend is allowed (0 = can resend) */
  cooldownSeconds: number;

  /** Error message if resend failed */
  error?: string | null;

  /** Optional test ID for testing */
  'data-testid'?: string;
}

/**
 * Mask email for privacy display
 * user@example.com -> u***@example.com
 */
function maskEmail(email: string): string {
  const [localPart, domain] = email.split('@');
  if (!domain || localPart.length === 0) return email;

  const maskedLocal =
    localPart.length <= 2
      ? localPart[0] + '***'
      : localPart[0] + '***' + localPart[localPart.length - 1];

  return `${maskedLocal}@${domain}`;
}

export function VerificationPending({
  email,
  onResend,
  isResending,
  cooldownSeconds,
  error,
  'data-testid': testId,
}: VerificationPendingProps) {
  const { t } = useTranslation();

  const canResend = cooldownSeconds === 0 && !isResending;
  const maskedEmail = maskEmail(email);

  return (
    <div
      className="flex flex-col items-center text-center space-y-6"
      data-testid={testId}
      role="status"
      aria-live="polite"
    >
      {/* Icon */}
      <div className="w-16 h-16 bg-primary/10 dark:bg-primary/20 rounded-full flex items-center justify-center">
        <Mail
          className="w-8 h-8 text-primary"
          aria-hidden="true"
        />
      </div>

      {/* Title */}
      <div className="space-y-2">
        <h2 className="text-2xl font-bold text-foreground">
          {t('auth.emailVerification.pending.title')}
        </h2>
        <p className="text-muted-foreground">
          {t('auth.emailVerification.pending.description')}
        </p>
      </div>

      {/* Email Display */}
      <div className="bg-muted/50 dark:bg-muted/30 rounded-lg px-4 py-3 w-full">
        <p className="text-sm text-muted-foreground mb-1">
          {t('auth.emailVerification.pending.sentTo')}
        </p>
        <p className="font-medium text-foreground" aria-label={`Email: ${maskedEmail}`}>
          {maskedEmail}
        </p>
      </div>

      {/* Instructions */}
      <div className="text-sm text-muted-foreground space-y-2">
        <p>{t('auth.emailVerification.pending.instructions')}</p>
        <p className="text-xs">
          {t('auth.emailVerification.pending.spamHint')}
        </p>
      </div>

      {/* Error Message */}
      {error && (
        <div
          className="rounded-md bg-destructive/10 dark:bg-destructive/20 border border-destructive/30 p-3 w-full"
          role="alert"
          aria-live="assertive"
        >
          <p className="text-sm text-destructive">{error}</p>
        </div>
      )}

      {/* Resend Button */}
      <div className="w-full">
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
            : t('auth.emailVerification.pending.resendButton')}
        </LoadingButton>
      </div>

      {/* Cooldown Message */}
      {cooldownSeconds > 0 && (
        <p className="text-xs text-muted-foreground" aria-live="polite">
          {t('auth.emailVerification.pending.cooldownMessage', { seconds: cooldownSeconds })}
        </p>
      )}
    </div>
  );
}
