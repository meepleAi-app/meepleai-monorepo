'use client';

import { useState } from 'react';

import { ArrowRightLeft } from 'lucide-react';

import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

interface ExecutionSummary {
  id: string;
  query: string;
  answer: string;
  strategy: string;
  latencyMs: number;
  tokens: number;
  cost: number;
  confidence: number;
  timestamp: string;
}

interface CompareRowProps {
  label: string;
  valueA: string | number;
  valueB: string | number;
  delta?: string;
  highlight?: boolean;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function computeDelta(a: number, b: number): string {
  if (a === 0 && b === 0) return '0%';
  if (a === 0) return '+∞';
  const pct = ((b - a) / a) * 100;
  const sign = pct >= 0 ? '+' : '';
  return `${sign}${pct.toFixed(1)}%`;
}

function CompareRow({ label, valueA, valueB, delta, highlight }: CompareRowProps) {
  return (
    <div
      className={cn(
        'grid grid-cols-[140px_1fr_1fr_100px] items-center gap-2 rounded-md px-3 py-2 text-sm',
        highlight ? 'bg-muted/50' : ''
      )}
    >
      <span className="font-medium text-muted-foreground">{label}</span>
      <span className="font-mono">{valueA}</span>
      <span className="font-mono">{valueB}</span>
      {delta && (
        <span
          className={cn(
            'text-xs font-medium text-right',
            delta.startsWith('+') ? 'text-red-600' : 'text-green-600',
            delta === '0%' || delta === '0.0%' ? 'text-muted-foreground' : ''
          )}
        >
          {delta}
        </span>
      )}
    </div>
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export function CompareTab() {
  const [executionA, setExecutionA] = useState<ExecutionSummary | null>(null);
  const [executionB, setExecutionB] = useState<ExecutionSummary | null>(null);
  const [idA, setIdA] = useState('');
  const [idB, setIdB] = useState('');

  // TODO: Wire to real API — GET /api/v1/admin/rag-executions/{id}
  // Placeholder: in a real implementation, these would fetch from the API
  const handleLoadA = () => {
    if (!idA.trim()) return;
    setExecutionA({
      id: idA,
      query: 'Sample query A',
      answer: 'Sample answer A',
      strategy: 'HybridRAG',
      latencyMs: 1200,
      tokens: 450,
      cost: 0.0045,
      confidence: 0.87,
      timestamp: new Date().toISOString(),
    });
  };

  const handleLoadB = () => {
    if (!idB.trim()) return;
    setExecutionB({
      id: idB,
      query: 'Sample query B',
      answer: 'Sample answer B',
      strategy: 'SingleModel',
      latencyMs: 800,
      tokens: 320,
      cost: 0.0032,
      confidence: 0.92,
      timestamp: new Date().toISOString(),
    });
  };

  return (
    <div className="space-y-6">
      {/* Execution Selectors */}
      <div className="grid grid-cols-1 md:grid-cols-[1fr_auto_1fr] items-end gap-4">
        <div className="space-y-2">
          <label className="text-sm font-medium text-foreground" htmlFor="exec-a">
            Esecuzione A
          </label>
          <div className="flex gap-2">
            <input
              id="exec-a"
              type="text"
              value={idA}
              onChange={e => setIdA(e.target.value)}
              placeholder="Execution ID o seleziona dalla storia"
              className="flex-1 rounded-md border bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            />
            <button
              onClick={handleLoadA}
              className="rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
              type="button"
            >
              Carica
            </button>
          </div>
        </div>

        <div className="hidden md:flex items-center justify-center pb-1">
          <ArrowRightLeft className="h-5 w-5 text-muted-foreground" />
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium text-foreground" htmlFor="exec-b">
            Esecuzione B
          </label>
          <div className="flex gap-2">
            <input
              id="exec-b"
              type="text"
              value={idB}
              onChange={e => setIdB(e.target.value)}
              placeholder="Execution ID o seleziona dalla storia"
              className="flex-1 rounded-md border bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            />
            <button
              onClick={handleLoadB}
              className="rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
              type="button"
            >
              Carica
            </button>
          </div>
        </div>
      </div>

      {/* Comparison Table */}
      {executionA && executionB ? (
        <div className="rounded-lg border">
          <div className="grid grid-cols-[140px_1fr_1fr_100px] gap-2 border-b bg-muted/30 px-3 py-2 text-xs font-medium text-muted-foreground">
            <span>Metrica</span>
            <span>Esecuzione A</span>
            <span>Esecuzione B</span>
            <span className="text-right">Delta</span>
          </div>
          <div className="divide-y">
            <CompareRow
              label="Strategy"
              valueA={executionA.strategy}
              valueB={executionB.strategy}
            />
            <CompareRow
              label="Latenza"
              valueA={`${executionA.latencyMs}ms`}
              valueB={`${executionB.latencyMs}ms`}
              delta={computeDelta(executionA.latencyMs, executionB.latencyMs)}
              highlight
            />
            <CompareRow
              label="Token"
              valueA={executionA.tokens}
              valueB={executionB.tokens}
              delta={computeDelta(executionA.tokens, executionB.tokens)}
            />
            <CompareRow
              label="Costo"
              valueA={`$${executionA.cost.toFixed(4)}`}
              valueB={`$${executionB.cost.toFixed(4)}`}
              delta={computeDelta(executionA.cost, executionB.cost)}
              highlight
            />
            <CompareRow
              label="Confidence"
              valueA={`${(executionA.confidence * 100).toFixed(1)}%`}
              valueB={`${(executionB.confidence * 100).toFixed(1)}%`}
              delta={computeDelta(executionA.confidence, executionB.confidence)}
            />
          </div>

          {/* Response Comparison */}
          <div className="border-t p-4">
            <h3 className="mb-3 text-sm font-medium text-foreground">Confronto Risposte</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-1">
                <span className="text-xs font-medium text-muted-foreground">Esecuzione A</span>
                <div className="rounded-md border bg-muted/20 p-3 text-sm whitespace-pre-wrap">
                  {executionA.answer}
                </div>
              </div>
              <div className="space-y-1">
                <span className="text-xs font-medium text-muted-foreground">Esecuzione B</span>
                <div className="rounded-md border bg-muted/20 p-3 text-sm whitespace-pre-wrap">
                  {executionB.answer}
                </div>
              </div>
            </div>
          </div>
        </div>
      ) : (
        <div className="flex h-48 items-center justify-center rounded-lg border border-dashed">
          <p className="text-sm text-muted-foreground">
            Inserisci due ID esecuzione per confrontare i risultati
          </p>
        </div>
      )}
    </div>
  );
}
