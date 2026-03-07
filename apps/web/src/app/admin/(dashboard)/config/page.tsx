/**
 * Admin Config Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5052 — Admin Config Migration
 *
 * Canonical entry for all system configuration admin pages.
 * Tabs: general · limits · flags · rate-limits · n8n · wizard
 */

import { Suspense } from 'react';

import { Settings, Gauge, Flag, ShieldCheck, Workflow, Wand2 } from 'lucide-react';

import { AdminHubTabBar, type HubTab } from '@/components/admin/layout/AdminHubTabBar';

import { FeatureFlagsWrapper } from './FeatureFlagsWrapper';
import { GeneralTab } from './GeneralTab';
import { LimitsTab } from './LimitsTab';
import { AdminConfigNavConfig } from './NavConfig';
import { RateLimitsTab } from './RateLimitsTab';

interface AdminConfigPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS: readonly HubTab[] = [
  { id: 'general', label: 'General', href: '/admin/config?tab=general', icon: <Settings /> },
  { id: 'limits', label: 'Limits', href: '/admin/config?tab=limits', icon: <Gauge /> },
  { id: 'flags', label: 'Feature Flags', href: '/admin/config?tab=flags', icon: <Flag /> },
  {
    id: 'rate-limits',
    label: 'Rate Limits',
    href: '/admin/config?tab=rate-limits',
    icon: <ShieldCheck />,
  },
  { id: 'n8n', label: 'n8n', href: '/admin/config?tab=n8n', icon: <Workflow /> },
  { id: 'wizard', label: 'Wizard', href: '/admin/config?tab=wizard', icon: <Wand2 /> },
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
    case 'general':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <GeneralTab />
        </Suspense>
      );
    case 'limits':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <LimitsTab />
        </Suspense>
      );
    case 'flags':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <FeatureFlagsWrapper />
        </Suspense>
      );
    case 'rate-limits':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <RateLimitsTab />
        </Suspense>
      );
    case 'n8n':
      return (
        <ComingSoonTab
          label="n8n Integration"
          description="Manage n8n workflow templates, webhooks, and automation triggers."
        />
      );
    case 'wizard':
      return (
        <ComingSoonTab
          label="Configuration Wizard"
          description="Step-by-step guided setup for initial platform configuration."
        />
      );
    default:
      return null;
  }
}

export default async function AdminConfigPage({ searchParams }: AdminConfigPageProps) {
  const params = await searchParams;
  const tab = (params.tab ?? 'general') as TabId;

  return (
    <div className="space-y-5">
      <AdminConfigNavConfig />
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Configuration
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          System settings, feature flags, rate limits, and integrations.
        </p>
      </div>

      <AdminHubTabBar tabs={TABS} activeTab={tab} />

      <div className="pt-1">{renderTabContent(tab)}</div>
    </div>
  );
}
