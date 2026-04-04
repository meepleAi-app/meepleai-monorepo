'use client';

import { useState } from 'react';

import {
  Search,
  Database,
  FileSearch,
  GitMerge,
  ArrowUpDown,
  Cpu,
  ChevronRight,
  ChevronDown,
} from 'lucide-react';

import type { PipelineTrace, PipelineTraceStep } from './types';

interface PipelineTraceTreeProps {
  trace: PipelineTrace;
}

const STEP_ICONS: Record<string, typeof Search> = {
  'Query Analysis': Search,
  'Dense Search': Database,
  'Sparse Search': FileSearch,
  'Hybrid Merge': GitMerge,
  Reranking: ArrowUpDown,
  'LLM Generation': Cpu,
};

function TraceNode({ step, index }: { step: PipelineTraceStep; index: number }) {
  const [expanded, setExpanded] = useState(false);
  const Icon = STEP_ICONS[step.name] || Search;

  return (
    <div className="font-nunito" data-testid={`trace-step-${index}`}>
      <button
        className="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm hover:bg-gray-50 transition-colors"
        onClick={() => setExpanded(!expanded)}
        data-testid={`trace-step-toggle-${index}`}
      >
        <span className="text-muted-foreground">
          {expanded ? (
            <ChevronDown className="h-3.5 w-3.5" />
          ) : (
            <ChevronRight className="h-3.5 w-3.5" />
          )}
        </span>
        <Icon className="h-4 w-4 text-amber-600 shrink-0" />
        <span className="flex-1 font-medium text-xs">{step.name}</span>
        <span
          className="text-xs text-muted-foreground tabular-nums"
          data-testid={`trace-step-duration-${index}`}
        >
          {step.durationMs}ms
        </span>
      </button>

      {expanded && (
        <div className="ml-10 mb-1 space-y-0.5" data-testid={`trace-step-details-${index}`}>
          {Object.entries(step.details).map(([key, value]) => (
            <div key={key} className="flex items-center gap-2 text-xs text-muted-foreground px-2">
              <span className="text-foreground/60">{key}:</span>
              <span className="font-medium">{String(value)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export function PipelineTraceTree({ trace }: PipelineTraceTreeProps) {
  return (
    <div className="space-y-0.5" data-testid="pipeline-trace-tree">
      {trace.steps.map((step, i) => (
        <TraceNode key={`${step.name}-${i}`} step={step} index={i} />
      ))}
      <div className="mt-2 flex items-center justify-between border-t pt-2 px-2 text-xs font-nunito">
        <span className="text-muted-foreground">Totale</span>
        <span className="font-semibold tabular-nums" data-testid="trace-total-duration">
          {trace.totalDurationMs}ms
        </span>
      </div>
    </div>
  );
}
