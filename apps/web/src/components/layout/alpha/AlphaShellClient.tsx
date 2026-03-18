'use client';

/**
 * AlphaShellClient — Client component providing the alpha layout structure.
 *
 * Layout:
 * - Desktop: sidebar + (topnav + main content)
 * - Mobile: topnav + main content + bottom tab bar
 *
 * The SwipeableContainer holds the 4 tab panels (HomeFeed, Library, Play, Chat).
 * Route-based {children} are rendered over the tab panels when navigating
 * to specific routes (e.g. /sessions/live/[id]).
 *
 * Uses h-dvh (dynamic viewport height) for mobile-safe full height.
 */

import { useRef, type ReactNode } from 'react';

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
 * Routes that should show the tab panels as default content.
 * Any other route renders {children} over the panels.
 */
const TAB_PANEL_ROUTES = ['/', '/alpha', '/home'];

interface AlphaShellClientProps {
  children: ReactNode;
  isAdmin: boolean;
}

export function AlphaShellClient({ children, isAdmin }: AlphaShellClientProps) {
  const mainRef = useRef<HTMLElement>(null);
  const pathname = usePathname();

  // Show tab panels when on a root/home route; otherwise show route-based content
  const showTabPanels = TAB_PANEL_ROUTES.includes(pathname);

  return (
    <div className="flex h-dvh bg-background">
      <AlphaDesktopSidebar isAdmin={isAdmin} />

      <div className="flex flex-col flex-1 min-w-0">
        <AlphaTopNav isAdmin={isAdmin} scrollContainerRef={mainRef} />

        {showTabPanels ? (
          <SwipeableContainer>
            <HomeFeed />
            <LibraryPanel />
            <PlayPanel />
            <ChatPanel />
          </SwipeableContainer>
        ) : (
          <main ref={mainRef} className="flex-1 overflow-y-auto pb-16 lg:pb-0">
            {children}
          </main>
        )}

        <AlphaTabBar />
      </div>
    </div>
  );
}
