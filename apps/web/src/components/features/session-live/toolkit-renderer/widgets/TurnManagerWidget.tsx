'use client';

import type { ReactElement } from 'react';

import type { ParsedWidget, WidgetConfigByType } from '@/lib/session-live/widget-state';

import { WidgetShell } from '../internals/WidgetShell';

import type { ToolkitRendererLabels } from '../labels';

interface Props {
  readonly widget: Extract<ParsedWidget, { type: 'TurnManager' }>;
  readonly collapsed: boolean;
  readonly onHeaderClick: () => void;
  readonly onConfigChange: (next: WidgetConfigByType['TurnManager']) => void;
  readonly labels: ToolkitRendererLabels;
}

export function TurnManagerWidget({
  widget,
  collapsed,
  onHeaderClick,
  onConfigChange,
  labels,
}: Props): ReactElement {
  const { config } = widget;
  const headerAria = (collapsed ? labels.expandAriaTemplate : labels.collapseAriaTemplate).replace(
    '{name}',
    labels.turnManager.heading
  );

  const phasesCount = config.phases?.length ?? 0;
  const canPrev = config.activeIndex > 0;
  const canNext = phasesCount === 0 || config.activeIndex < phasesCount - 1;

  const handlePrev = (): void => {
    if (!canPrev) return;
    onConfigChange({ ...config, activeIndex: config.activeIndex - 1 });
  };
  const handleNext = (): void => {
    if (!canNext) return;
    onConfigChange({ ...config, activeIndex: config.activeIndex + 1 });
  };

  const display = config.phaseBased ? (config.phases?.[config.activeIndex] ?? '—') : '—';

  return (
    <WidgetShell
      icon="🔄"
      title={labels.turnManager.heading}
      typeLabel="TurnManager"
      collapsed={collapsed}
      onHeaderClick={onHeaderClick}
      headerAriaLabel={headerAria}
      entityAccent="player"
    >
      <div data-slot="widget-turn-manager" className="flex items-center gap-2">
        <button
          type="button"
          onClick={handlePrev}
          disabled={!canPrev}
          className="rounded-md border border-border bg-muted px-3 py-2 text-xs font-bold text-muted-foreground transition-colors hover:bg-muted/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-40"
        >
          ‹ {labels.turnManager.prevLabel}
        </button>
        <div className="flex-1 text-center" aria-live="polite">
          <p className="text-xs font-semibold uppercase tracking-wider text-entity-player">
            {config.phaseBased ? labels.turnManager.phaseLabel : labels.turnManager.turnOfLabel}
          </p>
          <p className="text-sm font-bold text-foreground">{display}</p>
        </div>
        <button
          type="button"
          onClick={handleNext}
          disabled={!canNext}
          className="rounded-md border border-entity-player/40 bg-entity-player/10 px-3 py-2 text-xs font-bold text-entity-player transition-colors hover:bg-entity-player/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-40"
        >
          {labels.turnManager.nextLabel} ›
        </button>
      </div>
    </WidgetShell>
  );
}
