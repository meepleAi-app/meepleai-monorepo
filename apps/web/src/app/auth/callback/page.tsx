/**
 * OAuth Callback Page (AUTH-06) - App Router
 *
 * Handles OAuth redirect after user authorizes with provider.
 * Shows loading state and redirects based on success/error.
 */

import { Suspense } from 'react';

import { OAuthCallbackPageContent } from './_content';

export default function OAuthCallbackPage() {
  return (
    <Suspense
      fallback={
        <main className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-300">
          Caricamento callback OAuth...
        </main>
      }
    >
      <OAuthCallbackPageContent />
    </Suspense>
  );
}
