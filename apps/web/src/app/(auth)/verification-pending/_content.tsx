'use client';

import { useEffect, useState } from 'react';

import { Mail, RefreshCw } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { AuthCard } from '@/components/ui/v2/auth-card';
import { Btn } from '@/components/ui/v2/btn';
import { useEmailVerification } from '@/hooks/useEmailVerification';
import { useTranslation } from '@/hooks/useTranslation';

/**
 * Mask email for privacy display
 * user@example.com -> u***r@example.com
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

export function VerificationPendingContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t } = useTranslation();

  // Get email from query params or session storage
  const emailFromParams = searchParams?.get('email') ?? null;
  const [email, setEmail] = useState<string | null>(null);

  const { isResending, error, cooldownSeconds, resendVerificationEmail, clearError } =
    useEmailVerification();

  // Initialize email from params or session storage
  useEffect(() => {
    if (emailFromParams) {
      setEmail(emailFromParams);
      // Store in session storage for page refresh
      sessionStorage.setItem('pendingVerificationEmail', emailFromParams);
    } else {
      // Try to get from session storage
      const storedEmail = sessionStorage.getItem('pendingVerificationEmail');
      if (storedEmail) {
        setEmail(storedEmail);
      }
    }
  }, [emailFromParams]);

  // Clear error when component mounts
  useEffect(() => {
    clearError();
  }, [clearError]);

  /**
   * Handle resend verification email
   */
  const handleResend = async () => {
    if (!email) return;
    await resendVerificationEmail(email);
  };

  // If no email, render a minimal AuthCard with a redirect CTA
  if (!email) {
    return (
      <AuthCard title={t('auth.emailVerification.pending.title')}>
        <div className="text-center py-4 space-y-4" data-testid="verification-pending-no-email">
          <p className="text-muted-foreground text-sm">
            {t('auth.emailVerification.pending.noEmail')}
          </p>
          <button
            type="button"
            onClick={() => router.push('/register')}
            className="text-primary hover:text-primary/80 underline"
          >
            {t('auth.emailVerification.pending.goToRegister')}
          </button>
        </div>
      </AuthCard>
    );
  }

  const maskedEmail = maskEmail(email);
  const canResend = cooldownSeconds === 0 && !isResending;

  return (
    <AuthCard title={t('auth.emailVerification.pending.title')}>
      <div
        className="flex flex-col items-center text-center space-y-6"
        data-testid="verification-pending-page"
        role="status"
        aria-live="polite"
      >
        {/* Icon */}
        <div className="w-16 h-16 bg-primary/10 dark:bg-primary/20 rounded-full flex items-center justify-center">
          <Mail className="w-8 h-8 text-primary" aria-hidden="true" />
        </div>

        {/* Description */}
        <p className="text-muted-foreground text-sm">
          {t('auth.emailVerification.pending.description')}
        </p>

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
          <p className="text-xs">{t('auth.emailVerification.pending.spamHint')}</p>
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
        <Btn
          data-testid="resend-verification-button"
          variant="outline"
          fullWidth
          loading={isResending}
          disabled={!canResend}
          leftIcon={isResending ? undefined : <RefreshCw className="w-4 h-4" aria-hidden="true" />}
          onClick={handleResend}
        >
          {isResending
            ? t('auth.emailVerification.pending.resending')
            : cooldownSeconds > 0
              ? t('auth.emailVerification.pending.resendCooldown', {
                  seconds: cooldownSeconds,
                })
              : t('auth.emailVerification.pending.resendButton')}
        </Btn>

        {/* Cooldown Message */}
        {cooldownSeconds > 0 && (
          <p className="text-xs text-muted-foreground" aria-live="polite">
            {t('auth.emailVerification.pending.cooldownMessage', {
              seconds: cooldownSeconds,
            })}
          </p>
        )}
      </div>
    </AuthCard>
  );
}
