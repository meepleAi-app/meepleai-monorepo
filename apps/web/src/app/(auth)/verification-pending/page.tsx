/**
 * Verification Pending Page (Issue #3076)
 *
 * Displayed after registration to inform user to check their email.
 * Shows the email address (masked) and provides resend functionality.
 *
 * Task 12 (auth-flow-v2 migration): uses v2 AuthCard primitive via _content.tsx.
 */

import { Suspense } from 'react';

import { VerificationPendingContent } from './_content';

export default function VerificationPendingPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-dvh items-center justify-center bg-background">
          <div className="animate-pulse text-muted-foreground text-sm">Loading...</div>
        </div>
      }
    >
      <VerificationPendingContent />
    </Suspense>
  );
}
