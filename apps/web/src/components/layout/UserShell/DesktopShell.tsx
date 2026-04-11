'use client';

import type { ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';

import { MiniNavSlot } from './MiniNavSlot';
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
 *   │ main content (full width)     │
 *   └───────────────────────────────┘
 *
 * SessionBanner and MobileBottomBar added in M5.
 */
export function DesktopShell({ children }: DesktopShellProps) {
  return (
    <div className="min-h-dvh flex flex-col bg-[var(--nh-bg-base)]">
      <TopBar />
      <MiniNavSlot />
      <main className="flex-1 min-w-0 overflow-y-auto">{children}</main>
      <ChatSlideOverPanel />
    </div>
  );
}
