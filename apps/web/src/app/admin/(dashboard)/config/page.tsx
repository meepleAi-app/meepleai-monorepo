/**
 * Admin Config Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5052 — Admin Config Migration
 *
 * Canonical entry for all system configuration admin pages.
 * Tabs: general · limits · flags · rate-limits · n8n · wizard
 */

import { Suspense } from 'react';

import Link from 'next/link';

import { FeatureFlagsWrapper } from './FeatureFlagsWrapper';
import { GeneralTab } from './GeneralTab';
import { LimitsTab } from './LimitsTab';
import { AdminConfigNavConfig } from './NavConfig';
import { RateLimitsTab } from './RateLimitsTab';

interface AdminConfigPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'general', label: 'General', href: '/admin/config?tab=general' },
  { id: 'limits', label: 'Limits', href: '/admin/config?tab=limits' },
  { id: 'flags', label: 'Feature Flags', href: '/admin/config?tab=flags' },
  { id: 'rate-limits', label: 'Rate Limits', href: '/admin/config?tab=rate-limits' },
  { id: 'n8n', label: 'n8n', href: '/admin/config?tab=n8n' },
  { id: 'wizard', label: 'Wizard', href: '/admin/config?tab=wizard' },
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
    <div className="space-y-6">
      <AdminConfigNavConfig />
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Configuration
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          System settings, feature flags, rate limits, and integrations.
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
