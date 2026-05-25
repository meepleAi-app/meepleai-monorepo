/**
 * PromptPreviewBlock — system-prompt preview for `/toolkits/[id]` Agent tab.
 *
 * Wave 3 (#1479). Maps the mockup AgentTab "System prompt (preview)" section
 * (sp4-toolkit-detail.jsx:560-592). Pure presentational, labels injected.
 *
 * Source data: ToolkitAgentSummary.systemPromptPreview. The token count is not
 * exposed by the v1 backend schema (Gate B) — it is optional and hidden when
 * absent.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface PromptPreviewBlockLabels {
  readonly heading: string;
  readonly tokenCountSuffix: string;
}

export interface PromptPreviewBlockProps {
  readonly systemPrompt: string;
  readonly tokenCount?: number | null;
  readonly labels: PromptPreviewBlockLabels;
  readonly className?: string;
}

export function PromptPreviewBlock({
  systemPrompt,
  tokenCount,
  labels,
  className,
}: PromptPreviewBlockProps): JSX.Element {
  return (
    <div
      data-slot="toolkit-detail-prompt-preview"
      className={clsx('flex flex-col gap-2', className)}
    >
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-bold font-[Quicksand] text-sm text-foreground">{labels.heading}</h3>
        {tokenCount != null && (
          <span className="font-mono text-[10px] font-bold text-muted-foreground tabular-nums">
            {tokenCount} {labels.tokenCountSuffix}
          </span>
        )}
      </div>
      <pre className="m-0 max-h-[220px] overflow-auto whitespace-pre-wrap rounded-lg border border-border bg-muted p-3.5 font-mono text-[11.5px] leading-relaxed text-foreground">
        {systemPrompt}
      </pre>
    </div>
  );
}
