/**
 * DebugSummaryBar - Summary metrics bar for RAG debug tab
 * Issue #4916: Tab Debug sulla chat — totali tokens, latency, confidence
 */

'use client';

import { Clock, Cpu, Percent, Search } from 'lucide-react';

import type { DebugStep } from '@/hooks/useAgentChatStream';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function sumLatency(steps: DebugStep[]): number {
  return steps.reduce((acc, s) => acc + (s.latencyMs ?? 0), 0);
}

function extractCostUpdate(steps: DebugStep[]): {
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  confidence: number | null;
  model: string | null;
} | null {
  const costStep = steps.findLast(s => s.type === 17); // DebugCostUpdate
  if (!costStep) return null;
  const p = costStep.payload as Record<string, unknown>;
  return {
    promptTokens: typeof p.promptTokens === 'number' ? p.promptTokens : 0,
    completionTokens: typeof p.completionTokens === 'number' ? p.completionTokens : 0,
    totalTokens: typeof p.totalTokens === 'number' ? p.totalTokens : 0,
    confidence: typeof p.confidence === 'number' ? p.confidence : null,
    model: typeof p.model === 'string' ? p.model : null,
  };
}

function extractRetrievalResults(steps: DebugStep[]): {
  filteredCount: number;
} | null {
  const s = steps.findLast(s => s.type === 13); // DebugRetrievalResults
  if (!s) return null;
  const p = s.payload as Record<string, unknown>;
  return {
    filteredCount: typeof p.filteredCount === 'number' ? p.filteredCount : 0,
  };
}

// ─── Metric tile ──────────────────────────────────────────────────────────────

function Metric({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ElementType;
  label: string;
  value: string | number;
}) {
  return (
    <div className="flex items-center gap-1.5 px-3 py-1.5">
      <Icon className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
      <div className="flex flex-col min-w-0">
        <span className="text-[10px] text-muted-foreground leading-none">{label}</span>
        <span className="text-xs font-mono font-semibold leading-none mt-0.5 truncate">{value}</span>
      </div>
    </div>
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export interface DebugSummaryBarProps {
  steps: DebugStep[];
}

export function DebugSummaryBar({ steps }: DebugSummaryBarProps) {
  if (steps.length === 0) return null;

  const totalLatencyMs = sumLatency(steps);
  const cost = extractCostUpdate(steps);
  const retrieval = extractRetrievalResults(steps);

  return (
    <div className="flex flex-wrap items-center gap-0.5 rounded-lg bg-muted/40 border border-border/40 divide-x divide-border/40">
      {totalLatencyMs > 0 && (
        <Metric
          icon={Clock}
          label="Total latency"
          value={`${totalLatencyMs}ms`}
        />
      )}
      {cost && (
        <>
          <Metric
            icon={Cpu}
            label="Tokens in/out"
            value={`${cost.promptTokens} / ${cost.completionTokens}`}
          />
          {cost.confidence !== null && (
            <Metric
              icon={Percent}
              label="Confidence"
              value={`${(cost.confidence * 100).toFixed(0)}%`}
            />
          )}
        </>
      )}
      {retrieval && (
        <Metric
          icon={Search}
          label="KB chunks"
          value={retrieval.filteredCount}
        />
      )}
      {cost?.model && (
        <div className="px-3 py-1.5">
          <span className="text-[10px] font-mono text-muted-foreground truncate">{cost.model}</span>
        </div>
      )}
    </div>
  );
}
