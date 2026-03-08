/**
 * Admin Monitor Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5053 — Admin Monitor Migration
 *
 * Canonical entry for all system monitoring admin pages.
 * Tabs: alerts · cache · infra · services · command · testing · export
 */

import { Suspense } from 'react';

import { Bell, Database, HardDrive, Layers, Terminal, TestTube, Download } from 'lucide-react';

import { AdminHubTabBar, type HubTab } from '@/components/admin/layout/AdminHubTabBar';
import { AdminTabPersistence } from '@/components/admin/layout/AdminTabPersistence';

import { AlertsTab } from './AlertsTab';
import { BulkExportTab } from './BulkExportTab';
import { CacheTab } from './CacheTab';
import { CommandCenterTab } from './CommandCenterTab';
import { InfrastructureTab } from './InfrastructureTab';
import { AdminMonitorNavConfig } from './NavConfig';
import { TestingTab } from './TestingTab';

interface AdminMonitorPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS: readonly HubTab[] = [
  { id: 'alerts', label: 'Alerts', href: '/admin/monitor?tab=alerts', icon: <Bell /> },
  { id: 'cache', label: 'Cache', href: '/admin/monitor?tab=cache', icon: <Database /> },
  { id: 'infra', label: 'Infrastructure', href: '/admin/monitor?tab=infra', icon: <HardDrive /> },
  { id: 'services', label: 'Services', href: '/admin/monitor?tab=services', icon: <Layers /> },
  {
    id: 'command',
    label: 'Command Center',
    href: '/admin/monitor?tab=command',
    icon: <Terminal />,
  },
  { id: 'testing', label: 'Testing', href: '/admin/monitor?tab=testing', icon: <TestTube /> },
  { id: 'export', label: 'Bulk Export', href: '/admin/monitor?tab=export', icon: <Download /> },
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
    case 'alerts':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <AlertsTab />
        </Suspense>
      );
    case 'cache':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <CacheTab />
        </Suspense>
      );
    case 'infra':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <InfrastructureTab />
        </Suspense>
      );
    case 'services':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <InfrastructureTab />
        </Suspense>
      );
    case 'command':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <CommandCenterTab />
        </Suspense>
      );
    case 'testing':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <TestingTab />
        </Suspense>
      );
    case 'export':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <BulkExportTab />
        </Suspense>
      );
    default:
      return null;
  }
}

export default async function AdminMonitorPage({ searchParams }: AdminMonitorPageProps) {
  const params = await searchParams;
  const tab = (params.tab ?? 'alerts') as TabId;

  return (
    <div className="space-y-5">
      <AdminMonitorNavConfig />
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Monitor
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          System health, alerts, cache management, infrastructure, and operations.
        </p>
      </div>

      <AdminHubTabBar tabs={TABS} activeTab={tab} />
      <AdminTabPersistence hubName="monitor" defaultTab="alerts" />

      <div className="pt-1">{renderTabContent(tab)}</div>
    </div>
  );
}
