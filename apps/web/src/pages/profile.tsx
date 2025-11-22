/**
 * Profile Page - DEPRECATED (SPRINT-1, Issue #848)
 *
 * This page has been deprecated and redirects to /settings.
 * All profile functionality has been consolidated into the Settings page:
 * - Profile information → Settings > Profile tab
 * - OAuth account linking → Settings > Privacy tab
 * - 2FA management → Settings > Privacy tab
 *
 * @deprecated Use /settings instead
 */

import { useEffect } from 'react';
import { useRouter } from 'next/router';
import Head from 'next/head';

export default function ProfilePage() {
  const router = useRouter();

  useEffect(() => {
    // Redirect to settings page
    router.replace('/settings');
  }, [router]);

  return (
    <>
      <Head>
        <title>Redirecting... - MeepleAI</title>
      </Head>

      <div className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900">
        <div className="text-center space-y-4">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto" />
          <p className="text-slate-600 dark:text-slate-400">
            Redirecting to Settings page...
          </p>
        </div>
      </div>
    </>
  );
}

