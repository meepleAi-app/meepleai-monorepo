'use client';

import { useEffect, useState } from 'react';

import { Mail, RefreshCw } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { AuthCard } from '@/components/ui/auth-card';
import { Btn } from '@/components/ui/btn';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
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

/**
 * #2773 — `emailParam` arrives as a PROP from the async Server Component
 * page.tsx (which reads searchParams). Reading it as a prop instead of via the
 * `useSearchParams` client hook lets this component render server-side (SSR),
 * dropping the CSR bailout. Same pattern as `login/_content.tsx` (#2650).
 */
export interface VerificationPendingContentProps {
  /** `?email=` address; falls back to sessionStorage when absent. */
  emailParam?: string | null;
}

export function VerificationPendingContent({ emailParam = null }: VerificationPendingContentProps) {
  const router = useRouter();
  const { t } = useTranslation();

  const emailFromParams = emailParam;

  // #4183: the banner «Verifica ora» brings signed-in users here. They may have neither ?email=
  // nor sessionStorage, so the session's own email comes before the stored one. Same source as
  // the banner (useCurrentUser): AuthProvider's user was measured null on this page for a
  // signed-in user, because it is not refreshed after a client-side login.
  const { data: currentUser, isLoading: authLoading } = useCurrentUser();
  const sessionEmail = currentUser?.email ?? null;

  // sessionStorage exists only in the browser, so it is read after mount.
  const [storedEmail, setStoredEmail] = useState<string | null>(null);
  const [storageChecked, setStorageChecked] = useState(false);

  const { isResending, error, cooldownSeconds, resendVerificationEmail, clearError } =
    useEmailVerification();

  useEffect(() => {
    if (emailFromParams) {
      // Store in session storage for page refresh
      sessionStorage.setItem('pendingVerificationEmail', emailFromParams);
    } else {
      setStoredEmail(sessionStorage.getItem('pendingVerificationEmail'));
    }
    setStorageChecked(true);
  }, [emailFromParams]);

  // Resolved during render, not in an effect: an effect-set email left the first frame empty,
  // and the «no email» card flashed even when the email was already known.
  const email = emailFromParams ?? sessionEmail ?? storedEmail;

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

  // While the session or sessionStorage is still unknown the email may be about to arrive:
  // showing the «no email» card now would offer /register to a user who is already signed in.
  if (!email && (authLoading || !storageChecked)) {
    return <AuthCard title={t('auth.emailVerification.pending.title')}>{null}</AuthCard>;
  }

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
