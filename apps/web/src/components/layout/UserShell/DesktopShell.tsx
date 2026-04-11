'use client';

import type { ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { MobileBottomBar } from '@/components/layout/MobileBottomBar';

import { MiniNavSlot } from './MiniNavSlot';
import { SessionBanner } from './SessionBanner';
import { TopBar } from './TopBar';

interface DesktopShellProps {
  children: ReactNode;
}

/**
 * DesktopShell — Page-first layout.
 *
 * Layout:
 *   ┌───────────────────────────────┐
 *   │ TopBar (64px sticky)          │
 *   ├───────────────────────────────┤
 *   │ MiniNavSlot (48px, optional)  │
 *   ├───────────────────────────────┤
 *   │ SessionBanner (32px, session) │
 *   ├───────────────────────────────┤
 *   │ main content (full width)     │
 *   └───────────────────────────────┘
 */
export function DesktopShell({ children }: DesktopShellProps) {
  return (
    <div className="min-h-dvh flex flex-col bg-[var(--nh-bg-base)]">
      <TopBar />
      <MiniNavSlot />
      <SessionBanner />
      <main className="flex-1 min-w-0 overflow-y-auto">{children}</main>
      <ChatSlideOverPanel />
      <MobileBottomBar />
    </div>
  );
}
