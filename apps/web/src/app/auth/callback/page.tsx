'use client';

/**
 * OAuth Callback Page (AUTH-06) - App Router
 *
 * Handles OAuth redirect after user authorizes with provider.
 * Shows loading state and redirects based on success/error.
 */

import { Suspense, useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';

function OAuthCallbackPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const success = searchParams?.get('success') ?? null;
  const error = searchParams?.get('error') ?? null;
  const isNewUser = searchParams?.get('new') ?? null;
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');

  useEffect(() => {
    if (success === 'true') {
      setStatus('success');
      const _message =
        isNewUser === 'true'
          ? 'Welcome! Your account has been created.'
          : 'Successfully logged in!';

      setTimeout(() => {
        router.push('/dashboard');
      }, 1500);
    } else if (error) {
      setStatus('error');
      setTimeout(() => {
        router.push('/login');
      }, 3000);
    }
  }, [success, error, isNewUser, router]);

  return (
    <main className="min-h-dvh flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800">
      <div className="max-w-md w-full bg-white dark:bg-slate-800 rounded-2xl shadow-xl p-8 text-center">
        {status === 'loading' && (
          <>
            <div className="animate-spin rounded-full h-16 w-16 border-b-2 border-blue-600 mx-auto mb-4" />
            <h2 className="text-xl font-semibold text-slate-900 dark:text-white">
              Processing login...
            </h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-400">
              Please wait while we complete your authentication.
            </p>
          </>
        )}

        {status === 'success' && (
          <>
            <svg
              className="mx-auto h-16 w-16 text-green-600"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
            <h2 className="mt-4 text-xl font-semibold text-slate-900 dark:text-white">
              {isNewUser === 'true' ? 'Welcome!' : 'Login successful!'}
            </h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-400">
              Redirecting to dashboard...
            </p>
          </>
        )}

        {status === 'error' && (
          <>
            <svg
              className="mx-auto h-16 w-16 text-red-600"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
            <h2 className="mt-4 text-xl font-semibold text-slate-900 dark:text-white">
              Login failed
            </h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-400">
              Something went wrong. Redirecting to login page...
            </p>
          </>
        )}
      </div>
    </main>
  );
}

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
