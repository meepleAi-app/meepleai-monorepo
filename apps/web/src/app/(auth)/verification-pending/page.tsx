/**
 * Verification Pending Page (Issue #3076)
 *
 * Displayed after registration to inform user to check their email.
 * Shows the email address (masked) and provides resend functionality.
 */

import { Suspense } from 'react';

import { AuthLayout } from '@/components/layouts';

import { VerificationPendingContent } from './_content';

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
