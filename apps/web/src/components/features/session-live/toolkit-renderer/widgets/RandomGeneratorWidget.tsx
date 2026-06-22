'use client';

import type { ReactElement } from 'react';

import type { ParsedWidget, WidgetConfigByType } from '@/lib/session-live/widget-state';

import { WidgetShell } from '../internals/WidgetShell';

import type { ToolkitRendererLabels } from '../labels';

interface Props {
  readonly widget: Extract<ParsedWidget, { type: 'RandomGenerator' }>;
  readonly collapsed: boolean;
  readonly onHeaderClick: () => void;
  readonly onConfigChange: (next: WidgetConfigByType['RandomGenerator']) => void;
  readonly labels: ToolkitRendererLabels;
}

export function RandomGeneratorWidget({
  widget,
  collapsed,
  onHeaderClick,
  onConfigChange,
  labels,
}: Props): ReactElement {
  const { config } = widget;
  const headerAria = (collapsed ? labels.expandAriaTemplate : labels.collapseAriaTemplate).replace(
    '{name}',
    labels.randomGenerator.heading
  );

  const handleRoll = (): void => {
    if (config.faces.length === 0) return;
    const idx = Math.floor(Math.random() * config.faces.length);
    onConfigChange({ ...config, last: config.faces[idx]! });
  };

  return (
    <WidgetShell
      icon="🎲"
      title={config.name || labels.randomGenerator.heading}
      typeLabel="RandomGenerator"
      collapsed={collapsed}
      onHeaderClick={onHeaderClick}
      headerAriaLabel={headerAria}
      entityAccent="tool"
    >
      <div data-slot="widget-random-generator" className="flex items-center gap-3">
        <div className="flex-1 rounded-lg border border-dashed border-entity-tool/30 bg-entity-tool/5 p-3">
          <p className="text-xs font-semibold uppercase tracking-wider text-entity-tool">
            {labels.randomGenerator.lastLabel}
          </p>
          <p
            data-slot="random-generator-last"
            className="mt-1 text-lg font-bold text-foreground tabular-nums"
            aria-live="polite"
          >
            {config.last ?? '—'}
          </p>
        </div>
        <button
          type="button"
          onClick={handleRoll}
          className="shrink-0 rounded-lg bg-entity-tool px-4 py-2 text-sm font-bold text-white shadow-sm transition-colors hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {labels.randomGenerator.rollLabel}
        </button>
      </div>
    </WidgetShell>
  );
}
