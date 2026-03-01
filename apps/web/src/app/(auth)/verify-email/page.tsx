/**
 * Verify Email Page (Issue #3076)
 *
 * Handles email verification token validation.
 * User lands here after clicking the verification link in their email.
 */

import { Suspense } from 'react';

import { Loader2 } from 'lucide-react';

import { AuthLayout } from '@/components/layouts';

import { VerifyEmailContent } from './_content';

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
