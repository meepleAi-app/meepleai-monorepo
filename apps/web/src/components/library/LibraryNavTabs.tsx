/**
 * LibraryNavTabs Component
 * Issue #4055 - Add Navigation Links for Private Games Section
 *
 * Route-based tab navigation for library section pages.
 * Features:
 * - Route-based active state (via usePathname)
 * - Sticky tab bar with glass morphism
 * - Keyboard accessible (Arrow keys, Home/End)
 * - Mobile responsive with horizontal scroll
 * - ARIA compliant (tablist, tab roles)
 */

'use client';

import React from 'react';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';

import { LIBRARY_TABS, getActiveLibraryTab } from '@/config/library-navigation';
import { cn } from '@/lib/utils';

export function LibraryNavTabs() {
  const pathname = usePathname() ?? '/library';
  const searchParams = useSearchParams();
  const search = searchParams?.toString() ? `?${searchParams.toString()}` : '';
  const activeTabId = getActiveLibraryTab(pathname, search);

  const handleKeyDown = (e: React.KeyboardEvent, tabIndex: number) => {
    let newIndex = tabIndex;

    if (e.key === 'ArrowRight') {
      e.preventDefault();
      newIndex = tabIndex < LIBRARY_TABS.length - 1 ? tabIndex + 1 : 0;
    } else if (e.key === 'ArrowLeft') {
      e.preventDefault();
      newIndex = tabIndex > 0 ? tabIndex - 1 : LIBRARY_TABS.length - 1;
    } else if (e.key === 'Home') {
      e.preventDefault();
      newIndex = 0;
    } else if (e.key === 'End') {
      e.preventDefault();
      newIndex = LIBRARY_TABS.length - 1;
    } else {
      return;
    }

    const newTab = LIBRARY_TABS[newIndex];
    if (newTab) {
      const tabEl = document.querySelector(
        `[data-testid="library-tab-${newTab.id}"]`
      ) as HTMLAnchorElement;
      tabEl?.focus();
    }
  };

  return (
    <div
      className="border-b border-zinc-200/50 dark:border-zinc-700/50 bg-white/30 dark:bg-zinc-900/30 backdrop-blur-sm"
      role="tablist"
      aria-label="Library sections"
      data-testid="library-nav-tabs"
    >
      <nav className="container mx-auto flex gap-0 px-4 overflow-x-auto scrollbar-hide">
        {LIBRARY_TABS.map((tab, index) => {
          const Icon = tab.icon;
          const isActive = tab.id === activeTabId;

          return (
            <Link
              key={tab.id}
              href={tab.href}
              role="tab"
              aria-selected={isActive}
              aria-controls={`library-tabpanel-${tab.id}`}
              tabIndex={isActive ? 0 : -1}
              onKeyDown={(e) => handleKeyDown(e, index)}
              className={cn(
                'flex items-center gap-2 px-4 py-3 text-sm font-medium whitespace-nowrap transition-colors border-b-2 -mb-px',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2 rounded-t-sm',
                isActive
                  ? 'border-amber-500 text-amber-600 dark:text-amber-400'
                  : 'border-transparent text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-300 hover:border-zinc-300 dark:hover:border-zinc-600'
              )}
              data-testid={`library-tab-${tab.id}`}
            >
              <Icon className="h-4 w-4" aria-hidden="true" />
              <span>{tab.label}</span>
            </Link>
          );
        })}
      </nav>
    </div>
  );
}

export default LibraryNavTabs;
