'use client';

/**
 * AlphaShellClient — Client component providing the alpha layout structure.
 *
 * Layout:
 * - Desktop: sidebar + (topnav + main content)
 * - Mobile: topnav + main content + bottom tab bar
 *
 * Uses h-dvh (dynamic viewport height) for mobile-safe full height.
 */

import { useRef, type ReactNode } from 'react';

import { AlphaDesktopSidebar } from './AlphaDesktopSidebar';
import { AlphaTabBar } from './AlphaTabBar';
import { AlphaTopNav } from './AlphaTopNav';

interface AlphaShellClientProps {
  children: ReactNode;
  isAdmin: boolean;
}

export function AlphaShellClient({ children, isAdmin }: AlphaShellClientProps) {
  const mainRef = useRef<HTMLElement>(null);

  return (
    <div className="flex h-dvh bg-background">
      <AlphaDesktopSidebar isAdmin={isAdmin} className="hidden lg:flex" />

      <div className="flex flex-col flex-1 min-w-0">
        <AlphaTopNav isAdmin={isAdmin} scrollContainerRef={mainRef} />

        <main ref={mainRef} className="flex-1 overflow-y-auto pb-16 lg:pb-0">
          {children}
        </main>

        <AlphaTabBar />
      </div>
    </div>
  );
}
