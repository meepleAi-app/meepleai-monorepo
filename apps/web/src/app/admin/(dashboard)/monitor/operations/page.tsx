/**
 * Admin Operations Console
 * Issue #126 — Domain Operations Console page
 *
 * 4 tabs: Resources | Queue | Emergency | Audit
 * Each tab lazy-loaded with Suspense + skeleton.
 */

import { Suspense } from 'react';

import { Database, ListOrdered, ShieldAlert, ClipboardList } from 'lucide-react';

import { AdminHubTabBar, type HubTab } from '@/components/admin/layout/AdminHubTabBar';
import { AdminTabPersistence } from '@/components/admin/layout/AdminTabPersistence';

import { AuditTab } from './AuditTab';
import { EmergencyTab } from './EmergencyTab';
import { OperationsNavConfig } from './NavConfig';
import { QueueTab } from './QueueTab';
import { ResourcesTab } from './ResourcesTab';

interface OperationsPageProps {
  searchParams: Promise<{ tab?: string }>;
}

const TABS: readonly HubTab[] = [
  {
    id: 'resources',
    label: 'Resources',
    href: '/admin/monitor/operations?tab=resources',
    icon: <Database />,
  },
  {
    id: 'queue',
    label: 'Queue',
    href: '/admin/monitor/operations?tab=queue',
    icon: <ListOrdered />,
  },
  {
    id: 'emergency',
    label: 'Emergency',
    href: '/admin/monitor/operations?tab=emergency',
    icon: <ShieldAlert />,
  },
  {
    id: 'audit',
    label: 'Audit',
    href: '/admin/monitor/operations?tab=audit',
    icon: <ClipboardList />,
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

function renderTabContent(tab: TabId) {
  switch (tab) {
    case 'resources':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <ResourcesTab />
        </Suspense>
      );
    case 'queue':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <QueueTab />
        </Suspense>
      );
    case 'emergency':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <EmergencyTab />
        </Suspense>
      );
    case 'audit':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <AuditTab />
        </Suspense>
      );
    default:
      return null;
  }
}

export default async function OperationsPage({ searchParams }: OperationsPageProps) {
  const params = await searchParams;
  const tab = (params.tab ?? 'resources') as TabId;

  return (
    <div className="space-y-5" data-testid="operations-page">
      <OperationsNavConfig />
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Operations Console
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Resources, processing queue, emergency controls, and audit trail.
        </p>
      </div>

      <AdminHubTabBar tabs={TABS} activeTab={tab} />
      <AdminTabPersistence hubName="operations" defaultTab="resources" />

      <div className="pt-1">{renderTabContent(tab)}</div>
    </div>
  );
}
