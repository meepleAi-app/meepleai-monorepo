/**
 * Agent Testing Console Client
 * Issue #3378 / #4962
 *
 * Interactive testing interface for admin testing of agent typologies.
 * Wired to real backend: POST /api/v1/agent-typologies/{id}/test
 *
 * Note: the backend TestQueryRequest only accepts `testQuery`. Strategy and model
 * fields are saved as metadata alongside the result but do NOT currently affect
 * the test execution itself. Future: extend backend TestQueryRequest with overrides.
 */

'use client';

import { useState, useCallback } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Send, Loader2, Info } from 'lucide-react';
import { toast } from 'sonner';

import { TestChatInterface } from '@/components/admin/agents/TestChatInterface';
import { TestMetricsDisplay } from '@/components/admin/agents/TestMetricsDisplay';
import { Badge } from '@/components/ui/data-display/badge';
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
const STRATEGIES = [
  { value: 'FAST', label: 'Fast', description: 'Quick lookups, ~1s latency' },
  { value: 'BALANCED', label: 'Balanced', description: 'Standard questions, ~2s latency' },
  { value: 'PRECISE', label: 'Precise', description: 'Complex rules, ~4s latency' },
  { value: 'EXPERT', label: 'Expert', description: 'Research mode, ~8s latency' },
  { value: 'CONSENSUS', label: 'Consensus', description: 'Multi-model voting, ~12s latency' },
];

// Model options
const MODELS = [
  { value: 'gpt-4o-mini', label: 'GPT-4o Mini', tier: 'free' },
  { value: 'llama-3.3', label: 'Llama 3.3', tier: 'free' },
  { value: 'claude-3-haiku', label: 'Claude 3 Haiku', tier: 'normal' },
  { value: 'gpt-4-turbo', label: 'GPT-4 Turbo', tier: 'normal' },
  { value: 'claude-3-sonnet', label: 'Claude 3 Sonnet', tier: 'premium' },
  { value: 'gpt-4', label: 'GPT-4', tier: 'premium' },
  { value: 'claude-3-opus', label: 'Claude 3 Opus', tier: 'premium' },
];

interface TestResult {
  query: string;
  response: string;
  latency: number;
  tokensUsed: number;
  costEstimate: number;
  confidenceScore: number;
  citations: Array<{ page: number; text: string }>;
  timestamp: Date;
  // Captured at test time to avoid stale closure on save (optional for sub-component compat)
  typologyId?: string;
  modelUsed?: string;
  strategyOverride?: string;
}

