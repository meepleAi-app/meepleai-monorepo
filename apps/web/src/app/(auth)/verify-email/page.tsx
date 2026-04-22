/**
 * Verify Email Page (Issue #3076)
 *
 * Handles email verification token validation.
 * User lands here after clicking the verification link in their email.
 */

import { Suspense } from 'react';

import { AuthCard } from '@/components/ui/v2/auth-card';

import { VerifyEmailContent } from './_content';

export default function VerifyEmailPage() {
  return (
    <Suspense
      fallback={
        <AuthCard title="Email verification">
          <div className="text-center py-8">
            <div className="animate-pulse text-muted-foreground text-sm">
              Verifying your email...
            </div>
          </div>
        </AuthCard>
      }
    >
      <VerifyEmailContent />
    </Suspense>
  );
}
