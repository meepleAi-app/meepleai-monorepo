/**
 * EnterpriseAdminLayout Component
 * Issue #3689 - Layout Base & Navigation System
 *
 * Main layout wrapper for the enterprise admin dashboard.
 * Combines vertical sidebar + content area with sticky header and tabs.
 * Features:
 * - Responsive layout (desktop sidebar, mobile drawer)
 * - Section header with title and description
 * - Horizontal tab system per section
 * - Glassmorphic design tokens
 * - Mobile bottom navigation bar
 */

'use client';

import React, { type ReactNode, Suspense } from 'react';

import { useSearchParams } from 'next/navigation';

import { EnterpriseAdminSidebar } from './EnterpriseAdminSidebar';
import { EnterpriseTabSystem } from './EnterpriseTabSystem';
import { MobileBottomNav } from './MobileBottomNav';
import type { EnterpriseSection } from '@/config/enterprise-navigation';
import { cn } from '@/lib/utils';

export interface EnterpriseAdminLayoutProps {
  /** Current section configuration */
  section: EnterpriseSection;
  /** Content to render in the active tab panel */
  children: ReactNode;
}

function EnterpriseAdminLayoutInner({ section, children }: EnterpriseAdminLayoutProps) {
  const searchParams = useSearchParams();
  const activeTab = searchParams?.get('tab') ?? section.tabs[0]?.id ?? 'dashboard';

  return (
    <div className="flex h-[calc(100vh-4rem)] -mt-4">
      {/* Vertical Sidebar */}
      <EnterpriseAdminSidebar />

      {/* Main content area */}
      <main className="flex-1 flex flex-col overflow-hidden">
        {/* Sticky section header */}
        <header
          className={cn(
            'bg-white/50 dark:bg-zinc-900/50 backdrop-blur-sm',
            'border-b border-zinc-200/50 dark:border-zinc-700/50',
            'px-6 lg:px-8 py-4',
            'sticky top-0 z-20 shrink-0'
          )}
        >
          <h1 className="text-2xl lg:text-3xl font-quicksand font-bold text-zinc-900 dark:text-zinc-100 mb-0.5">
            {section.label}
          </h1>
          <p className="text-sm text-zinc-500 dark:text-zinc-400">{section.description}</p>
        </header>

        {/* Tab system + content */}
        <div className="flex-1 overflow-y-auto">
          <EnterpriseTabSystem tabs={section.tabs} activeTab={activeTab}>
            <div className="p-6 lg:p-8">
              {children}
            </div>
          </EnterpriseTabSystem>
        </div>
      </main>

      {/* Mobile bottom navigation */}
      <MobileBottomNav />
    </div>
  );
}

export function EnterpriseAdminLayout(props: EnterpriseAdminLayoutProps) {
  return (
    <Suspense fallback={<EnterpriseLayoutSkeleton />}>
      <EnterpriseAdminLayoutInner {...props} />
    </Suspense>
  );
}

function EnterpriseLayoutSkeleton() {
  return (
    <div className="flex h-[calc(100vh-4rem)] -mt-4">
      <aside className="hidden lg:flex lg:flex-col lg:w-56 border-r border-zinc-200/50 dark:border-zinc-700/50 bg-white/80 dark:bg-zinc-900/95">
        <div className="flex-1 p-3">
          <div className="animate-pulse space-y-2">
            {Array.from({ length: 7 }).map((_, i) => (
              <div key={i} className="h-10 bg-muted/30 rounded-lg" />
            ))}
          </div>
        </div>
      </aside>
      <main className="flex-1 flex flex-col">
        <div className="px-8 py-5 border-b border-zinc-200/50 dark:border-zinc-700/50">
          <div className="animate-pulse">
            <div className="h-8 w-48 bg-muted/30 rounded mb-2" />
            <div className="h-4 w-72 bg-muted/20 rounded" />
          </div>
        </div>
        <div className="p-8">
          <div className="animate-pulse grid grid-cols-4 gap-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-32 bg-muted/20 rounded-2xl" />
            ))}
          </div>
        </div>
      </main>
    </div>
  );
}

export default EnterpriseAdminLayout;
