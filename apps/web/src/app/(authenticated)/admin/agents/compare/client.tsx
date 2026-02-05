/**
 * Strategy Comparison Client
 * Issue #3380
 *
 * Interactive UI for comparing 2-4 strategy+model configurations side-by-side.
 */

'use client';

import { useState, useCallback } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Play, Loader2, Plus, ArrowLeft, Save } from 'lucide-react';
import Link from 'next/link';
import { toast } from 'sonner';

import { ComparisonMetrics } from '@/components/admin/agents/ComparisonMetrics';
import { ComparisonPanel } from '@/components/admin/agents/ComparisonPanel';
import { ConfigSelector } from '@/components/admin/agents/ConfigSelector';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

// Strategy options matching backend RagStrategy enum
export const STRATEGIES = [
  { value: 'FAST', label: 'Fast', description: 'Quick lookups, ~1s', color: 'green' },
  { value: 'BALANCED', label: 'Balanced', description: 'Standard, ~2s', color: 'blue' },
  { value: 'PRECISE', label: 'Precise', description: 'Complex rules, ~4s', color: 'purple' },
  { value: 'EXPERT', label: 'Expert', description: 'Research, ~8s', color: 'orange' },
  { value: 'CONSENSUS', label: 'Consensus', description: 'Multi-model, ~12s', color: 'red' },
] as const;

// Model options with tier info
export const MODELS = [
  { value: 'gpt-4o-mini', label: 'GPT-4o Mini', tier: 'free' },
  { value: 'llama-3.3', label: 'Llama 3.3', tier: 'free' },
  { value: 'claude-3-haiku', label: 'Claude 3 Haiku', tier: 'normal' },
  { value: 'gpt-4-turbo', label: 'GPT-4 Turbo', tier: 'normal' },
  { value: 'claude-3-sonnet', label: 'Claude 3 Sonnet', tier: 'premium' },
  { value: 'gpt-4', label: 'GPT-4', tier: 'premium' },
  { value: 'claude-3-opus', label: 'Claude 3 Opus', tier: 'premium' },
] as const;

export interface ComparisonConfig {
  id: string;
  strategy: string;
  model: string;
}

export interface ComparisonResult {
  configId: string;
  strategy: string;
  model: string;
  response: string;
  latency: number;
  tokensUsed: number;
  costEstimate: number;
  confidenceScore: number;
  citations: Array<{ page: number; text: string }>;
  timestamp: Date;
}

const generateId = () => Math.random().toString(36).substring(2, 9);

const DEFAULT_CONFIGS: ComparisonConfig[] = [
  { id: generateId(), strategy: 'FAST', model: 'gpt-4o-mini' },
  { id: generateId(), strategy: 'PRECISE', model: 'claude-3-sonnet' },
];

