/**
 * EnterpriseSectionPage Component
 * Issue #3689 - Layout Base & Navigation System
 *
 * Shared wrapper for enterprise section pages.
 * Renders the sticky section header + tab system.
 */

'use client';

import React, { type ReactNode, Suspense } from 'react';

import { useSearchParams } from 'next/navigation';

import { EnterpriseTabSystem } from './EnterpriseTabSystem';
import type { EnterpriseSection } from '@/config/enterprise-navigation';
import { cn } from '@/lib/utils';

interface EnterpriseSectionPageProps {
  section: EnterpriseSection;
  /** Map of tab ID → content to render */
  tabContent: Record<string, ReactNode>;
  /** Fallback content when tab has no specific content */
  fallback?: ReactNode;
}

function SectionPageInner({ section, tabContent, fallback }: EnterpriseSectionPageProps) {
  const searchParams = useSearchParams();
  const activeTab = searchParams?.get('tab') ?? section.tabs[0]?.id ?? '';

  const content = tabContent[activeTab] ?? fallback ?? (
    <div className="text-center py-12 text-zinc-500 dark:text-zinc-400">
      <p className="text-lg font-medium">
        {section.tabs.find((t) => t.id === activeTab)?.label ?? 'Tab'} - Coming Soon
      </p>
      <p className="text-sm mt-1">This section is under development.</p>
    </div>
  );

  return (
    <>
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
            {content}
          </div>
        </EnterpriseTabSystem>
      </div>
    </>
  );
}

export function EnterpriseSectionPage(props: EnterpriseSectionPageProps) {
  return (
    <Suspense
      fallback={
        <div className="p-8">
          <div className="animate-pulse">
            <div className="h-8 w-48 bg-muted/30 rounded mb-2" />
            <div className="h-4 w-72 bg-muted/20 rounded mb-8" />
            <div className="h-10 w-full bg-muted/15 rounded mb-8" />
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="h-32 bg-muted/20 rounded-2xl" />
              ))}
            </div>
          </div>
        </div>
      }
    >
      <SectionPageInner {...props} />
    </Suspense>
  );
}

export default EnterpriseSectionPage;
