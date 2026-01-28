'use client';

/**
 * Verify Email Page (Issue #3076)
 *
 * Handles email verification token validation.
 * User lands here after clicking the verification link in their email.
 *
 * Features:
 * - Automatic token verification on page load
 * - Loading state during verification
 * - Success state with auto-redirect
 * - Error handling with retry options
 * - WCAG 2.1 AA compliant
 *
 * Flow:
 * - User clicks email link → lands here with token in query params
 * - Token is verified against backend
 * - Success → show success message, redirect to dashboard
 * - Error → show error message with appropriate action
 */

import { Suspense, useEffect, useState, useCallback } from 'react';

import { Loader2 } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { VerificationError } from '@/components/auth/VerificationError';
import { VerificationSuccess } from '@/components/auth/VerificationSuccess';
import { AuthLayout } from '@/components/layouts';
import { useEmailVerification } from '@/hooks/useEmailVerification';
import { useTranslation } from '@/hooks/useTranslation';

export default function VerifyEmailPage() {
  return (
    <Suspense
      fallback={
        <AuthLayout>
          <div className="flex flex-col items-center justify-center py-12 space-y-4">
            <Loader2 className="w-8 h-8 animate-spin text-primary" />
            <p className="text-muted-foreground">Verifying your email...</p>
          </div>
        </AuthLayout>
      }
    >
      <VerifyEmailContent />
    </Suspense>
  );
}

function VerifyEmailContent() {
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
    router.push('/dashboard');
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

  // No token provided
  if (!token) {
    return (
      <AuthLayout data-testid="verify-email-page">
        <VerificationError
          errorType="invalid"
          errorMessage={t('auth.emailVerification.error.noToken')}
          onRetry={handleRetry}
          data-testid="verification-error"
        />
      </AuthLayout>
    );
  }

  // Loading state
  if (isLoading) {
    return (
      <AuthLayout data-testid="verify-email-page">
        <div className="flex flex-col items-center justify-center py-12 space-y-4">
          <Loader2 className="w-8 h-8 animate-spin text-primary" aria-hidden="true" />
          <p className="text-muted-foreground" role="status" aria-live="polite">
            {t('auth.emailVerification.verifying')}
          </p>
        </div>
      </AuthLayout>
    );
  }

  // Success state
  if (isVerified) {
    return (
      <AuthLayout data-testid="verify-email-page">
        <VerificationSuccess
          email={email || undefined}
          redirectUrl="/dashboard"
          autoRedirectSeconds={3}
          onRedirect={handleRedirect}
          data-testid="verification-success"
        />
      </AuthLayout>
    );
  }

  // Error state
  if (error && errorType) {
    return (
      <AuthLayout data-testid="verify-email-page">
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
      </AuthLayout>
    );
  }

  // Default loading (should not reach here normally)
  return (
    <AuthLayout data-testid="verify-email-page">
      <div className="flex flex-col items-center justify-center py-12 space-y-4">
        <Loader2 className="w-8 h-8 animate-spin text-primary" aria-hidden="true" />
        <p className="text-muted-foreground">{t('auth.emailVerification.verifying')}</p>
      </div>
    </AuthLayout>
  );
}
