/**
 * EnterpriseTabSystem Component
 * Issue #3689 - Layout Base & Navigation System
 *
 * Horizontal tab navigation for enterprise admin sections.
 * Features:
 * - URL-based tab state (?tab=tokens)
 * - Sticky tab bar below section header
 * - Active tab styling with amber accent
 * - Tab overflow scroll on mobile
 * - Keyboard accessible
 */

'use client';

import React from 'react';

import { useRouter, useSearchParams, usePathname } from 'next/navigation';

import type { EnterpriseTab } from '@/config/enterprise-navigation';
import { cn } from '@/lib/utils';

export interface EnterpriseTabSystemProps {
  /** Available tabs for current section */
  tabs: EnterpriseTab[];
  /** Currently active tab ID (from URL or default) */
  activeTab: string;
  /** Children rendered below tabs */
  children?: React.ReactNode;
  /** Additional className for the tab bar */
  className?: string;
}

export function EnterpriseTabSystem({
  tabs,
  activeTab,
  children,
  className,
}: EnterpriseTabSystemProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const handleTabChange = (tabId: string) => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.set('tab', tabId);
    router.push(`${pathname}?${params.toString()}`, { scroll: false });
  };

  const handleKeyDown = (e: React.KeyboardEvent, tabIndex: number) => {
    let newIndex = tabIndex;

    if (e.key === 'ArrowRight') {
      e.preventDefault();
      newIndex = tabIndex < tabs.length - 1 ? tabIndex + 1 : 0;
    } else if (e.key === 'ArrowLeft') {
      e.preventDefault();
      newIndex = tabIndex > 0 ? tabIndex - 1 : tabs.length - 1;
    } else if (e.key === 'Home') {
      e.preventDefault();
      newIndex = 0;
    } else if (e.key === 'End') {
      e.preventDefault();
      newIndex = tabs.length - 1;
    } else {
      return;
    }

    const newTab = tabs[newIndex];
    if (newTab) {
      handleTabChange(newTab.id);
      // Focus the new tab button
      const tabEl = document.querySelector(`[data-testid="enterprise-tab-${newTab.id}"]`) as HTMLButtonElement;
      tabEl?.focus();
    }
  };

  return (
    <div className={cn('flex flex-col', className)}>
      {/* Sticky tab bar */}
      <div
        className="border-b border-zinc-200/50 dark:border-zinc-700/50 bg-white/30 dark:bg-zinc-900/30 backdrop-blur-sm sticky top-[73px] z-10"
        role="tablist"
        aria-label="Section tabs"
      >
        <nav className="flex gap-0 px-6 overflow-x-auto scrollbar-hide">
          {tabs.map((tab, index) => {
            const Icon = tab.icon;
            const isActive = tab.id === activeTab;

            return (
              <button
                key={tab.id}
                role="tab"
                aria-selected={isActive}
                aria-controls={`tabpanel-${tab.id}`}
                tabIndex={isActive ? 0 : -1}
                onClick={() => handleTabChange(tab.id)}
                onKeyDown={(e) => handleKeyDown(e, index)}
                className={cn(
                  'flex items-center gap-2 px-4 py-3 text-sm font-medium whitespace-nowrap transition-colors border-b-2 -mb-px',
                  isActive
                    ? 'border-amber-500 text-amber-600 dark:text-amber-400'
                    : 'border-transparent text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-300 hover:border-zinc-300 dark:hover:border-zinc-600'
                )}
                data-testid={`enterprise-tab-${tab.id}`}
              >
                <Icon className="h-4 w-4" aria-hidden="true" />
                <span>{tab.label}</span>
              </button>
            );
          })}
        </nav>
      </div>

      {/* Tab content panel */}
      <div
        role="tabpanel"
        id={`tabpanel-${activeTab}`}
        aria-labelledby={`enterprise-tab-${activeTab}`}
        className="flex-1"
      >
        {children}
      </div>
    </div>
  );
}

export default EnterpriseTabSystem;