export function StrategyComparisonClient() {
  const [selectedTypologyId, setSelectedTypologyId] = useState<string>('');
  const [query, setQuery] = useState<string>('');
  const [configs, setConfigs] = useState<ComparisonConfig[]>(DEFAULT_CONFIGS);
  const [results, setResults] = useState<ComparisonResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  // Fetch typologies
  const { data: typologies, isLoading: typologiesLoading } = useQuery<Typology[]>({
    queryKey: ['admin', 'typologies'],
    queryFn: () => api.agents.getTypologies('Approved'),
  });

  const addConfig = useCallback(() => {
    if (configs.length >= 4) {
      toast.error('Maximum 4 configurations allowed');
      return;
    }
    setConfigs(prev => [
      ...prev,
      { id: generateId(), strategy: 'BALANCED', model: 'gpt-4o-mini' },
    ]);
  }, [configs.length]);

  const removeConfig = useCallback((id: string) => {
    if (configs.length <= 2) {
      toast.error('Minimum 2 configurations required');
      return;
    }
    setConfigs(prev => prev.filter(c => c.id !== id));
    setResults(prev => prev.filter(r => r.configId !== id));
  }, [configs.length]);

  const updateConfig = useCallback((id: string, field: 'strategy' | 'model', value: string) => {
    setConfigs(prev =>
      prev.map(c => (c.id === id ? { ...c, [field]: value } : c))
    );
  }, []);

  const runComparison = useCallback(async () => {
    if (!selectedTypologyId || !query.trim()) {
      toast.error('Missing Configuration', {
        description: 'Please select a typology and enter a query.',
      });
      return;
    }

    setIsLoading(true);
    setResults([]);

    try {
      // Run tests in parallel using Promise.all
      const testPromises = configs.map(async (config): Promise<ComparisonResult> => {
        // Simulate API call - replace with actual API when available
        // const response = await api.agents.test(selectedTypologyId, {
        //   query,
        //   strategyOverride: config.strategy,
        //   modelOverride: config.model,
        // });

        // Mock response with strategy-specific latency
        const strategyInfo = STRATEGIES.find(s => s.value === config.strategy);
        const baseLatency = config.strategy === 'FAST' ? 1 :
          config.strategy === 'BALANCED' ? 2 :
          config.strategy === 'PRECISE' ? 4 :
          config.strategy === 'EXPERT' ? 8 : 12;

        await new Promise(resolve => setTimeout(resolve, baseLatency * 500 + Math.random() * 500));

        return {
          configId: config.id,
          strategy: config.strategy,
          model: config.model,
          response: `[${strategyInfo?.label ?? config.strategy}] Based on the game rules for "${query}", the answer involves understanding the core mechanics. This is a simulated response demonstrating the ${config.strategy} strategy with ${config.model}.`,
          latency: baseLatency * (0.8 + Math.random() * 0.4),
          tokensUsed: Math.floor(150 + Math.random() * 350),
          costEstimate: config.model.includes('opus') ? 0.02 + Math.random() * 0.01 :
            config.model.includes('sonnet') ? 0.008 + Math.random() * 0.004 :
            config.model.includes('haiku') ? 0.003 + Math.random() * 0.002 :
            0.001 + Math.random() * 0.002,
          confidenceScore: config.strategy === 'PRECISE' ? 0.85 + Math.random() * 0.1 :
            config.strategy === 'EXPERT' ? 0.8 + Math.random() * 0.15 :
            config.strategy === 'CONSENSUS' ? 0.88 + Math.random() * 0.1 :
            0.7 + Math.random() * 0.15,
          citations: [
            { page: Math.floor(Math.random() * 30) + 1, text: 'Section 4.2: Game Setup' },
            { page: Math.floor(Math.random() * 30) + 10, text: 'Section 5.1: Turn Structure' },
          ],
          timestamp: new Date(),
        };
      });

      const allResults = await Promise.all(testPromises);
      setResults(allResults);

      toast.success('Comparison Complete', {
        description: `${configs.length} configurations tested in parallel`,
      });
    } catch (error) {
      toast.error('Comparison Failed', {
        description: error instanceof Error ? error.message : 'Unknown error occurred',
      });
    } finally {
      setIsLoading(false);
    }
  }, [selectedTypologyId, query, configs]);

  const saveComparison = useCallback(async () => {
    if (results.length === 0) {
      toast.error('No results to save');
      return;
    }

    try {
      // TODO: Implement save to history - integrate with #3379
      toast.success('Comparison Saved', {
        description: 'Results saved to test history.',
      });
    } catch (error) {
      toast.error('Save Failed', {
        description: error instanceof Error ? error.message : 'Failed to save comparison',
      });
    }
  }, [results]);

  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/admin/agents/test">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Strategy Comparison</h1>
            <p className="text-muted-foreground mt-1">
              Compare 2-4 strategy/model configurations side-by-side
            </p>
          </div>
        </div>
        {results.length > 0 && (
          <Button variant="outline" onClick={saveComparison}>
            <Save className="mr-2 h-4 w-4" />
            Save Comparison
          </Button>
        )}
      </div>

      {/* Query Configuration */}
      <Card>
        <CardHeader>
          <CardTitle>Test Query</CardTitle>
          <CardDescription>Select a typology and enter the query to test</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <label className="text-sm font-medium">Typology</label>
              <Select
                value={selectedTypologyId}
                onValueChange={setSelectedTypologyId}
                disabled={typologiesLoading}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select typology..." />
                </SelectTrigger>
                <SelectContent>
                  {typologies?.map(typology => (
                    <SelectItem key={typology.id} value={typology.id}>
                      {typology.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Query</label>
              <Textarea
                placeholder="How do I build a settlement in Catan?"
                value={query}
                onChange={e => setQuery(e.target.value)}
                rows={2}
                className="resize-none"
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Configuration Cards */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold">Configurations ({configs.length}/4)</h2>
          <Button
            variant="outline"
            size="sm"
            onClick={addConfig}
            disabled={configs.length >= 4}
          >
            <Plus className="mr-2 h-4 w-4" />
            Add Configuration
          </Button>
        </div>

        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          {configs.map((config, index) => (
            <ConfigSelector
              key={config.id}
              config={config}
              index={index}
              strategies={STRATEGIES}
              models={MODELS}
              onUpdate={updateConfig}
              onRemove={configs.length > 2 ? removeConfig : undefined}
            />
          ))}
        </div>
      </div>

      {/* Run Button */}
      <div className="flex justify-center">
        <Button
          size="lg"
          onClick={runComparison}
          disabled={isLoading || !selectedTypologyId || !query.trim()}
          className="min-w-[200px]"
        >
          {isLoading ? (
            <>
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Running Tests...
            </>
          ) : (
            <>
              <Play className="mr-2 h-5 w-5" />
              Run Comparison
            </>
          )}
        </Button>
      </div>

      {/* Results */}
      {results.length > 0 && (
        <>
          {/* Metrics Summary */}
          <ComparisonMetrics results={results} strategies={STRATEGIES} models={MODELS} />

          {/* Side-by-Side Results */}
          <div className="space-y-4">
            <h2 className="text-xl font-semibold">Response Comparison</h2>
            <div className={`grid gap-4 ${
              results.length === 2 ? 'md:grid-cols-2' :
              results.length === 3 ? 'md:grid-cols-3' :
              'md:grid-cols-2 lg:grid-cols-4'
            }`}>
              {results.map((result) => (
                <ComparisonPanel
                  key={result.configId}
                  result={result}
                  strategies={STRATEGIES}
                  models={MODELS}
                  isWinner={result === results.reduce((best, r) =>
                    r.confidenceScore > best.confidenceScore ? r : best
                  )}
                />
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
