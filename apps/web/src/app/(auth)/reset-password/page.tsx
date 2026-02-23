/**
 * Password Reset Page (AUTH-04) - App Router
 *
 * Two-mode password reset flow with AuthLayout (Issue #2231):
 * 1. Request Mode (no token): User enters email to request reset
 * 2. Reset Mode (with token): User sets new password
 */

import { Suspense } from 'react';

import { AuthLayout } from '@/components/layouts';

import { ResetPasswordPageContent } from './_content';

export default function ResetPasswordPage() {
  return (
    <Suspense
      fallback={
        <AuthLayout title="Loading...">
          <div className="text-center py-8">
            <div className="animate-pulse text-slate-500">Loading...</div>
          </div>
        </AuthLayout>
      }
    >
      <ResetPasswordPageContent />
    </Suspense>
  );
}
