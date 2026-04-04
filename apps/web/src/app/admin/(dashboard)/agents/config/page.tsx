'use client';

/**
 * Configuration Page — Strategy + Models + Limits
 * Consolidates /agents/strategy, /agents/models, /agents/chat-limits into a single
 * tabbed page. Supports deep-linking via ?tab=models and ?tab=limits.
 * Issue #5490 admin-consolidation epic.
 */

import { useSearchParams } from 'next/navigation';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import { AgentLimitsTabContent } from './AgentLimitsTabContent';
import { AgentModelsTabContent } from './AgentModelsTabContent';
import { AgentStrategyTabContent } from './AgentStrategyTabContent';

// ─── Valid tab values ─────────────────────────────────────────────────────────

type TabValue = 'strategy' | 'models' | 'limits';

const VALID_TABS: TabValue[] = ['strategy', 'models', 'limits'];

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
        <p className="text-sm text-muted-foreground mt-1">
          Strategy, modelli e limiti degli agenti
        </p>
      </div>

      <Tabs defaultValue={defaultTab}>
        <TabsList>
          <TabsTrigger value="strategy">Strategy</TabsTrigger>
          <TabsTrigger value="models">Models</TabsTrigger>
          <TabsTrigger value="limits">Limits</TabsTrigger>
        </TabsList>

        <TabsContent value="strategy" className="mt-6">
          <AgentStrategyTabContent />
        </TabsContent>

        <TabsContent value="models" className="mt-6">
          <AgentModelsTabContent />
        </TabsContent>

        <TabsContent value="limits" className="mt-6">
          <AgentLimitsTabContent />
        </TabsContent>
      </Tabs>
    </div>
  );
}
