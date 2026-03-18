/**
 * AlphaShell — Stub layout shell for the alpha UI redesign.
 *
 * Activated via the `alpha-layout` feature flag.
 * This is a placeholder that will be replaced with the full
 * alpha navigation system in Task 1.1.
 */

import { type ReactNode } from 'react';

interface AlphaShellProps {
  children: ReactNode;
  isAdmin?: boolean;
}

export function AlphaShell({ children }: AlphaShellProps) {
  return <div className="alpha-shell">{children}</div>;
}
