'use client';

/**
 * ToolkitRenderer — Issue #2376 G5c polymorphic dispatcher.
 *
 * Switches on widget.type and renders one of 6 widget components, or an
 * UnknownWidget fallback for unregistered values.
 *
 * Accordion FSM: single-expanded (DEC-5). Click header to toggle.
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2376-g5c-toolkit-renderer-design.md §3
 */

import type { ReactElement } from 'react';

import type { ParsedWidget, WidgetConfigByType, WidgetType } from '@/lib/session-live/widget-state';

import { NoteManagerWidget } from './widgets/NoteManagerWidget';
import { RandomGeneratorWidget } from './widgets/RandomGeneratorWidget';
import { ResourceManagerWidget } from './widgets/ResourceManagerWidget';
import { ScoreTrackerWidget } from './widgets/ScoreTrackerWidget';
import { TurnManagerWidget } from './widgets/TurnManagerWidget';
import { UnknownWidget } from './widgets/UnknownWidget';
import { WhiteboardWidget } from './widgets/WhiteboardWidget';

import type { ToolkitRendererLabels } from './labels';

export type { ToolkitRendererLabels };

export interface ToolkitRendererProps {
  readonly widgets: ReadonlyArray<ParsedWidget>;
  readonly openWidgetId: string | null;
  readonly onOpenWidgetChange: (id: string | null) => void;
  readonly onWidgetConfigChange: <T extends WidgetType>(
    widgetId: string,
    nextConfig: WidgetConfigByType[T]
  ) => void;
  readonly players?: ReadonlyArray<{ id: string; name: string }>;
  readonly labels: ToolkitRendererLabels;
}

const KNOWN_TYPES: ReadonlySet<WidgetType> = new Set([
  'RandomGenerator',
  'TurnManager',
  'ScoreTracker',
  'ResourceManager',
  'NoteManager',
  'Whiteboard',
]);

export function ToolkitRenderer({
  widgets,
  openWidgetId,
  onOpenWidgetChange,
  onWidgetConfigChange,
  players,
  labels,
}: ToolkitRendererProps): ReactElement {
  const enabled = widgets.filter(w => w.isEnabled).sort((a, b) => a.displayOrder - b.displayOrder);

  if (enabled.length === 0) {
    return (
      <section
        data-slot="toolkit-renderer"
        data-empty="true"
        role="region"
        aria-label={labels.title}
        className="flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-border bg-muted/10 p-6 text-center"
      >
        <p className="text-sm font-bold text-foreground">{labels.emptyTitle}</p>
        <p className="text-xs text-muted-foreground">{labels.emptyBody}</p>
      </section>
    );
  }

  return (
    <section
      data-slot="toolkit-renderer"
      role="region"
      aria-label={labels.title}
      className="flex flex-col gap-2"
    >
      {enabled.map(w => {
        const collapsed = w.id !== openWidgetId;
        const onHeaderClick = (): void => {
          onOpenWidgetChange(collapsed ? w.id : null);
        };

        if (!KNOWN_TYPES.has(w.type as WidgetType)) {
          console.warn('[ToolkitRenderer] Unknown widget type:', (w as { type: string }).type);
          return <UnknownWidget key={w.id} widget={w} labels={labels} />;
        }

        switch (w.type) {
          case 'RandomGenerator':
            return (
              <div key={w.id} data-widget-type={w.type}>
                <RandomGeneratorWidget
                  widget={w}
                  collapsed={collapsed}
                  onHeaderClick={onHeaderClick}
                  onConfigChange={cfg => onWidgetConfigChange(w.id, cfg)}
                  labels={labels}
                />
              </div>
            );
          case 'TurnManager':
            return (
              <div key={w.id} data-widget-type={w.type}>
                <TurnManagerWidget
                  widget={w}
                  collapsed={collapsed}
                  onHeaderClick={onHeaderClick}
                  onConfigChange={cfg => onWidgetConfigChange(w.id, cfg)}
                  labels={labels}
                />
              </div>
            );
          case 'ScoreTracker':
            return (
              <div key={w.id} data-widget-type={w.type}>
                <ScoreTrackerWidget
                  widget={w}
                  players={players}
                  collapsed={collapsed}
                  onHeaderClick={onHeaderClick}
                  onConfigChange={cfg => onWidgetConfigChange(w.id, cfg)}
                  labels={labels}
                />
              </div>
            );
          case 'ResourceManager':
            return (
              <div key={w.id} data-widget-type={w.type}>
                <ResourceManagerWidget
                  widget={w}
                  collapsed={collapsed}
                  onHeaderClick={onHeaderClick}
                  onConfigChange={cfg => onWidgetConfigChange(w.id, cfg)}
                  labels={labels}
                />
              </div>
            );
          case 'NoteManager':
            return (
              <div key={w.id} data-widget-type={w.type}>
                <NoteManagerWidget
                  widget={w}
                  collapsed={collapsed}
                  onHeaderClick={onHeaderClick}
                  onConfigChange={cfg => onWidgetConfigChange(w.id, cfg)}
                  labels={labels}
                />
              </div>
            );
          case 'Whiteboard':
            return (
              <div key={w.id} data-widget-type={w.type}>
                <WhiteboardWidget
                  widget={w}
                  collapsed={collapsed}
                  onHeaderClick={onHeaderClick}
                  onConfigChange={cfg => onWidgetConfigChange(w.id, cfg)}
                  labels={labels}
                />
              </div>
            );
        }
      })}
    </section>
  );
}
