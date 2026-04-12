'use client';

import type { ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { ActionPill } from '@/components/layout/ActionPill';
import { HandRail } from '@/components/layout/HandRail';
import { MobileBottomBar } from '@/components/layout/MobileBottomBar';

import { MiniNavSlot } from './MiniNavSlot';
import { SessionBanner } from './SessionBanner';
import { TopBar } from './TopBar';

interface DesktopShellProps {
  children: ReactNode;
}

/**
 * DesktopShell — Hand-First layout.
 *
 * Layout:
 *   ┌─────────────────────────────────────────────┐
 *   │ TopBar (56px sticky)                        │
 *   ├──────────────┬──────────────────────────────┤
 *   │ HandRail     │ MiniNavSlot (48px, optional) │
 *   │ (64→200px,   ├──────────────────────────────┤
 *   │  hidden mob) │ SessionBanner (32px, session) │
 *   │              ├──────────────────────────────┤
 *   │              │ main content                  │
 *   │              │      [ActionPill floating]    │
 *   └──────────────┴──────────────────────────────┘
 */
export function DesktopShell({ children }: DesktopShellProps) {
  return (
    <div className="min-h-dvh flex flex-col bg-[var(--nh-bg-base)]">
      <TopBar />
      <div className="flex flex-1 min-h-0">
        <HandRail />
        <div className="flex flex-col flex-1 min-w-0">
          <MiniNavSlot />
          <SessionBanner />
          <main className="flex-1 min-w-0 overflow-y-auto relative">
            {children}
            <ActionPill />
          </main>
        </div>
      </div>
      <ChatSlideOverPanel />
      <MobileBottomBar />
    </div>
  );
}
