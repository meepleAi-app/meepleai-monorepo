/**
 * AppShell — Unified layout shell (RSC wrapper)
 *
 * React Server Component that reads the sidebar cookie via next/headers
 * and passes the initial collapsed state to the client component,
 * eliminating the sidebar flash on page load.
 *
 * Replaces both LayoutShell (authenticated) and PublicLayout (public)
 * with a single auth-aware component.
 */

import { cookies } from 'next/headers';
import { type ReactNode } from 'react';

import { parseSidebarCookie, SIDEBAR_COOKIE_NAME } from '@/lib/cookies/sidebar-cookie';

import { AppShellClient } from './AppShellClient';

export interface AppShellProps {
  children: ReactNode;
  /** Remove horizontal padding from the content area (for full-bleed pages) */
  fullWidth?: boolean;
  /** Additional className for the main element */
  className?: string;
}

export async function AppShell({ children, fullWidth, className }: AppShellProps) {
  const cookieStore = await cookies();
  const initialSidebarCollapsed = parseSidebarCookie(
    cookieStore.get(SIDEBAR_COOKIE_NAME)?.value
  );

  return (
    <AppShellClient
      initialSidebarCollapsed={initialSidebarCollapsed}
      fullWidth={fullWidth}
      className={className}
    >
      {children}
    </AppShellClient>
  );
}
