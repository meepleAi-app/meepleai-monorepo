/**
 * AlphaShell — Server component layout shell for the alpha UI redesign.
 *
 * Activated via the `alpha-layout` feature flag in (authenticated)/layout.tsx.
 * Mirrors the UnifiedShell pattern: server component receives isAdmin from
 * the parent layout and delegates rendering to AlphaShellClient.
 */

import { type ReactNode } from 'react';

import { AlphaShellClient } from './AlphaShellClient';

interface AlphaShellProps {
  children: ReactNode;
  isAdmin?: boolean;
}

export async function AlphaShell({ children, isAdmin = false }: AlphaShellProps) {
  return <AlphaShellClient isAdmin={isAdmin}>{children}</AlphaShellClient>;
}
