/**
 * Admin Analytics Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5051 — Admin Analytics Migration
 *
 * Canonical entry for all analytics admin pages.
 * Tabs: overview · ai-usage · audit · reports · api-keys
 */

import { Suspense } from 'react';

import { BarChart3, BrainCircuit, ScrollText, FileBarChart, Key } from 'lucide-react';

import { AdminHubTabBar, type HubTab } from '@/components/admin/layout/AdminHubTabBar';
import { AdminTabPersistence } from '@/components/admin/layout/AdminTabPersistence';

import { AiUsageTab } from './AiUsageTab';
import { ApiKeysTab } from './ApiKeysTab';
import { AuditLogTab } from './AuditLogTab';
import { OverviewTab } from './OverviewTab';
import { ReportsTab } from './ReportsTab';

interface AdminAnalyticsPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS: readonly HubTab[] = [
  { id: 'overview', label: 'Overview', href: '/admin/analytics?tab=overview', icon: <BarChart3 /> },
  {
    id: 'ai-usage',
    label: 'AI Usage',
    href: '/admin/analytics?tab=ai-usage',
    icon: <BrainCircuit />,
  },
  { id: 'audit', label: 'Audit Log', href: '/admin/analytics?tab=audit', icon: <ScrollText /> },
  { id: 'reports', label: 'Reports', href: '/admin/analytics?tab=reports', icon: <FileBarChart /> },
  { id: 'api-keys', label: 'API Keys', href: '/admin/analytics?tab=api-keys', icon: <Key /> },
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
    case 'overview':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <OverviewTab />
        </Suspense>
      );
    case 'ai-usage':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <AiUsageTab />
        </Suspense>
      );
    case 'audit':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <AuditLogTab />
        </Suspense>
      );
    case 'reports':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <ReportsTab />
        </Suspense>
      );
    case 'api-keys':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <ApiKeysTab />
        </Suspense>
      );
    default:
      return null;
  }
}

export default async function AdminAnalyticsPage({ searchParams }: AdminAnalyticsPageProps) {
  const params = await searchParams;
  const tab = (params.tab ?? 'overview') as TabId;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Analitiche
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Statistiche di utilizzo, analitiche AI, log di audit e report.
        </p>
      </div>

      <AdminHubTabBar tabs={TABS} activeTab={tab} />
      <AdminTabPersistence hubName="analytics" defaultTab="overview" />

      <div className="pt-1">{renderTabContent(tab)}</div>
    </div>
  );
}
