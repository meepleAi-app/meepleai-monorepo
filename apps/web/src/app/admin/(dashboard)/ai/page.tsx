/**
 * Admin AI Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5048 — Admin AI Hub Migration
 *
 * Canonical entry for all AI/agent-related admin pages.
 * Tabs: agents · typologies · definitions · lab · prompts · models · requests · rag
 */

import { Suspense } from 'react';

import Link from 'next/link';

import { AgentsTab } from './AgentsTab';
import { AiLabTab } from './AiLabTab';
import { DefinitionsTab } from './DefinitionsTab';
import { ModelsTab } from './ModelsTab';
import { AdminAiNavConfig } from './NavConfig';
import { PromptsTab } from './PromptsTab';
import { RagTab } from './RagTab';
import { RequestsTab } from './RequestsTab';
import { TypologiesTab } from './TypologiesTab';

interface AdminAiPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS = [
  { id: 'agents', label: 'Agents', href: '/admin/ai?tab=agents' },
  { id: 'typologies', label: 'Typologies', href: '/admin/ai?tab=typologies' },
  { id: 'definitions', label: 'Definitions', href: '/admin/ai?tab=definitions' },
  { id: 'lab', label: 'AI Lab', href: '/admin/ai?tab=lab' },
  { id: 'prompts', label: 'Prompts', href: '/admin/ai?tab=prompts' },
  { id: 'models', label: 'Models', href: '/admin/ai?tab=models' },
  { id: 'requests', label: 'Requests', href: '/admin/ai?tab=requests' },
  { id: 'rag', label: 'RAG', href: '/admin/ai?tab=rag' },
] as const;

type TabId = (typeof TABS)[number]['id'];

function TabSkeleton() {
  return (
    <div className="h-[600px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
  );
}

function renderTabContent(tab: TabId) {
  switch (tab) {
    case 'agents':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <AgentsTab />
        </Suspense>
      );
    case 'typologies':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <TypologiesTab />
        </Suspense>
      );
    case 'definitions':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <DefinitionsTab />
        </Suspense>
      );
    case 'lab':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <AiLabTab />
        </Suspense>
      );
    case 'prompts':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <PromptsTab />
        </Suspense>
      );
    case 'models':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <ModelsTab />
        </Suspense>
      );
    case 'requests':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <RequestsTab />
        </Suspense>
      );
    case 'rag':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <RagTab />
        </Suspense>
      );
    default:
      return null;
  }
}

export default async function AdminAiPage({ searchParams }: AdminAiPageProps) {
  const params = await searchParams;
  const tab = (params.tab ?? 'agents') as TabId;

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
