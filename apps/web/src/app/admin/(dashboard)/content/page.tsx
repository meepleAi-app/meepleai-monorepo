/**
 * Admin Content Hub
 * Issue #5040 — Consolidate Admin Routes
 *
 * Canonical entry for all content management admin pages.
 * Tabs: games · shared · faqs · kb · sessions
 *
 * TODO (Issue #5049): Migrate full content with tab-based layout + ActionBar.
 */

import Link from 'next/link';

import { AdminContentNavConfig } from './NavConfig';

interface AdminContentPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'games',    label: 'Games',          href: '/admin/content?tab=games' },
  { id: 'shared',   label: 'Shared Games',   href: '/admin/content?tab=shared' },
  { id: 'faqs',     label: 'FAQs',           href: '/admin/content?tab=faqs' },
  { id: 'kb',       label: 'Knowledge Base', href: '/admin/content?tab=kb' },
  { id: 'sessions', label: 'Sessions',       href: '/admin/content?tab=sessions' },
] as const;

const SUB_PAGES = [
  { label: 'Games',                   href: '/admin/games' },
  { label: 'Shared Games',            href: '/admin/shared-games' },
  { label: 'Shared Games Approvals',  href: '/admin/shared-games/approval-queue' },
  { label: 'FAQs',                    href: '/admin/faqs' },
  { label: 'PDFs / KB',               href: '/admin/pdfs' },
  { label: 'Game Sessions',           href: '/admin/game-sessions' },
  { label: 'Share Requests',          href: '/admin/share-requests' },
];

export default async function AdminContentPage({ searchParams }: AdminContentPageProps) {
  const params = await searchParams;
  const tab = params.tab ?? 'games';

  return (
    <div className="space-y-6">
      <AdminContentNavConfig />
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Content Management
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Manage games, shared content, FAQs, knowledge base, and sessions.
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

      {/* Placeholder — full content migrated in Issue #5049 */}
      <div className="rounded-lg border border-dashed border-border/60 p-8 text-center">
        <p className="text-sm font-medium text-foreground">
          Tab: <span className="font-mono">{tab}</span>
        </p>
        <p className="mt-2 text-xs text-muted-foreground">
          Full tab content will be available after Issue #5049 (Admin Page Migrations).
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
