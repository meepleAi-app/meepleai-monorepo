'use client';

import { useEffect, useState, useCallback } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { VerificationError } from '@/components/auth/VerificationError';
import { VerificationSuccess } from '@/components/auth/VerificationSuccess';
import { AuthCard } from '@/components/ui/v2/auth-card';
import { useEmailVerification } from '@/hooks/useEmailVerification';
import { useTranslation } from '@/hooks/useTranslation';

export function VerifyEmailContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t } = useTranslation();

  const token = searchParams?.get('token') ?? null;
  const emailFromParams = searchParams?.get('email') ?? null;

  const {
    isLoading,
    isResending,
    error,
    errorType,
    isVerified,
    cooldownSeconds,
    verifyEmail,
    resendVerificationEmail,
  } = useEmailVerification();

  const [hasAttempted, setHasAttempted] = useState(false);
  const [email, setEmail] = useState<string | null>(null);

  // Get email from params or session storage for resend functionality
  useEffect(() => {
    if (emailFromParams) {
      setEmail(emailFromParams);
    } else {
      const storedEmail = sessionStorage.getItem('pendingVerificationEmail');
      if (storedEmail) {
        setEmail(storedEmail);
      }
    }
  }, [emailFromParams]);

  // Verify token on mount
  useEffect(() => {
    if (token && !hasAttempted) {
      setHasAttempted(true);
      void verifyEmail(token);
    }
  }, [token, hasAttempted, verifyEmail]);

  /**
   * Handle redirect to dashboard
   */
  const handleRedirect = useCallback(() => {
    // Clear stored email after successful verification
    sessionStorage.removeItem('pendingVerificationEmail');
    router.push('/library');
  }, [router]);

  /**
   * Handle resend verification email
   */
  const handleResend = async () => {
    if (!email) {
      // Redirect to register if no email available
      router.push('/register');
      return;
    }
    await resendVerificationEmail(email);
  };

  /**
   * Handle go to login
   */
  const handleGoToLogin = () => {
    sessionStorage.removeItem('pendingVerificationEmail');
    router.push('/login');
  };

  /**
   * Handle retry (go back to verification pending)
   */
  const handleRetry = () => {
    if (email) {
      router.push(`/verification-pending?email=${encodeURIComponent(email)}`);
    } else {
      router.push('/register');
    }
  };

  /**
   * Render a loading branch (used for both in-flight verification and the
   * brief pre-verification mount window). The loading message is announced
   * to assistive tech only when the verification is actually in progress.
   */
  const renderLoading = (withStatusRole: boolean) => (
    <AuthCard title={t('auth.emailVerification.pageTitle')}>
      <div className="text-center py-8" data-testid="verify-email-page">
        <div
          className="animate-pulse text-muted-foreground text-sm"
          {...(withStatusRole ? { role: 'status', 'aria-live': 'polite' } : {})}
        >
          {t('auth.emailVerification.verifying')}
        </div>
      </div>
    </AuthCard>
  );

  // No token provided
  if (!token) {
    return (
      <AuthCard title={t('auth.emailVerification.pageTitle')}>
        <div data-testid="verify-email-page">
          <VerificationError
            errorType="invalid"
            errorMessage={t('auth.emailVerification.error.noToken')}
            onRetry={handleRetry}
            data-testid="verification-error"
          />
        </div>
      </AuthCard>
    );
  }

  // Loading state
  if (isLoading) {
    return renderLoading(true);
  }

  // Success state
  if (isVerified) {
    return (
      <AuthCard title={t('auth.emailVerification.pageTitle')}>
        <div data-testid="verify-email-page">
          <VerificationSuccess
            email={email || undefined}
            redirectUrl="/library"
            autoRedirectSeconds={3}
            onRedirect={handleRedirect}
            data-testid="verification-success"
          />
        </div>
      </AuthCard>
    );
  }

  // Error state
  if (error && errorType) {
    return (
      <AuthCard title={t('auth.emailVerification.pageTitle')}>
        <div data-testid="verify-email-page">
          <VerificationError
            errorType={errorType}
            errorMessage={error}
            onResend={email ? handleResend : undefined}
            isResending={isResending}
            cooldownSeconds={cooldownSeconds}
            onGoToLogin={handleGoToLogin}
            onRetry={handleRetry}
            data-testid="verification-error"
          />
        </div>
      </AuthCard>
    );
  }

  // Default loading (brief window between mount and verifyEmail setting isLoading)
  return renderLoading(false);
}
