'use client';

import type { ReactElement } from 'react';

import type { ParsedWidget } from '@/lib/session-live/widget-state';

import type { ToolkitRendererLabels } from '../labels';

interface Props {
  readonly widget: ParsedWidget;
  readonly labels: ToolkitRendererLabels;
}

export function UnknownWidget({ widget, labels }: Props): ReactElement {
  return (
    <section
      data-slot="widget-unknown"
      role="status"
      aria-live="polite"
      className="flex flex-col gap-2 rounded-lg border border-amber-700/30 bg-amber-900/10 p-3"
    >
      <p className="text-sm font-bold text-amber-200">{labels.unknownTitle}</p>
      <p className="text-xs text-amber-100/70">{labels.unknownBody}</p>
      <code className="text-xs text-amber-100/50">
        widget.type: {(widget as { type: string }).type}
      </code>
    </section>
  );
}
