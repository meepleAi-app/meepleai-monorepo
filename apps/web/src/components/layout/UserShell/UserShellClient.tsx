'use client';

import { type ReactNode } from 'react';

import { usePathname } from 'next/navigation';

import { DashboardEngineProvider } from '@/components/dashboard-v2';
import { ChatPanel } from '@/components/features/chat/ChatPanel';
import { HomeFeed } from '@/components/features/home/HomeFeed';
import { LibraryPanel } from '@/components/features/library/LibraryPanel';
import { PlayPanel } from '@/components/features/play/PlayPanel';

import { SwipeableContainer } from './SwipeableContainer';
import { UserDesktopSidebar } from './UserDesktopSidebar';
import { UserTabBar } from './UserTabBar';
import { UserTopNav } from './UserTopNav';

const TAB_PANEL_ROUTES = ['/', '/home', '/dashboard'];

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  const pathname = usePathname();
  const isTabPanelRoute = TAB_PANEL_ROUTES.includes(pathname);

  return (
    <div className="flex h-dvh bg-background">
      <UserDesktopSidebar />

      <div className="flex flex-col flex-1 min-w-0">
        <UserTopNav />

        <DashboardEngineProvider>
          {/* Tab panels always mounted (hidden via CSS) to preserve scroll/state */}
          <div className={isTabPanelRoute ? 'flex-1 min-h-0' : 'hidden'}>
            <SwipeableContainer>
              <HomeFeed />
              <LibraryPanel />
              <PlayPanel />
              <ChatPanel />
            </SwipeableContainer>
          </div>

          {/* Route content rendered as overlay when not on a tab panel route */}
          {!isTabPanelRoute && (
            <main className="flex-1 overflow-y-auto pb-16 lg:pb-0">{children}</main>
          )}
        </DashboardEngineProvider>

        <UserTabBar />
      </div>
    </div>
  );
}
