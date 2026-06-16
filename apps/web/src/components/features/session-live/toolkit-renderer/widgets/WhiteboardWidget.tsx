'use client';

import type { ReactElement } from 'react';

import type { ParsedWidget, WidgetConfigByType } from '@/lib/session-live/widget-state';

import { WidgetShell } from '../internals/WidgetShell';

import type { ToolkitRendererLabels } from '../labels';

interface Props {
  readonly widget: Extract<ParsedWidget, { type: 'Whiteboard' }>;
  readonly collapsed: boolean;
  readonly onHeaderClick: () => void;
  readonly onConfigChange: (next: WidgetConfigByType['Whiteboard']) => void;
  readonly labels: ToolkitRendererLabels;
}

// MVP: tool palette + dashed placeholder canvas. Real drawing deferred per spec §10.
// onConfigChange wired but currently unused — toolkit save is a no-op in MVP.

export function WhiteboardWidget({
  widget,
  collapsed,
  onHeaderClick,
  onConfigChange,
  labels,
}: Props): ReactElement {
  const headerAria = (collapsed ? labels.expandAriaTemplate : labels.collapseAriaTemplate).replace(
    '{name}',
    labels.whiteboard.heading
  );

  return (
    <WidgetShell
      icon="🖊"
      title={labels.whiteboard.heading}
      typeLabel="Whiteboard"
      collapsed={collapsed}
      onHeaderClick={onHeaderClick}
      headerAriaLabel={headerAria}
      entityAccent="chat"
    >
      <div data-slot="widget-whiteboard" className="flex flex-col gap-2">
        <div role="toolbar" aria-label={labels.whiteboard.heading} className="flex gap-1.5">
          <button
            type="button"
            aria-label={labels.whiteboard.toolPenLabel}
            className="flex h-7 w-7 items-center justify-center rounded border border-entity-chat/40 bg-entity-chat/10 text-sm text-entity-chat focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            ✏️
          </button>
          <button
            type="button"
            aria-label={labels.whiteboard.toolEraserLabel}
            className="flex h-7 w-7 items-center justify-center rounded border border-border bg-muted text-sm text-muted-foreground hover:bg-muted/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            🩹
          </button>
          <button
            type="button"
            aria-label={labels.whiteboard.toolCircleLabel}
            className="flex h-7 w-7 items-center justify-center rounded border border-border bg-muted text-sm text-muted-foreground hover:bg-muted/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            ⭕
          </button>
        </div>
        <div
          className="flex h-24 items-center justify-center rounded border border-dashed border-entity-chat/30 bg-card text-xs font-medium text-muted-foreground"
          aria-label={labels.whiteboard.placeholderLabel}
        >
          {labels.whiteboard.placeholderLabel}
        </div>
      </div>
    </WidgetShell>
  );
}
