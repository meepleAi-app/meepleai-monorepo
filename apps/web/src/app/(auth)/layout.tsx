/**
 * Auth Route Group Layout
 * Issue #2233 - Phase 4: Route Groups
 *
 * Applies AuthLayout to all pages in (auth) group:
 * - /login
 * - /register
 * - /reset-password
 * - /oauth-callback
 *
 * Features:
 * - Minimal header (logo + home link)
 * - Centered card container (max-width 450px)
 * - Minimal footer (Privacy + Terms)
 * - Dark mode support
 * - Optimized for auth flows
 */

'use client';

import { ReactNode } from 'react';

/**
 * Auth layout wrapper
 *
 * AuthLayout is applied directly by individual auth pages
 * with page-specific titles and subtitles.
 *
 * This wrapper just provides a consistent container.
 */
export default function AuthRootLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
