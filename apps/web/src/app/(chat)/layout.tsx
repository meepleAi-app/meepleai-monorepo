/**
 * Chat Route Group Layout
 *
 * Uses UserShell for the chat experience.
 * (chat) is a sibling of (authenticated), so it needs its own shell wrapper.
 *
 * Issue #293 — PageTransition pilot: when NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS=true,
 * wraps children in a fade transition. Feature-flagged to allow runtime A/B.
 * Scope limited to the chat route group (smallest blast radius).
 */

'use client';

import { type ReactNode } from 'react';

import { UserShell } from '@/components/layout/UserShell';
import { PageTransition } from '@/components/ui/animations/PageTransition';

const PAGE_TRANSITIONS_ENABLED = process.env.NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS === 'true';

export default function ChatLayout({ children }: { children: ReactNode }) {
  const content = PAGE_TRANSITIONS_ENABLED ? (
    <PageTransition variant="fade">{children}</PageTransition>
  ) : (
    children
  );

  return <UserShell>{content}</UserShell>;
}
