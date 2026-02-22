/**
 * Admin Analytics Hub
 * Issue #5040 — Consolidate Admin Routes
 *
 * Canonical entry for all analytics admin pages.
 * Tabs: overview · ai-usage · audit · reports · api-keys
 *
 * TODO (Issue #5051): Migrate full content with tab-based layout + ActionBar.
 */

import Link from 'next/link';

import { AdminAnalyticsNavConfig } from './NavConfig';

interface AdminAnalyticsPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'overview',  label: 'Overview',   href: '/admin/analytics?tab=overview' },
  { id: 'ai-usage',  label: 'AI Usage',   href: '/admin/analytics?tab=ai-usage' },
  { id: 'audit',     label: 'Audit Log',  href: '/admin/analytics?tab=audit' },
  { id: 'reports',   label: 'Reports',    href: '/admin/analytics?tab=reports' },
  { id: 'api-keys',  label: 'API Keys',   href: '/admin/analytics?tab=api-keys' },
] as const;

/** Old sub-page links available while full migration is pending */
const SUB_PAGES = [
  { label: 'Analytics Overview', href: '/admin/analytics' },
  { label: 'AI Usage',           href: '/admin/ai-usage' },
  { label: 'Audit Log',          href: '/admin/audit-log' },
  { label: 'Reports',            href: '/admin/reports' },
  { label: 'API Keys',           href: '/admin/api-keys' },
];

export default async function AdminAnalyticsPage({ searchParams }: AdminAnalyticsPageProps) {
  const params = await searchParams;
  const tab = params.tab ?? 'overview';

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

      {/* Placeholder — full content migrated in Issue #5051 */}
      <div className="rounded-lg border border-dashed border-border/60 p-8 text-center">
        <p className="text-sm font-medium text-foreground">
          Tab: <span className="font-mono">{tab}</span>
        </p>
        <p className="mt-2 text-xs text-muted-foreground">
          Full tab content will be available after Issue #5051 (Admin Analytics Migration).
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
