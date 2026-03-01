/**
 * Admin Content Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5049 — Admin Page Migrations
 *
 * Canonical entry for all content management admin pages.
 * Tabs: games · shared · faqs · kb · sessions
 */

import { Suspense } from 'react';

import Link from 'next/link';

import { KnowledgeBaseTab } from './KnowledgeBaseTab';
import { AdminContentNavConfig } from './NavConfig';
import { SharedGamesTab } from './SharedGamesTab';

interface AdminContentPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'games', label: 'Games', href: '/admin/content?tab=games' },
  { id: 'shared', label: 'Shared Games', href: '/admin/content?tab=shared' },
  { id: 'faqs', label: 'FAQs', href: '/admin/content?tab=faqs' },
  { id: 'kb', label: 'Knowledge Base', href: '/admin/content?tab=kb' },
  { id: 'sessions', label: 'Sessions', href: '/admin/content?tab=sessions' },
] as const;

type TabId = (typeof TABS)[number]['id'];

function TabSkeleton() {
  return (
    <div className="h-[600px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
  );
}

function ComingSoonTab({ label, description }: { label: string; description: string }) {
  return (
    <div className="rounded-lg border border-dashed border-border/60 p-16 text-center">
      <p className="text-base font-semibold text-foreground">{label}</p>
      <p className="mt-2 text-sm text-muted-foreground">{description}</p>
    </div>
  );
}

function renderTabContent(tab: TabId) {
  switch (tab) {
    case 'games':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <SharedGamesTab showCategories />
        </Suspense>
      );
    case 'shared':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <SharedGamesTab />
        </Suspense>
      );
    case 'kb':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <KnowledgeBaseTab />
        </Suspense>
      );
    case 'faqs':
      return (
        <ComingSoonTab
          label="FAQs"
          description="Manage frequently asked questions for games and the platform."
        />
      );
    case 'sessions':
      return (
        <ComingSoonTab
          label="Game Sessions"
          description="Browse and manage recorded game sessions."
        />
      );
    default:
      return null;
  }
}

export default async function AdminContentPage({ searchParams }: AdminContentPageProps) {
  const params = await searchParams;
  const tab = (params.tab ?? 'shared') as TabId;

  return (
    <div className="space-y-6">
      <AdminContentNavConfig />
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Content Management
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Manage games, shared content, FAQs, knowledge base, and sessions.
        </p>
      </div>

      {/* Tab bar */}
      <div className="flex flex-wrap gap-2 border-b border-border/50 pb-3">
        {TABS.map(t => (
          <Link
            key={t.id}
            href={t.href}
            className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
              tab === t.id
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-muted hover:text-foreground'
            }`}
          >
            {t.label}
          </Link>
        ))}
      </div>

      {/* Tab content */}
      {renderTabContent(tab)}
    </div>
  );
}
