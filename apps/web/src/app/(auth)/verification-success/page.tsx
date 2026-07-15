'use client';

/**
 * Verification Success Page (Issue #3076)
 *
 * Alternative success page after email verification.
 * Can be used as a direct redirect destination if needed.
 *
 * Features:
 * - Success animation
 * - Auto-redirect to dashboard
 * - Manual continue button
 * - WCAG 2.1 AA compliant
 *
 * Note: In most cases, users will see the success state on /verify-email page.
 * This page exists as a fallback/alternative route.
 */

import { Suspense } from 'react';

import { useRouter } from 'next/navigation';

import { VerificationSuccess } from '@/components/auth/VerificationSuccess';
import { AuthLayout } from '@/components/layouts';

export default function VerificationSuccessPage() {
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
      <VerificationSuccessContent />
    </Suspense>
  );
}

function VerificationSuccessContent() {
  const router = useRouter();

  /**
   * Handle redirect to dashboard
   */
  const handleRedirect = () => {
    // Clear any stored verification email
    sessionStorage.removeItem('pendingVerificationEmail');
    router.push('/library');
  };

  return (
    <AuthLayout data-testid="verification-success-page">
      <VerificationSuccess
        redirectUrl="/library"
        autoRedirectSeconds={3}
        onRedirect={handleRedirect}
        data-testid="verification-success-component"
      />
    </AuthLayout>
  );
}
