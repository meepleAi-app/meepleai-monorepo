/**
 * Password Reset Page (AUTH-04) — v2 migration (Task 13).
 *
 * Two-mode password reset flow wrapped in a Suspense boundary so that the
 * client-only `useSearchParams()` hook inside `_content.tsx` is allowed.
 */

import { Suspense } from 'react';

import { Metadata } from 'next';

import { ResetPasswordFallback, ResetPasswordPageContent } from './_content';

export const metadata: Metadata = {
  title: 'Reimposta password | MeepleAI',
  description: 'Reimposta la password del tuo account MeepleAI in modo sicuro.',
  robots: { index: false, follow: false },
};

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={<ResetPasswordFallback />}>
      <ResetPasswordPageContent />
    </Suspense>
  );
}
