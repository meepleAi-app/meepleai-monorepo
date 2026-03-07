/**
 * Admin Content Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5049 — Admin Page Migrations
 *
 * Canonical entry for all content management admin pages.
 * Tabs: games · shared · faqs · kb · sessions
 */

import { Suspense } from 'react';

import { Gamepad2, Share2, HelpCircle, Database, CalendarDays } from 'lucide-react';

import { AdminHubTabBar, type HubTab } from '@/components/admin/layout/AdminHubTabBar';

import { KnowledgeBaseTab } from './KnowledgeBaseTab';
import { AdminContentNavConfig } from './NavConfig';
import { SharedGamesTab } from './SharedGamesTab';

interface AdminContentPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS: readonly HubTab[] = [
  { id: 'games', label: 'Games', href: '/admin/content?tab=games', icon: <Gamepad2 /> },
  { id: 'shared', label: 'Shared Games', href: '/admin/content?tab=shared', icon: <Share2 /> },
  { id: 'faqs', label: 'FAQs', href: '/admin/content?tab=faqs', icon: <HelpCircle /> },
  { id: 'kb', label: 'Knowledge Base', href: '/admin/content?tab=kb', icon: <Database /> },
  {
    id: 'sessions',
    label: 'Sessions',
    href: '/admin/content?tab=sessions',
    icon: <CalendarDays />,
  },
] as const;

type TabId = (typeof TABS)[number]['id'];

function TabSkeleton() {
  return (
    <div className="space-y-3 pt-2">
      <div className="h-10 w-48 rounded-lg bg-white/40 dark:bg-zinc-800/40 animate-pulse" />
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {[1, 2, 3].map(i => (
          <div key={i} className="h-24 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse" />
        ))}
      </div>
    </div>
  );
}

function ComingSoonTab({ label, description }: { label: string; description: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-6 text-center rounded-2xl border border-dashed border-border/50 bg-white/30 dark:bg-zinc-800/20 backdrop-blur-sm">
      <p className="font-quicksand text-sm font-semibold text-foreground">{label}</p>
      <p className="mt-1.5 max-w-sm text-xs text-muted-foreground">{description}</p>
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
    <div className="space-y-5">
      <AdminContentNavConfig />
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Content Management
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Manage games, shared content, FAQs, knowledge base, and sessions.
        </p>
      </div>

      <AdminHubTabBar tabs={TABS} activeTab={tab} />

      <div className="pt-1">{renderTabContent(tab)}</div>
    </div>
  );
}
