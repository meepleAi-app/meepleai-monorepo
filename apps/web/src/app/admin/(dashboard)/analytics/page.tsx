/**
 * Admin Analytics Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5051 — Admin Analytics Migration
 *
 * Canonical entry for all analytics admin pages.
 * Tabs: overview · ai-usage · audit · reports · api-keys
 */

import { Suspense } from 'react';

import Link from 'next/link';

import { AiUsageTab } from './AiUsageTab';
import { ApiKeysTab } from './ApiKeysTab';
import { AuditLogTab } from './AuditLogTab';
import { AdminAnalyticsNavConfig } from './NavConfig';
import { OverviewTab } from './OverviewTab';
import { ReportsTab } from './ReportsTab';

interface AdminAnalyticsPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'overview', label: 'Overview', href: '/admin/analytics?tab=overview' },
  { id: 'ai-usage', label: 'AI Usage', href: '/admin/analytics?tab=ai-usage' },
  { id: 'audit', label: 'Audit Log', href: '/admin/analytics?tab=audit' },
  { id: 'reports', label: 'Reports', href: '/admin/analytics?tab=reports' },
  { id: 'api-keys', label: 'API Keys', href: '/admin/analytics?tab=api-keys' },
] as const;

type TabId = (typeof TABS)[number]['id'];

function TabSkeleton() {
  return (
    <div className="h-[600px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
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
    <div className="space-y-6">
      <AdminAnalyticsNavConfig />
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Analytics
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Usage statistics, AI analytics, audit logs, and reports.
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
