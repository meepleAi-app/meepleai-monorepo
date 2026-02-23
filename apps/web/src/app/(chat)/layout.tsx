'use client';

/**
 * Chat Route Group Layout
 * Issue #5044 — Chat MiniNav + ActionBar
 *
 * Wraps all /chat/* pages with NavigationProvider so that pages can
 * call useSetNavConfig() to register MiniNav tabs and ActionBar actions.
 *
 * Includes MiniNav (L2) and NavActionBar (L3) rendered inline.
 * Both components return null when their respective configs are empty,
 * so this is non-breaking for pages that don't set nav config.
 */

import { type ReactNode, Suspense } from 'react';

import { MiniNav } from '@/components/layout/MiniNav/MiniNav';
import { NavActionBar } from '@/components/layout/ActionBar/NavActionBar';
import { NavigationProvider } from '@/context/NavigationContext';

function ChatLayoutInner({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col min-h-0 flex-1">
      {/* MiniNav (L2) — sticky at top. Renders null when no tabs configured. */}
      <div className="sticky top-12 md:top-0 z-30">
        <Suspense>
          <MiniNav />
        </Suspense>
      </div>

      {/* Page content */}
      <div className="flex-1">
        {children}
      </div>

      {/* NavActionBar (L3) — desktop: sticky bottom, mobile: fixed bottom */}
      <NavActionBar />
    </div>
  );
}

export default function ChatLayout({ children }: { children: ReactNode }) {
  return (
    <NavigationProvider>
      <ChatLayoutInner>{children}</ChatLayoutInner>
    </NavigationProvider>
  );
}
