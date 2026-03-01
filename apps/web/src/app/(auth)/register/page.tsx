/**
 * Register Page - App Router
 *
 * Standalone registration page with AuthModal.
 * Uses AuthLayout wrapper for consistent auth page UX (Issue #2231).
 */

import { Suspense } from 'react';

import { RegisterFallback, RegisterPageContent } from './_content';

export default function RegisterPage() {
  return (
    <Suspense fallback={<RegisterFallback />}>
      <RegisterPageContent />
    </Suspense>
  );
}
