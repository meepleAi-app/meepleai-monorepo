/**
 * Login Page (AUTH-05) - App Router
 *
 * Standalone login page with session expiration handling.
 * Uses AuthLayout wrapper for consistent auth page UX (Issue #2231).
 *
 * Note (FE-IMP-005): Client-side only - AuthModal uses TanStack Query
 */

import { Suspense } from 'react';

import { LoginFallback, LoginPageContent } from './_content';

export default function LoginPage() {
  return (
    <Suspense fallback={<LoginFallback />}>
      <LoginPageContent />
    </Suspense>
  );
}
