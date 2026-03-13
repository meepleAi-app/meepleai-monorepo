/**
 * Chat Route Group Layout
 * Issue #5044 — Chat MiniNav + ActionBar
 *
 * Wraps all /chat/* pages with NavigationProvider so that pages can
 * call useSetNavConfig() to register MiniNav tabs and ActionBar actions.
 *
 * Includes NavActionBar (L3) rendered inline.
 * Tabs are now rendered in SidebarContextNav.
 * NavActionBar returns null when no actions configured, so this is
 * non-breaking for pages that don't set nav config.
 */

import { type ReactNode } from 'react';

import { NavActionBar } from '@/components/layout/ActionBar/NavActionBar';
import { NavigationProvider } from '@/context/NavigationContext';

function ChatLayoutInner({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col min-h-0 flex-1">
      {/* Page content */}
      <div className="flex-1">{children}</div>

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
