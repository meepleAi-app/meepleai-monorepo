/**
 * Password Reset Page (AUTH-04) — v2 migration (Task 13).
 *
 * Two-mode password reset flow wrapped in a Suspense boundary so that the
 * client-only `useSearchParams()` hook inside `_content.tsx` is allowed.
 */

import { Suspense } from 'react';

import { ResetPasswordFallback, ResetPasswordPageContent } from './_content';

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={<ResetPasswordFallback />}>
      <ResetPasswordPageContent />
    </Suspense>
  );
}
