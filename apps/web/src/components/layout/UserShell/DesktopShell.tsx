'use client';

import { useState, type ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { AppTopBar } from '@/components/layout/AppNav/AppTopBar';
import { MobileBottomBar } from '@/components/layout/AppNav/MobileBottomBar';
import { MobileTopBar } from '@/components/layout/AppNav/MobileTopBar';
import { SideDrawer } from '@/components/layout/SideDrawer/SideDrawer';

import { SessionBanner } from './SessionBanner';

interface DesktopShellProps {
  children: ReactNode;
}

/**
 * Authenticated user shell (sp4-dashboard navbar).
 * Desktop/tablet: {@link AppTopBar}. Mobile: {@link MobileTopBar} (☰ → drawer) +
 * {@link MobileBottomBar}. The hamburger drawer holds the secondary destinations
 * ("tutto il resto"); the bottom bar holds the 5 primary tabs.
 */
export function DesktopShell({ children }: DesktopShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <div className="min-h-dvh flex flex-col bg-[var(--bg)]">
      <AppTopBar />
      <MobileTopBar onHamburgerClick={() => setDrawerOpen(true)} />

      <SessionBanner />

      <main id="main-content" className="flex-1 overflow-y-auto overflow-x-clip pb-16 md:pb-0">
        {children}
      </main>

      <ChatSlideOverPanel />
      <MobileBottomBar />
      <SideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
    </div>
  );
}
