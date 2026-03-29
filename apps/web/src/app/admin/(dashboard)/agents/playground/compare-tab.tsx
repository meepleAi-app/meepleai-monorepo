'use client';

import { useState, useCallback } from 'react';

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

  const [loadingA, setLoadingA] = useState(false);
  const [loadingB, setLoadingB] = useState(false);
  const [errorA, setErrorA] = useState<string | null>(null);
  const [errorB, setErrorB] = useState<string | null>(null);

  const loadExecution = useCallback(
    async (
      id: string,
      setExecution: (e: ExecutionSummary | null) => void,
      setLoading: (l: boolean) => void,
      setError: (e: string | null) => void
    ) => {
      if (!id.trim()) return;
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`/api/v1/admin/rag-executions/${encodeURIComponent(id.trim())}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const exec = await res.json();
        setExecution({
          id: exec.id,
          query: exec.query ?? '',
          answer: exec.executionTrace ? '[Vedi trace]' : '[N/A]',
          strategy: exec.strategy ?? 'Unknown',
          latencyMs: exec.totalLatencyMs ?? 0,
          tokens: exec.totalTokens ?? 0,
          cost: exec.totalCost ?? 0,
          confidence: exec.confidence ?? 0,
          timestamp: exec.createdAt ?? new Date().toISOString(),
        });
      } catch {
        setError('Esecuzione non trovata');
        setExecution(null);
      } finally {
        setLoading(false);
      }
    },
    []
  );

  const handleLoadA = () => loadExecution(idA, setExecutionA, setLoadingA, setErrorA);
  const handleLoadB = () => loadExecution(idB, setExecutionB, setLoadingB, setErrorB);

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
