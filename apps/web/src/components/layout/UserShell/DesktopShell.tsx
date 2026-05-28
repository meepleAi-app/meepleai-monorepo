'use client';

import { useState, type ReactNode } from 'react';

import { usePathname } from 'next/navigation';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { AppTopBar } from '@/components/layout/AppNav/AppTopBar';
import { isImmersiveRoute } from '@/components/layout/AppNav/immersive-routes';
import { MobileBottomBar } from '@/components/layout/AppNav/MobileBottomBar';
import { MobileTopBar } from '@/components/layout/AppNav/MobileTopBar';
import { SideDrawer } from '@/components/layout/SideDrawer/SideDrawer';
import { cn } from '@/lib/utils';

import { SessionBanner } from './SessionBanner';

interface DesktopShellProps {
  children: ReactNode;
}

/**
 * Authenticated user shell (sp4-dashboard navbar).
 * Desktop/tablet: {@link AppTopBar}. Mobile: {@link MobileTopBar} (☰ → drawer) +
 * {@link MobileBottomBar}. The hamburger drawer holds the secondary destinations
 * ("tutto il resto"); the bottom bar holds the 5 primary tabs.
 *
 * The bottom-bar clearance padding is dropped on immersive routes, where the
 * bottom bar hides itself (kept in sync via {@link isImmersiveRoute}).
 */
export function DesktopShell({ children }: DesktopShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const pathname = usePathname();
  const immersive = isImmersiveRoute(pathname);

  return (
    <div className="min-h-dvh flex flex-col bg-[var(--bg)]">
      <AppTopBar />
      <MobileTopBar onHamburgerClick={() => setDrawerOpen(true)} />

      <SessionBanner />

      <main
        id="main-content"
        className={cn('flex-1 overflow-y-auto overflow-x-clip', !immersive && 'pb-16 md:pb-0')}
      >
        {children}
      </main>

      <ChatSlideOverPanel />
      <MobileBottomBar />
      <SideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
    </div>
  );
}
