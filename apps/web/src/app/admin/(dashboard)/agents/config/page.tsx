'use client';

/**
 * Configuration Page — Strategy + Models
 * Consolidates /agents/strategy and /agents/models into a single tabbed page.
 * Supports deep-linking via ?tab=models. Issue #5490 admin-consolidation epic.
 * Issue #4212: the Limits tab (chat history per tier) is gone — it saved keys nobody read.
 */

import { useSearchParams } from 'next/navigation';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import { AgentModelsTabContent } from './AgentModelsTabContent';
import { AgentStrategyTabContent } from './AgentStrategyTabContent';

// ─── Valid tab values ─────────────────────────────────────────────────────────

type TabValue = 'strategy' | 'models';

const VALID_TABS: TabValue[] = ['strategy', 'models'];

function isValidTab(value: string | null): value is TabValue {
  return VALID_TABS.includes(value as TabValue);
}

// ─── Page ────────────────────────────────────────────────────────────────────

export default function ConfigurationPage() {
  const searchParams = useSearchParams();
  const tabParam = searchParams.get('tab');
  const defaultTab: TabValue = isValidTab(tabParam) ? tabParam : 'strategy';

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Configurazione AI
        </h1>
        <p className="text-sm text-muted-foreground mt-1">Strategy e modelli degli agenti</p>
      </div>

      <Tabs defaultValue={defaultTab}>
        <TabsList>
          <TabsTrigger value="strategy">Strategy</TabsTrigger>
          <TabsTrigger value="models">Models</TabsTrigger>
        </TabsList>

        <TabsContent value="strategy" className="mt-6">
          <AgentStrategyTabContent />
        </TabsContent>

        <TabsContent value="models" className="mt-6">
          <AgentModelsTabContent />
        </TabsContent>
      </Tabs>
    </div>
  );
}
