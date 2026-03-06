/**
 * Admin Monitor Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5053 — Admin Monitor Migration
 *
 * Canonical entry for all system monitoring admin pages.
 * Tabs: alerts · cache · infra · services · command · testing · export
 */

import { Suspense } from 'react';

import Link from 'next/link';

import { AlertsTab } from './AlertsTab';
import { CacheTab } from './CacheTab';
import { CommandCenterTab } from './CommandCenterTab';
import { InfrastructureTab } from './InfrastructureTab';
import { AdminMonitorNavConfig } from './NavConfig';
import { TestingTab } from './TestingTab';

interface AdminMonitorPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'alerts', label: 'Alerts', href: '/admin/monitor?tab=alerts' },
  { id: 'cache', label: 'Cache', href: '/admin/monitor?tab=cache' },
  { id: 'infra', label: 'Infrastructure', href: '/admin/monitor?tab=infra' },
  { id: 'services', label: 'Services', href: '/admin/monitor?tab=services' },
  { id: 'command', label: 'Command Center', href: '/admin/monitor?tab=command' },
  { id: 'testing', label: 'Testing', href: '/admin/monitor?tab=testing' },
  { id: 'export', label: 'Bulk Export', href: '/admin/monitor?tab=export' },
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
        <ComingSoonTab
          label="Bulk Export"
          description="Export users, audit logs, and API keys in bulk."
        />
      );
    default:
      return null;
  }
}

export default async function AdminMonitorPage({ searchParams }: AdminMonitorPageProps) {
  const params = await searchParams;
  const tab = (params.tab ?? 'alerts') as TabId;

  return (
    <div className="space-y-6">
      <AdminMonitorNavConfig />
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Monitor
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          System health, alerts, cache management, infrastructure, and operations.
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
