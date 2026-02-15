'use client';

import { useCallback, useState } from 'react';

import { Download, Loader2, Play } from 'lucide-react';
import { toast } from 'sonner';

import { Button, Card, CardContent, CardHeader, CardTitle, Input, ScrollArea } from '@/components/ui';
import { parsePlaygroundSSEChunk } from '@/lib/agent/playground-sse-parser';
import type { CompletionMetadata, CostBreakdown, LatencyBreakdown, PlaygroundSSEHandlers } from '@/lib/agent/playground-sse-parser';
import { cn } from '@/lib/utils';
import { usePlaygroundStore } from '@/stores/playground-store';
import type { PlaygroundStrategy } from '@/stores/playground-store';

interface StrategyResult {
  strategy: PlaygroundStrategy;
  status: 'idle' | 'loading' | 'done' | 'error';
  response: string;
  tokens: number;
  cost: CostBreakdown | null;
  latency: LatencyBreakdown | null;
  latencyMs: number;
  confidence: number | null;
  error?: string;
}

const STRATEGIES: PlaygroundStrategy[] = ['RetrievalOnly', 'SingleModel', 'MultiModelConsensus'];

const STRATEGY_COLORS: Record<PlaygroundStrategy, string> = {
  RetrievalOnly: 'border-blue-200 bg-blue-50/50',
  SingleModel: 'border-amber-200 bg-amber-50/50',
  MultiModelConsensus: 'border-purple-200 bg-purple-50/50',
};

const STRATEGY_LABELS: Record<PlaygroundStrategy, string> = {
  RetrievalOnly: 'Retrieval Only',
  SingleModel: 'Single Model',
  MultiModelConsensus: 'Multi-Model',
};

function emptyResult(strategy: PlaygroundStrategy): StrategyResult {
  return { strategy, status: 'idle', response: '', tokens: 0, cost: null, latency: null, latencyMs: 0, confidence: null };
}

