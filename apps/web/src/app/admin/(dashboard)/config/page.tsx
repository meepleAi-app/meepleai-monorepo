/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or admin-decorative inline gradient; DS-13a admin scope, mockup .e-bg pattern. Future: extract --admin-* token family (deferred to DS-15 audit). */
/**
 * Admin Config Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5052 — Admin Config Migration
 *
 * Canonical entry for all system configuration admin pages.
 * Tabs: general · flags · rate-limits · banner
 * #4449: the Limits tab is gone — its PDF upload limits had no reader on any upload path.
 */

import { Suspense } from 'react';

import { Settings, Flag, ShieldCheck, Megaphone } from 'lucide-react';

import { AdminHubTabBar, type HubTab } from '@/components/admin/layout/AdminHubTabBar';
import { AdminTabPersistence } from '@/components/admin/layout/AdminTabPersistence';
import { StatusBannerAdmin } from '@/components/features/status-banner';

import { FeatureFlagsWrapper } from './FeatureFlagsWrapper';
import { GeneralTab } from './GeneralTab';
import { RateLimitsTab } from './RateLimitsTab';

interface AdminConfigPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS: readonly HubTab[] = [
  { id: 'general', label: 'General', href: '/admin/config?tab=general', icon: <Settings /> },
  { id: 'flags', label: 'Feature Flags', href: '/admin/config?tab=flags', icon: <Flag /> },
  {
    id: 'rate-limits',
    label: 'Rate Limits',
    href: '/admin/config?tab=rate-limits',
    icon: <ShieldCheck />,
  },
  { id: 'banner', label: 'Banner', href: '/admin/config?tab=banner', icon: <Megaphone /> },
] as const;

type TabId = (typeof TABS)[number]['id'];

function TabSkeleton() {
  return (
    <div className="space-y-3 pt-2">
      <div className="h-10 w-48 rounded-lg bg-card/40 dark:bg-zinc-800/40 animate-pulse" />
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {[1, 2, 3].map(i => (
          <div key={i} className="h-24 rounded-xl bg-card/40 dark:bg-zinc-800/40 animate-pulse" />
        ))}
      </div>
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
    case 'banner':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <StatusBannerAdmin />
        </Suspense>
      );
    default:
      return null;
  }
}

export default async function AdminConfigPage({ searchParams }: AdminConfigPageProps) {
  const params = await searchParams;
  // An unknown tab falls back to General. #4449 removed the Limits tab: a link to it, or the tab
  // remembered by AdminTabPersistence, would otherwise open an empty page.
  const requested = params.tab ?? 'general';
  const tab: TabId = TABS.some(t => t.id === requested) ? (requested as TabId) : 'general';

  return (
    <div className="space-y-5">
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Configurazione
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Impostazioni di sistema, feature flag e limiti di frequenza.
        </p>
      </div>

      <AdminHubTabBar tabs={TABS} activeTab={tab} />
      <AdminTabPersistence hubName="config" defaultTab="general" />

      <div className="pt-1">{renderTabContent(tab)}</div>
    </div>
  );
}
