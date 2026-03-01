/**
 * Admin Monitor Hub
 * Issue #5040 — Consolidate Admin Routes
 *
 * Canonical entry for all system monitoring admin pages.
 * Tabs: alerts · cache · infra · services · command · testing · export
 *
 * TODO (Issue #5053): Migrate full content with tab-based layout + ActionBar.
 */

import Link from 'next/link';

import { AdminMonitorNavConfig } from './NavConfig';

interface AdminMonitorPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'alerts',   label: 'Alerts',          href: '/admin/monitor?tab=alerts' },
  { id: 'cache',    label: 'Cache',           href: '/admin/monitor?tab=cache' },
  { id: 'infra',    label: 'Infrastructure',  href: '/admin/monitor?tab=infra' },
  { id: 'services', label: 'Services',        href: '/admin/monitor?tab=services' },
  { id: 'command',  label: 'Command Center',  href: '/admin/monitor?tab=command' },
  { id: 'testing',  label: 'Testing',         href: '/admin/monitor?tab=testing' },
  { id: 'export',   label: 'Bulk Export',     href: '/admin/monitor?tab=export' },
] as const;

/** Old sub-page links available while full migration is pending */
const SUB_PAGES = [
  { label: 'Alerts',          href: '/admin/alerts' },
  { label: 'Alert Config',    href: '/admin/alerts/config' },
  { label: 'Alert Rules',     href: '/admin/alert-rules' },
  { label: 'Cache',           href: '/admin/cache' },
  { label: 'Infrastructure',  href: '/admin/infrastructure' },
  { label: 'Services',        href: '/admin/services' },
  { label: 'Command Center',  href: '/admin/command-center' },
  { label: 'Testing',         href: '/admin/testing' },
  { label: 'Bulk Export',     href: '/admin/bulk-export' },
];

export default async function AdminMonitorPage({ searchParams }: AdminMonitorPageProps) {
  const params = await searchParams;
  const tab = params.tab ?? 'alerts';

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

      {/* Placeholder — full content migrated in Issue #5053 */}
      <div className="rounded-lg border border-dashed border-border/60 p-8 text-center">
        <p className="text-sm font-medium text-foreground">
          Tab: <span className="font-mono">{tab}</span>
        </p>
        <p className="mt-2 text-xs text-muted-foreground">
          Full tab content will be available after Issue #5053 (Admin Monitor Migration).
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
