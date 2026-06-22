/**
 * DisabledTabPanel — generic Phase-5 disabled-shell for `/toolkits/[id]`.
 *
 * Used by 4 tabs (agent / kb / versions / ratings) per spec Path C while
 * #822 + #819 backend work is pending. Renders a skeleton + tooltip + CTA
 * "Coming in Phase 5" instead of real content. Component never receives the
 * Phase-5 data — it's a pure shell.
 */

'use client';

import type { JSX } from 'react';

import { tabPanelId } from './ToolkitTabs';

import type { ToolkitTabKey } from './ToolkitTabs';

export interface DisabledTabPanelLabels {
  readonly title: string;
  readonly description: string;
  readonly phase5Badge: string;
}

export interface DisabledTabPanelProps {
  readonly tabKey: ToolkitTabKey;
  readonly labels: DisabledTabPanelLabels;
  readonly icon?: string;
  readonly hidden?: boolean;
}

export function DisabledTabPanel({
  tabKey,
  labels,
  icon = '🔒',
  hidden,
}: DisabledTabPanelProps): JSX.Element {
  return (
    <div
      role="tabpanel"
      id={tabPanelId(tabKey)}
      aria-labelledby={`tab-toolkit-detail-${tabKey}`}
      data-slot="toolkit-detail-tabpanel-disabled"
      data-tab-key={tabKey}
      hidden={hidden}
      className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-border bg-muted/30 py-12 text-center"
    >
      <span className="text-5xl" aria-hidden="true">
        {icon}
      </span>
      <h2 className="font-bold font-[Quicksand] text-lg text-foreground">{labels.title}</h2>
      <p className="max-w-md text-sm text-muted-foreground">{labels.description}</p>
      <span className="mt-1 inline-flex items-center rounded-full bg-foreground/10 px-3 py-0.5 font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
        {labels.phase5Badge}
      </span>
    </div>
  );
}
