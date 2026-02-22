/**
 * Admin AI Hub
 * Issue #5040 — Consolidate Admin Routes
 *
 * Canonical entry for all AI/agent-related admin pages.
 * Tabs: agents · typologies · definitions · lab · prompts · models · requests · rag
 *
 * TODO (Issue #5048): Migrate full content with tab-based layout + ActionBar.
 */

import Link from 'next/link';

import { AdminAiNavConfig } from './NavConfig';

interface AdminAiPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'agents',      label: 'Agents',      href: '/admin/ai?tab=agents' },
  { id: 'typologies',  label: 'Typologies',  href: '/admin/ai?tab=typologies' },
  { id: 'definitions', label: 'Definitions', href: '/admin/ai?tab=definitions' },
  { id: 'lab',         label: 'AI Lab',      href: '/admin/ai?tab=lab' },
  { id: 'prompts',     label: 'Prompts',     href: '/admin/ai?tab=prompts' },
  { id: 'models',      label: 'Models',      href: '/admin/ai?tab=models' },
  { id: 'requests',    label: 'Requests',    href: '/admin/ai?tab=requests' },
  { id: 'rag',         label: 'RAG',         href: '/admin/ai?tab=rag' },
] as const;

/** Old sub-page links available while full migration is pending */
const SUB_PAGES = [
  { label: 'Agent Catalog',       href: '/admin/agents' },
  { label: 'Agent Definitions',   href: '/admin/agent-definitions' },
  { label: 'Agent Typologies',    href: '/admin/agent-typologies' },
  { label: 'AI Lab',              href: '/admin/ai-lab' },
  { label: 'Prompts',             href: '/admin/prompts' },
  { label: 'AI Models',           href: '/admin/ai-models' },
  { label: 'AI Requests',         href: '/admin/ai-requests' },
  { label: 'RAG',                 href: '/admin/rag' },
  { label: 'RAG Executions',      href: '/admin/rag-executions' },
  { label: 'Strategies',          href: '/admin/strategies' },
];

export default async function AdminAiPage({ searchParams }: AdminAiPageProps) {
  const params = await searchParams;
  const tab = params.tab ?? 'agents';

  return (
    <div className="space-y-6">
      <AdminAiNavConfig />
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          AI & Agents
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Manage AI agents, definitions, models, prompts, and RAG pipeline.
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

      {/* Placeholder — full content migrated in Issue #5048 */}
      <div className="rounded-lg border border-dashed border-border/60 p-8 text-center">
        <p className="text-sm font-medium text-foreground">
          Tab: <span className="font-mono">{tab}</span>
        </p>
        <p className="mt-2 text-xs text-muted-foreground">
          Full tab content will be available after Issue #5048 (Admin Page Migrations).
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
