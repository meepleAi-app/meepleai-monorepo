/**
 * Quick Ask Layout
 *
 * Minimal layout for the voice-first Quick Ask page.
 * No sidebar, no MiniNav — just a gradient background shell.
 */

import type { ReactNode } from 'react';

export default function QuickAskLayout({ children }: { children: ReactNode }) {
  return <div className="min-h-dvh bg-gradient-to-b from-background to-muted/30">{children}</div>;
}
