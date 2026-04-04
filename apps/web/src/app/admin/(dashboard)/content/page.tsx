/**
 * Admin Content Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5049 — Admin Page Migrations
 *
 * Canonical entry for all content management admin pages.
 * Tabs: games · shared · kb
 */

import { Suspense } from 'react';

import { Share2, Database } from 'lucide-react';

import { AdminHubTabBar, type HubTab } from '@/components/admin/layout/AdminHubTabBar';
import { AdminTabPersistence } from '@/components/admin/layout/AdminTabPersistence';

import { KnowledgeBaseTab } from './KnowledgeBaseTab';
import { SharedGamesTab } from './SharedGamesTab';

interface AdminContentPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS: readonly HubTab[] = [
  { id: 'shared', label: 'Shared Games', href: '/admin/content?tab=shared', icon: <Share2 /> },
  { id: 'kb', label: 'Knowledge Base', href: '/admin/content?tab=kb', icon: <Database /> },
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

function renderTabContent(tab: TabId) {
  switch (tab) {
    case 'shared':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <SharedGamesTab showCategories />
        </Suspense>
      );
    case 'kb':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <KnowledgeBaseTab />
        </Suspense>
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
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Gestione Contenuti
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Gestisci giochi, contenuti condivisi e knowledge base.
        </p>
      </div>

      <AdminHubTabBar tabs={TABS} activeTab={tab} />
      <AdminTabPersistence hubName="content" defaultTab="shared" />

      <div className="pt-1">{renderTabContent(tab)}</div>
    </div>
  );
}
