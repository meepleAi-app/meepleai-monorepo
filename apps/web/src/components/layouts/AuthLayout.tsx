/**
 * AuthLayout Component - Issue #2231
 *
 * Specialized layout for authentication pages (login, register, reset-password).
 * Provides a minimal, centered design optimized for auth UX.
 *
 * Features:
 * - Minimal header (logo + home link only)
 * - Centered card container (max-width 450px)
 * - Minimal footer (Privacy + Terms links)
 * - Dark mode support
 * - Responsive mobile (full-width with padding)
 * - Accessible navigation and landmarks
 *
 * Design Philosophy:
 * - Separation of concerns from PublicLayout
 * - Optimized for focused auth flows
 * - Clean, distraction-free UX
 *
 * @example
 * ```tsx
 * <AuthLayout title="Welcome Back" subtitle="Sign in to continue">
 *   <LoginForm />
 * </AuthLayout>
 * ```
 */

'use client';

import { ReactNode } from 'react';
import Link from 'next/link';
import { MeepleLogo } from '@/components/ui/meeple';
import { cn } from '@/lib/utils';

export interface AuthLayoutProps {
  /** Page content (typically auth forms) */
  children: ReactNode;
  /** Optional page title */
  title?: string;
  /** Optional subtitle/description */
  subtitle?: string;
  /** Show back link to home (default: true) */
  showBackLink?: boolean;
  /** Additional className for customization */
  className?: string;
  /** Optional test ID for testing */
  'data-testid'?: string;
}

export function AuthLayout({
  children,
  title,
  subtitle,
  showBackLink = true,
  className,
  'data-testid': testId,
}: AuthLayoutProps) {
  return (
    <div
      className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-950 dark:to-slate-900"
      data-testid={testId}
    >
      {/* Minimal Header */}
      <header className="w-full py-4 px-4 sm:px-6 lg:px-8">
        <div className="max-w-7xl mx-auto flex justify-between items-center">
          {/* Logo - Links to home */}
          <Link
            href="/"
            className="flex items-center gap-3 hover:opacity-80 transition-opacity focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 rounded-lg"
            aria-label="MeepleAI Home"
          >
            <MeepleLogo variant="full" size="md" />
          </Link>

          {/* Optional back link */}
          {showBackLink && (
            <Link
              href="/"
              className="text-sm text-slate-600 dark:text-slate-400 hover:text-primary dark:hover:text-primary transition-colors focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 rounded px-2 py-1"
              aria-label="Back to home"
            >
              ← Back to Home
            </Link>
          )}
        </div>
      </header>

      {/* Main Content - Centered Card */}
      <main className="flex-1 flex items-center justify-center px-4 py-8 sm:px-6 lg:px-8">
        <div className={cn('w-full max-w-md', className)}>
          {/* Optional Title Section */}
          {(title || subtitle) && (
            <div className="text-center mb-8 space-y-2">
              {title && (
                <h1 className="text-3xl font-bold text-slate-900 dark:text-slate-50">{title}</h1>
              )}
              {subtitle && <p className="text-sm text-slate-600 dark:text-slate-400">{subtitle}</p>}
            </div>
          )}

          {/* Auth Content Card */}
          <div className="bg-white dark:bg-slate-800 rounded-lg shadow-lg p-6 sm:p-8">
            {children}
          </div>

          {/* Security Notice */}
          <div className="mt-6 text-center">
            <p className="text-xs text-slate-500 dark:text-slate-500 flex items-center justify-center gap-2">
              <span className="text-green-500" role="img" aria-label="Security indicator">
                🔒
              </span>
              <span>Secured with industry-standard encryption</span>
            </p>
          </div>
        </div>
      </main>

      {/* Minimal Footer */}
      <footer className="w-full py-6 px-4 sm:px-6 lg:px-8 border-t border-slate-200 dark:border-slate-800">
        <div className="max-w-7xl mx-auto">
          <nav className="flex justify-center items-center gap-6" aria-label="Footer navigation">
            <Link
              href="/privacy"
              className="text-sm text-slate-600 dark:text-slate-400 hover:text-primary dark:hover:text-primary transition-colors focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 rounded px-2 py-1"
            >
              Privacy Policy
            </Link>
            <span className="text-slate-400 dark:text-slate-600" aria-hidden="true">
              •
            </span>
            <Link
              href="/terms"
              className="text-sm text-slate-600 dark:text-slate-400 hover:text-primary dark:hover:text-primary transition-colors focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 rounded px-2 py-1"
            >
              Terms of Service
            </Link>
          </nav>

          {/* Copyright */}
          <div className="mt-4 text-center">
            <p className="text-xs text-slate-500 dark:text-slate-600">
              © {new Date().getFullYear()} MeepleAI. All rights reserved.
            </p>
          </div>
        </div>
      </footer>
    </div>
  );
}
