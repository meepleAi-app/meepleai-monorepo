/**
 * ToolsTabPanel — `?tab=tools` content for `/toolkits/[id]`.
 *
 * Enabled tab per spec Path C. v1 shows only the aggregate tools count from
 * the ToolkitDetailDto; per-tool detail (timer, dice, counter, deck configs)
 * is part of the Phase 5 own-variant work tracked in #822. Renders an empty
 * state when toolsCount === 0.
 */

'use client';

import type { JSX } from 'react';

import { tabPanelId } from './ToolkitTabs';

export interface ToolsTabPanelLabels {
  readonly heading: string;
  readonly description: string;
  readonly countTemplate: string;
  readonly emptyTitle: string;
  readonly emptyBody: string;
}

export interface ToolsTabPanelProps {
  readonly toolsCount: number;
  readonly labels: ToolsTabPanelLabels;
  readonly hidden?: boolean;
}

export function ToolsTabPanel({ toolsCount, labels, hidden }: ToolsTabPanelProps): JSX.Element {
  if (toolsCount === 0) {
    return (
      <div
        role="tabpanel"
        id={tabPanelId('tools')}
        aria-labelledby="tab-toolkit-detail-tools"
        data-slot="toolkit-detail-tabpanel-tools"
        hidden={hidden}
        className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-border bg-muted/30 py-10 text-center"
      >
        <span className="text-4xl" aria-hidden="true">
          🔧
        </span>
        <h2 className="font-bold font-[Quicksand] text-base text-foreground">
          {labels.emptyTitle}
        </h2>
        <p className="max-w-sm text-sm text-muted-foreground">{labels.emptyBody}</p>
      </div>
    );
  }

  return (
    <div
      role="tabpanel"
      id={tabPanelId('tools')}
      aria-labelledby="tab-toolkit-detail-tools"
      data-slot="toolkit-detail-tabpanel-tools"
      hidden={hidden}
      className="flex flex-col gap-4"
    >
      <header>
        <h2 className="font-bold font-[Quicksand] text-lg text-foreground">{labels.heading}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{labels.description}</p>
      </header>
      <div
        data-slot="toolkit-detail-tools-summary"
        className="rounded-xl border border-border bg-card p-5"
      >
        <div className="flex items-center gap-4">
          <div
            aria-hidden="true"
            className="flex h-12 w-12 items-center justify-center rounded-lg bg-muted text-2xl"
          >
            🛠️
          </div>
          <div className="min-w-0 flex-1">
            <div className="font-bold font-[Quicksand] text-2xl text-foreground tabular-nums">
              {toolsCount}
            </div>
            <div className="font-mono text-xs uppercase tracking-wider text-muted-foreground">
              {labels.countTemplate.replace('{count}', String(toolsCount))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
