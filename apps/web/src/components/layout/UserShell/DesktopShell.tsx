'use client';

import { useState, type ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { MobileCTAPill } from '@/components/layout/MobileCTAPill';
import { SearchOverlay } from '@/components/layout/SearchOverlay';
import { SideDrawer } from '@/components/layout/SideDrawer/SideDrawer';

import { SessionBanner } from './SessionBanner';
import { TopBarV2 } from './TopBarV2';

interface DesktopShellProps {
  children: ReactNode;
}

export function DesktopShell({ children }: DesktopShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  return (
    <div className="min-h-dvh flex flex-col bg-[var(--bg-base)]">
      <TopBarV2
        onHamburgerClick={() => setDrawerOpen(true)}
        onSearchClick={() => setSearchOpen(true)}
      />

      <SessionBanner />

      <main className="flex-1 overflow-y-auto">{children}</main>

      <MobileCTAPill />
      <ChatSlideOverPanel />
      <SideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
      <SearchOverlay open={searchOpen} onClose={() => setSearchOpen(false)} />
    </div>
  );
}