export function ComparisonView({ agentId }: { agentId: string }) {
  const [question, setQuestion] = useState('');
  const [results, setResults] = useState<StrategyResult[]>(STRATEGIES.map(emptyResult));
  const [isRunning, setIsRunning] = useState(false);
  const { systemMessage, currentGameId, modelOverride, providerOverride } = usePlaygroundStore();

  const runComparison = useCallback(async () => {
    if (!agentId) {
      toast.error('Please select an agent first');
      return;
    }
    if (!question.trim()) {
      toast.error('Please enter a question');
      return;
    }

    setIsRunning(true);
    setResults(STRATEGIES.map(emptyResult));

    const promises = STRATEGIES.map(async (strategy, idx) => {
      setResults((prev) => prev.map((r, i) => (i === idx ? { ...r, status: 'loading' } : r)));
      const startTime = Date.now();

      try {
        const response = await fetch(`/api/v1/admin/agent-definitions/${agentId}/playground/chat`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            message: question,
            strategy,
            ...(systemMessage ? { systemMessage } : {}),
            ...(currentGameId ? { gameId: currentGameId } : {}),
            ...(modelOverride ? { modelOverride } : {}),
            ...(providerOverride ? { providerOverride } : {}),
          }),
        });

        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const reader = response.body?.getReader();
        const decoder = new TextDecoder();
        let responseText = '';

        const handlers: PlaygroundSSEHandlers = {
          onToken: (token) => {
            responseText += token;
            setResults((prev) => prev.map((r, i) => (i === idx ? { ...r, response: responseText } : r)));
          },
          onComplete: (metadata: CompletionMetadata) => {
            setResults((prev) => prev.map((r, i) =>
              i === idx
                ? {
                    ...r,
                    status: 'done' as const,
                    tokens: metadata.totalTokens,
                    cost: metadata.costBreakdown ?? null,
                    latency: metadata.latencyBreakdown ?? null,
                    latencyMs: Date.now() - startTime,
                    confidence: metadata.confidence ?? null,
                  }
                : r
            ));
          },
          onError: (error) => {
            setResults((prev) => prev.map((r, i) =>
              i === idx ? { ...r, status: 'error' as const, error: error.errorMessage } : r
            ));
          },
          onStateUpdate: () => {},
          onCitations: () => {},
          onFollowUpQuestions: () => {},
          onHeartbeat: () => {},
        };

        while (reader) {
          const { done, value } = await reader.read();
          if (done) break;
          parsePlaygroundSSEChunk(decoder.decode(value, { stream: true }), handlers);
        }

        // If onComplete never fired, mark as done
        setResults((prev) => prev.map((r, i) =>
          i === idx && r.status === 'loading'
            ? { ...r, status: 'done' as const, latencyMs: Date.now() - startTime }
            : r
        ));
      } catch (error) {
        setResults((prev) => prev.map((r, i) =>
          i === idx
            ? { ...r, status: 'error' as const, error: error instanceof Error ? error.message : 'Unknown error' }
            : r
        ));
      }
    });

    await Promise.allSettled(promises);
    setIsRunning(false);
  }, [agentId, question, systemMessage, currentGameId, modelOverride, providerOverride]);

  const handleExport = () => {
    const data = {
      question,
      timestamp: new Date().toISOString(),
      results: results.map((r) => ({
        strategy: r.strategy,
        status: r.status,
        tokens: r.tokens,
        cost: r.cost?.totalCost ?? 0,
        latencyMs: r.latencyMs,
        confidence: r.confidence,
        responseLength: r.response.length,
        response: r.response.slice(0, 500),
      })),
    };
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `comparison-${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    toast.success('Comparison exported');
  };

  const allDone = results.every((r) => r.status === 'done' || r.status === 'error');
  const doneResults = results.filter((r) => r.status === 'done');

  // Find best/worst per metric
  const bestLatency = doneResults.length > 0 ? doneResults.reduce((a, b) => (a.latencyMs < b.latencyMs ? a : b)).strategy : null;
  const bestCost = doneResults.length > 0 ? doneResults.reduce((a, b) => ((a.cost?.totalCost ?? 0) < (b.cost?.totalCost ?? 0) ? a : b)).strategy : null;
  const bestTokens = doneResults.length > 0 ? doneResults.reduce((a, b) => (a.tokens < b.tokens ? a : b)).strategy : null;

  return (
    <div className="space-y-4">
      {/* Input */}
      <div className="flex gap-2">
        <Input
          placeholder="Enter a question to compare across all strategies..."
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && !isRunning && runComparison()}
          className="flex-1 text-sm"
          disabled={isRunning}
        />
        <Button onClick={runComparison} disabled={isRunning || !agentId || !question.trim()} size="sm">
          {isRunning ? <Loader2 className="h-4 w-4 animate-spin mr-1.5" /> : <Play className="h-4 w-4 mr-1.5" />}
          Compare
        </Button>
        {allDone && doneResults.length > 0 && (
          <Button variant="outline" size="sm" onClick={handleExport}>
            <Download className="h-4 w-4 mr-1.5" />
            Export
          </Button>
        )}
      </div>

      {/* Results Grid - 3 columns */}
      <div className="grid grid-cols-3 gap-3">
        {results.map((result) => (
          <Card key={result.strategy} className={cn('transition-colors', STRATEGY_COLORS[result.strategy])}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm flex items-center justify-between">
                <span>{STRATEGY_LABELS[result.strategy]}</span>
                {result.status === 'loading' && <Loader2 className="h-3.5 w-3.5 animate-spin text-muted-foreground" />}
                {result.status === 'done' && <span className="text-[10px] bg-green-100 text-green-800 px-1.5 py-0.5 rounded">Done</span>}
                {result.status === 'error' && <span className="text-[10px] bg-red-100 text-red-800 px-1.5 py-0.5 rounded">Error</span>}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {/* Response */}
              <ScrollArea className="h-40 rounded border bg-background/80 p-2">
                {result.status === 'idle' && (
                  <p className="text-xs text-muted-foreground italic">Waiting...</p>
                )}
                {result.status === 'error' && (
                  <p className="text-xs text-red-600">{result.error}</p>
                )}
                {(result.status === 'loading' || result.status === 'done') && (
                  <p className="text-xs whitespace-pre-wrap">{result.response || '...'}</p>
                )}
              </ScrollArea>

              {/* Metrics */}
              {(result.status === 'done' || result.status === 'loading') && (
                <div className="space-y-1 text-xs">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Tokens</span>
                    <span className={cn('font-mono', bestTokens === result.strategy && 'text-green-600 font-semibold')}>
                      {result.tokens > 0 ? result.tokens.toLocaleString() : '—'}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Cost</span>
                    <span className={cn('font-mono', bestCost === result.strategy && 'text-green-600 font-semibold')}>
                      {result.cost ? `$${result.cost.totalCost.toFixed(6)}` : '—'}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Latency</span>
                    <span className={cn('font-mono', bestLatency === result.strategy && 'text-green-600 font-semibold')}>
                      {result.latencyMs > 0 ? `${(result.latencyMs / 1000).toFixed(2)}s` : '—'}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Confidence</span>
                    <span className="font-mono">
                      {result.confidence !== null ? `${(result.confidence * 100).toFixed(0)}%` : '—'}
                    </span>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Summary Table */}
      {allDone && doneResults.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Comparison Summary</CardTitle>
          </CardHeader>
          <CardContent>
            <table className="w-full text-xs">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-1.5 font-medium">Metric</th>
                  {results.map((r) => (
                    <th key={r.strategy} className="text-right py-1.5 font-medium">{STRATEGY_LABELS[r.strategy]}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                <tr className="border-b">
                  <td className="py-1.5 text-muted-foreground">Tokens</td>
                  {results.map((r) => (
                    <td key={r.strategy} className={cn('text-right font-mono py-1.5', bestTokens === r.strategy && 'text-green-600 font-semibold')}>
                      {r.status === 'done' ? r.tokens.toLocaleString() : '—'}
                    </td>
                  ))}
                </tr>
                <tr className="border-b">
                  <td className="py-1.5 text-muted-foreground">Cost</td>
                  {results.map((r) => (
                    <td key={r.strategy} className={cn('text-right font-mono py-1.5', bestCost === r.strategy && 'text-green-600 font-semibold')}>
                      {r.cost ? `$${r.cost.totalCost.toFixed(6)}` : '—'}
                    </td>
                  ))}
                </tr>
                <tr className="border-b">
                  <td className="py-1.5 text-muted-foreground">Latency</td>
                  {results.map((r) => (
                    <td key={r.strategy} className={cn('text-right font-mono py-1.5', bestLatency === r.strategy && 'text-green-600 font-semibold')}>
                      {r.status === 'done' ? `${(r.latencyMs / 1000).toFixed(2)}s` : '—'}
                    </td>
                  ))}
                </tr>
                <tr>
                  <td className="py-1.5 text-muted-foreground">Confidence</td>
                  {results.map((r) => (
                    <td key={r.strategy} className="text-right font-mono py-1.5">
                      {r.confidence !== null ? `${(r.confidence * 100).toFixed(0)}%` : '—'}
                    </td>
                  ))}
                </tr>
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
