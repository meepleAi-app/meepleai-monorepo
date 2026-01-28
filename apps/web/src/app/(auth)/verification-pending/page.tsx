'use client';

/**
 * Verification Pending Page (Issue #3076)
 *
 * Displayed after registration to inform user to check their email.
 * Shows the email address (masked) and provides resend functionality.
 *
 * Features:
 * - Email masking for privacy
 * - Resend verification email with rate limiting
 * - Cooldown timer (60 seconds)
 * - Error handling
 * - WCAG 2.1 AA compliant
 *
 * Flow:
 * - User registers → redirected here with email in query params
 * - User can resend verification email (rate limited)
 * - User checks email and clicks verification link
 */

import { Suspense, useEffect, useState } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { VerificationPending } from '@/components/auth/VerificationPending';
import { AuthLayout } from '@/components/layouts';
import { useEmailVerification } from '@/hooks/useEmailVerification';
import { useTranslation } from '@/hooks/useTranslation';

export default function VerificationPendingPage() {
  return (
    <Suspense
      fallback={
        <AuthLayout>
          <div className="text-center py-8">
            <div className="animate-pulse text-muted-foreground">Loading...</div>
          </div>
        </AuthLayout>
      }
    >
      <VerificationPendingContent />
    </Suspense>
  );
}

function VerificationPendingContent() {
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

  // If no email, redirect to register
  if (!email) {
    return (
      <AuthLayout>
        <div className="text-center py-8 space-y-4">
          <p className="text-muted-foreground">
            {t('auth.emailVerification.pending.noEmail')}
          </p>
          <button
            onClick={() => router.push('/register')}
            className="text-primary hover:text-primary/80 underline"
          >
            {t('auth.emailVerification.pending.goToRegister')}
          </button>
        </div>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout data-testid="verification-pending-page">
      <VerificationPending
        email={email}
        onResend={handleResend}
        isResending={isResending}
        cooldownSeconds={cooldownSeconds}
        error={error}
        data-testid="verification-pending-component"
      />
    </AuthLayout>
  );
}
