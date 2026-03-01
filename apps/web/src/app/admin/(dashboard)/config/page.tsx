/**
 * Admin Config Hub
 * Issue #5040 — Consolidate Admin Routes
 *
 * Canonical entry for all system configuration admin pages.
 * Tabs: general · limits · flags · rate-limits · n8n · wizard
 *
 * TODO (Issue #5052): Migrate full content with tab-based layout + ActionBar.
 */

import Link from 'next/link';

import { AdminConfigNavConfig } from './NavConfig';

interface AdminConfigPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'general',      label: 'General',     href: '/admin/config?tab=general' },
  { id: 'limits',       label: 'Limits',      href: '/admin/config?tab=limits' },
  { id: 'flags',        label: 'Feature Flags', href: '/admin/config?tab=flags' },
  { id: 'rate-limits',  label: 'Rate Limits', href: '/admin/config?tab=rate-limits' },
  { id: 'n8n',          label: 'n8n',         href: '/admin/config?tab=n8n' },
  { id: 'wizard',       label: 'Wizard',      href: '/admin/config?tab=wizard' },
] as const;

/** Old sub-page links available while full migration is pending */
const SUB_PAGES = [
  { label: 'Configuration',        href: '/admin/configuration' },
  { label: 'Library Limits',       href: '/admin/configuration/game-library-limits' },
  { label: 'PDF Upload Limits',    href: '/admin/configuration/pdf-upload-limits' },
  { label: 'PDF Tier Limits',      href: '/admin/configuration/pdf-tier-limits' },
  { label: 'Feature Flags',        href: '/admin/feature-flags' },
  { label: 'Tier Limits',          href: '/admin/tier-limits' },
  { label: 'n8n Templates',        href: '/admin/n8n-templates' },
];

export default async function AdminConfigPage({ searchParams }: AdminConfigPageProps) {
  const params = await searchParams;
  const tab = params.tab ?? 'general';

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
        {TABS.map((t) => (
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

      {/* Placeholder — full content migrated in Issue #5052 */}
      <div className="rounded-lg border border-dashed border-border/60 p-8 text-center">
        <p className="text-sm font-medium text-foreground">
          Tab: <span className="font-mono">{tab}</span>
        </p>
        <p className="mt-2 text-xs text-muted-foreground">
          Full tab content will be available after Issue #5052 (Admin Config Migration).
        </p>
        <p className="mt-4 text-xs text-muted-foreground">
          Access via individual pages below until migration is complete:
        </p>
        <div className="mt-3 flex flex-wrap justify-center gap-2">
          {SUB_PAGES.map((p) => (
            <Link
              key={p.href}
              href={p.href}
              className="rounded-md border border-border/60 px-3 py-1 text-xs text-muted-foreground hover:border-primary/50 hover:text-foreground transition-colors"
            >
              {p.label}
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
