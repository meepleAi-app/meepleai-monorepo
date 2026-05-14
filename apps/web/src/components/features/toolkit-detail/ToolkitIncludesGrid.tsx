/**
 * ToolkitIncludesGrid — "Cosa include" 3-cell grid for `/toolkits/[id]` Overview.
 *
 * Wave 3 (#1145). Shows the toolkit's bundle at a glance: agent name, KB
 * documents count, tools count. Pure presentational — agent + kb sections
 * render placeholder hint when their Phase-5 enrichment isn't available.
 */

'use client';

import type { JSX } from 'react';

export interface ToolkitIncludesGridLabels {
  readonly agent: string;
  readonly kbDocs: string;
  readonly tools: string;
}

export interface ToolkitIncludesGridProps {
  readonly agentName: string;
  readonly kbDocsCount: number;
  readonly toolsCount: number;
  readonly labels: ToolkitIncludesGridLabels;
}

interface CellProps {
  readonly icon: string;
  readonly title: string;
  readonly value: string;
}

function Cell({ icon, title, value }: CellProps): JSX.Element {
  return (
    <div
      data-slot="toolkit-detail-includes-cell"
      className="flex items-center gap-3 rounded-lg border border-border bg-card p-4"
    >
      <div
        aria-hidden="true"
        className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-muted text-xl"
      >
        {icon}
      </div>
      <div className="min-w-0 flex-1">
        <div className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          {title}
        </div>
        <div className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground">
          {value}
        </div>
      </div>
    </div>
  );
}

export function ToolkitIncludesGrid({
  agentName,
  kbDocsCount,
  toolsCount,
  labels,
}: ToolkitIncludesGridProps): JSX.Element {
  return (
    <div data-slot="toolkit-detail-includes-grid" className="grid grid-cols-1 gap-3 sm:grid-cols-3">
      <Cell icon="🤖" title={labels.agent} value={agentName} />
      <Cell icon="📄" title={labels.kbDocs} value={String(kbDocsCount)} />
      <Cell icon="🔧" title={labels.tools} value={String(toolsCount)} />
    </div>
  );
}