export function AgentTestClient() {
  const [selectedTypologyId, setSelectedTypologyId] = useState<string>('');
  const [selectedStrategy, setSelectedStrategy] = useState<string>('BALANCED');
  const [selectedModel, setSelectedModel] = useState<string>('gpt-4o-mini');
  const [query, setQuery] = useState<string>('');
  const [isLoading, setIsLoading] = useState(false);
  const [testResults, setTestResults] = useState<TestResult[]>([]);

  // Fetch typologies
  const { data: typologies, isLoading: typologiesLoading } = useQuery<Typology[]>({
    queryKey: ['admin', 'typologies'],
    queryFn: () => api.agents.getTypologies('Approved'),
  });

  const handleSendQuery = useCallback(async () => {
    if (!selectedTypologyId || !query.trim()) {
      toast.error('Missing Configuration', {
        description: 'Please select a typology and enter a query.',
      });
      return;
    }

    setIsLoading(true);
    const startTime = Date.now();

    try {
      const result = await api.agents.testTypology(selectedTypologyId, query);

      const latencySeconds = (Date.now() - startTime) / 1000;

      // Capture typologyId, model, and strategy at test time to avoid stale closure on save
      const testResult: TestResult = {
        query,
        response: result.response,
        latency: latencySeconds,
        tokensUsed: 0,
        costEstimate: 0,
        confidenceScore: result.confidenceScore,
        citations: [],
        timestamp: new Date(),
        typologyId: selectedTypologyId,
        modelUsed: selectedModel,
        strategyOverride: selectedStrategy,
      };

      setTestResults(prev => [testResult, ...prev]);
      setQuery('');

      toast.success('Test Completed', {
        description: `Response received in ${latencySeconds.toFixed(2)}s`,
      });
    } catch (error) {
      toast.error('Test Failed', {
        description: error instanceof Error ? error.message : 'Unknown error occurred',
      });
    } finally {
      setIsLoading(false);
    }
  }, [selectedTypologyId, query, selectedModel, selectedStrategy]);

  const handleSaveResult = useCallback(async (result: TestResult) => {
    if (!result.typologyId) {
      toast.error('Save Failed', { description: 'Missing test metadata. Please re-run the test.' });
      return;
    }
    try {
      // Use values captured at test time, not from current UI state (avoids stale closure)
      await api.agents.saveTestResult({
        typologyId: result.typologyId,
        query: result.query,
        response: result.response,
        modelUsed: result.modelUsed ?? '',
        confidenceScore: result.confidenceScore,
        tokensUsed: result.tokensUsed,
        costEstimate: result.costEstimate,
        latencyMs: Math.round(result.latency * 1000),
        strategyOverride: result.strategyOverride,
      });
      toast.success('Result Saved', {
        description: 'Test result saved to history.',
      });
    } catch (error) {
      toast.error('Save Failed', {
        description: error instanceof Error ? error.message : 'Failed to save result',
      });
    }
  }, []);

  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Agent Testing Console</h1>
        <p className="text-muted-foreground mt-2">
          Test agent typologies with different strategies and models
        </p>
      </div>

      {/* Configuration Panel */}
      <Card>
        <CardHeader>
          <CardTitle>Test Configuration</CardTitle>
          <CardDescription>Select typology, strategy, and model for testing</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-3">
            {/* Typology Selector */}
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

            {/* Strategy — logged as metadata, does not yet affect test execution */}
            <div className="space-y-2">
              <label className="text-sm font-medium flex items-center gap-1">
                Strategy
                <span className="text-xs text-muted-foreground font-normal flex items-center gap-0.5">
                  <Info className="h-3 w-3" />
                  logged only
                </span>
              </label>
              <Select value={selectedStrategy} onValueChange={setSelectedStrategy}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {STRATEGIES.map(strategy => (
                    <SelectItem key={strategy.value} value={strategy.value}>
                      <div className="flex flex-col">
                        <span>{strategy.label}</span>
                        <span className="text-xs text-muted-foreground">
                          {strategy.description}
                        </span>
                      </div>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Model — logged as metadata, does not yet affect test execution */}
            <div className="space-y-2">
              <label className="text-sm font-medium flex items-center gap-1">
                Model
                <span className="text-xs text-muted-foreground font-normal flex items-center gap-0.5">
                  <Info className="h-3 w-3" />
                  logged only
                </span>
              </label>
              <Select value={selectedModel} onValueChange={setSelectedModel}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MODELS.map(model => (
                    <SelectItem key={model.value} value={model.value}>
                      <div className="flex items-center gap-2">
                        <span>{model.label}</span>
                        <Badge variant="outline" className="text-xs">
                          {model.tier}
                        </Badge>
                      </div>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Chat Interface */}
      <div className="grid gap-6 lg:grid-cols-2">
        {/* Query Input */}
        <Card>
          <CardHeader>
            <CardTitle>Test Query</CardTitle>
            <CardDescription>Enter your test question below</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <Textarea
              placeholder="How do I build a settlement in Catan?"
              value={query}
              onChange={e => setQuery(e.target.value)}
              rows={4}
              className="resize-none"
            />
            <div className="flex gap-2">
              <Button
                onClick={handleSendQuery}
                disabled={isLoading || !selectedTypologyId || !query.trim()}
                className="flex-1"
              >
                {isLoading ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Testing...
                  </>
                ) : (
                  <>
                    <Send className="mr-2 h-4 w-4" />
                    Send Test Query
                  </>
                )}
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Latest Result Metrics */}
        {testResults.length > 0 && (
          <TestMetricsDisplay
            result={testResults[0]}
            onSave={() => handleSaveResult(testResults[0])}
          />
        )}
      </div>

      {/* Chat History */}
      <TestChatInterface
        results={testResults}
        onSave={handleSaveResult}
      />
    </div>
  );
}
