/**
 * Admin AI Hub
 * Issue #5040 — Consolidate Admin Routes
 * Issue #5048 — Admin AI Hub Migration
 *
 * Canonical entry for all AI/agent-related admin pages.
 * Tabs: agents · typologies · definitions · lab · prompts · models · requests · rag
 */

import { Suspense } from 'react';

import {
  Bot,
  BrainCircuit,
  Cpu,
  FlaskConical,
  Activity,
  ListOrdered,
  ScrollText,
  Settings,
  Tag,
} from 'lucide-react';

import { AdminHubTabBar, type HubTab } from '@/components/admin/layout/AdminHubTabBar';
import { AdminTabPersistence } from '@/components/admin/layout/AdminTabPersistence';

import { AgentsTab } from './AgentsTab';
import { AiLabTab } from './AiLabTab';
import { DefinitionsTab } from './DefinitionsTab';
import { LlmConfigTab } from './LlmConfigTab';
import { ModelsTab } from './ModelsTab';
import { AdminAiNavConfig } from './NavConfig';
import { PromptsTab } from './PromptsTab';
import { RagTab } from './RagTab';
import { RequestsTab } from './RequestsTab';
import { TypologiesTab } from './TypologiesTab';

interface AdminAiPageProps {
  searchParams: Promise<{ tab?: string; section?: string }>;
}

const TABS: readonly HubTab[] = [
  { id: 'agents', label: 'Agents', href: '/admin/ai?tab=agents', icon: <Bot /> },
  { id: 'typologies', label: 'Typologies', href: '/admin/ai?tab=typologies', icon: <Tag /> },
  {
    id: 'definitions',
    label: 'Definitions',
    href: '/admin/ai?tab=definitions',
    icon: <ListOrdered />,
  },
  { id: 'lab', label: 'AI Lab', href: '/admin/ai?tab=lab', icon: <FlaskConical /> },
  { id: 'prompts', label: 'Prompts', href: '/admin/ai?tab=prompts', icon: <ScrollText /> },
  { id: 'models', label: 'Models', href: '/admin/ai?tab=models', icon: <Cpu /> },
  { id: 'requests', label: 'Requests', href: '/admin/ai?tab=requests', icon: <Activity /> },
  { id: 'rag', label: 'RAG', href: '/admin/ai?tab=rag', icon: <BrainCircuit /> },
  { id: 'config', label: 'Config', href: '/admin/ai?tab=config', icon: <Settings /> },
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
    case 'config':
      return (
        <Suspense fallback={<TabSkeleton />}>
          <LlmConfigTab />
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
    <div className="space-y-5">
      <AdminAiNavConfig />

      {/* Header */}
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          AI & Agents
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Manage AI agents, definitions, models, prompts, and RAG pipeline.
        </p>
      </div>

      {/* Tab bar — scrollable on mobile, wraps on desktop */}
      <AdminHubTabBar tabs={TABS} activeTab={tab} />
      <AdminTabPersistence hubName="ai" defaultTab="agents" />

      {/* Tab content */}
      <div className="pt-1">{renderTabContent(tab)}</div>
    </div>
  );
}
