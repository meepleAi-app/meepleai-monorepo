/**
 * Authenticated Route Group Layout
 *
 * Renders the shared authenticated shell (UserShellClient) for all
 * authenticated user pages. This layout is itself an (async) server
 * component, so any future per-request server data can be fetched here and
 * passed into the shell.
 * Admin pages use a separate AdminShell via admin/(dashboard)/layout.tsx.
 */

import { type ReactNode } from 'react';

import { UserShellClient } from '@/components/layout/UserShell';

export default async function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  return <UserShellClient>{children}</UserShellClient>;
}
