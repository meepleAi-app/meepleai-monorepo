/**
 * Login Page (AUTH-05)
 *
 * Simple login page that displays session expiration message when redirected
 * from session timeout.
 */

import { useRouter } from 'next/router';
import Head from 'next/head';

export default function LoginPage() {
  const router = useRouter();
  const { reason } = router.query;

  const isSessionExpired = reason === 'session_expired';

  return (
    <>
      <Head>
        <title>Login - MeepleAI</title>
      </Head>

      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 px-4">
        <div className="max-w-md w-full bg-white dark:bg-slate-800 rounded-2xl shadow-xl p-8">
          {/* Logo/Title */}
          <div className="text-center mb-8">
            <h1 className="text-3xl font-bold text-slate-900 dark:text-white">
              MeepleAI
            </h1>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-400">
              AI-Powered Board Game Rules Assistant
            </p>
          </div>

          {/* Session Expired Alert */}
          {isSessionExpired && (
            <div
              className="
                mb-6
                p-4
                bg-amber-50
                dark:bg-amber-900/20
                border
                border-amber-200
                dark:border-amber-800
                rounded-lg
              "
              role="alert"
            >
              <div className="flex items-start gap-3">
                <svg
                  className="w-5 h-5 text-amber-600 dark:text-amber-400 flex-shrink-0 mt-0.5"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                  />
                </svg>
                <div>
                  <h3 className="text-sm font-semibold text-amber-900 dark:text-amber-100">
                    Session Expired
                  </h3>
                  <p className="mt-1 text-sm text-amber-800 dark:text-amber-200">
                    Your session has expired due to inactivity. Please log in again to continue.
                  </p>
                </div>
              </div>
            </div>
          )}

          {/* Login Form Placeholder */}
          <div className="space-y-6">
            <p className="text-center text-slate-600 dark:text-slate-400">
              Login functionality will be implemented here.
            </p>

            <div className="pt-4 border-t border-slate-200 dark:border-slate-700">
              <a
                href="/"
                className="
                  block
                  w-full
                  text-center
                  px-4
                  py-2
                  bg-primary-600
                  hover:bg-primary-700
                  text-white
                  font-medium
                  rounded-lg
                  transition-colors
                  focus:outline-none
                  focus:ring-2
                  focus:ring-primary-500
                  focus:ring-offset-2
                "
              >
                Return to Home
              </a>
            </div>
          </div>

          {/* Footer */}
          <p className="mt-8 text-center text-xs text-slate-500 dark:text-slate-500">
            Sessions expire after 30 days of inactivity for your security.
          </p>
        </div>
      </div>
    </>
  );
}
