/**
 * Public Route Group Layout
 * Issue #2233 - Phase 4: Route Groups
 * Updated: Issue #3104 - Use UnifiedHeader (auth handled internally)
 *
 * Applies PublicLayout to all pages in (public) group:
 * - / (home)
 * - /games
 * - /dashboard
 * - /settings
 * - /sessions
 * - /giochi
 * - /board-game-ai
 *
 * Features:
 * - UnifiedHeader with user context (internally managed)
 * - PublicFooter
 * - UnifiedActionBar for authenticated mobile users
 * - Responsive container
 * - Dark mode support
 */

'use client';

import { ReactNode } from 'react';

import { PublicLayout } from '@/components/layouts/PublicLayout';

export default function PublicRootLayout({ children }: { children: ReactNode }) {
  return <PublicLayout showNewsletter={false}>{children}</PublicLayout>;
}
