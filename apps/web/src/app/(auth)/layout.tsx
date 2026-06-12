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
 *
 * Issue #2169: wraps children in <main id="main-content"> so the WCAG 2.1
 * § 1.3.1 landmark requirement is satisfied and the root-layout "Skip to
 * main content" link has a valid target. Previously the (auth) tree
 * mounted form content directly inside <body> with no landmark — axe-core
 * flagged 11x "content not contained by landmarks" + 1x "no skip link
 * target" on /login during the US-2 validation walkthrough.
 */

'use client';

import { ReactNode } from 'react';

/**
 * Auth layout wrapper.
 *
 * AuthLayout is applied directly by individual auth pages with page-
 * specific titles and subtitles. This wrapper provides the WCAG-required
 * <main> landmark + skip-link target shared across the (auth) route
 * group.
 */
export default function AuthRootLayout({ children }: { children: ReactNode }) {
  return <main id="main-content">{children}</main>;
}
