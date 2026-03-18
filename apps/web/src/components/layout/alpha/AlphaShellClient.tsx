'use client';

/**
 * AlphaShellClient — Client component providing the alpha layout structure.
 *
 * Layout:
 * - Desktop: sidebar + (topnav + main content)
 * - Mobile: topnav + main content + bottom tab bar
 *
 * The SwipeableContainer holds the 4 tab panels (HomeFeed, Library, Play, Chat)
 * and is always rendered. When the user navigates to a specific route (e.g.
 * /sessions/live/[id], /profile, /settings), the route content is rendered as
 * a full overlay on top of the panels.
 *
 * Uses h-dvh (dynamic viewport height) for mobile-safe full height.
 */

import { type ReactNode } from 'react';

import { usePathname } from 'next/navigation';

import { ChatPanel } from '@/components/features/chat/ChatPanel';
import { HomeFeed } from '@/components/features/home/HomeFeed';
import { LibraryPanel } from '@/components/features/library/LibraryPanel';
import { PlayPanel } from '@/components/features/play/PlayPanel';

import { AlphaDesktopSidebar } from './AlphaDesktopSidebar';
import { AlphaTabBar } from './AlphaTabBar';
import { AlphaTopNav } from './AlphaTopNav';
import { SwipeableContainer } from './SwipeableContainer';

/**
 * Routes where ONLY the tab panels are shown (no overlay).
 * All other routes render {children} as an overlay on top of the panels.
 */
const TAB_PANEL_ROUTES = ['/', '/alpha', '/home', '/dashboard'];

interface AlphaShellClientProps {
  children: ReactNode;
  isAdmin: boolean;
}

export function AlphaShellClient({ children, isAdmin }: AlphaShellClientProps) {
  const pathname = usePathname();

  // On root/dashboard routes, only tab panels are visible (no overlay)
  const isTabPanelRoute = TAB_PANEL_ROUTES.includes(pathname);

  return (
    <div className="flex h-dvh bg-background">
      <AlphaDesktopSidebar isAdmin={isAdmin} />

      <div className="flex flex-col flex-1 min-w-0">
        <AlphaTopNav isAdmin={isAdmin} />

        {/* Tab panels are always rendered as the background content */}
        <div className={isTabPanelRoute ? 'flex-1 min-h-0' : 'hidden'}>
          <SwipeableContainer>
            <HomeFeed />
            <LibraryPanel />
            <PlayPanel />
            <ChatPanel />
          </SwipeableContainer>
        </div>

        {/* Route-based content rendered as overlay when not on a tab panel route */}
        {!isTabPanelRoute && (
          <main className="flex-1 overflow-y-auto pb-16 lg:pb-0">{children}</main>
        )}

        <AlphaTabBar />
      </div>
    </div>
  );
}
