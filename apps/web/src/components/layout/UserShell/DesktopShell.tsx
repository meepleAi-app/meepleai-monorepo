'use client';

import type { ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { MyHandBottomBar, MyHandSidebar } from '@/components/layout/MyHand';

import { DesktopHandRail } from './DesktopHandRail';
import { MiniNavSlot } from './MiniNavSlot';
import { TopBar } from './TopBar';

interface DesktopShellProps {
  children: ReactNode;
}

/**
 * DesktopShell — new Phase 1 layout composition.
 *
 * Layout:
 *   ┌───────────────────────────────┐
 *   │ TopBar (64px sticky)        │
 *   ├───────────────────────────────┤
 *   │ MiniNavSlot (48px, optional)  │
 *   ├────┬──────────────────────────┤
 *   │ HR │ main                     │
 *   │ 76 │                          │
 *   └────┴──────────────────────────┘
 */
export function DesktopShell({ children }: DesktopShellProps) {
  return (
    <div className="min-h-dvh flex flex-col bg-[var(--nh-bg-base)]">
      <TopBar />
      <MiniNavSlot />
      <div className="flex-1 flex min-h-0">
        <DesktopHandRail />
        <main className="flex-1 min-w-0 overflow-y-auto">{children}</main>
        <div className="hidden md:flex">
          <MyHandSidebar />
        </div>
      </div>
      <div className="md:hidden">
        <MyHandBottomBar />
      </div>
      <ChatSlideOverPanel />
    </div>
  );
}
