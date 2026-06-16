'use client';

import type { ReactElement } from 'react';

import type { ParsedWidget, WidgetConfigByType } from '@/lib/session-live/widget-state';

import { Stepper } from '../internals/Stepper';
import { WidgetShell } from '../internals/WidgetShell';

import type { ToolkitRendererLabels } from '../labels';

interface Props {
  readonly widget: Extract<ParsedWidget, { type: 'ResourceManager' }>;
  readonly collapsed: boolean;
  readonly onHeaderClick: () => void;
  readonly onConfigChange: (next: WidgetConfigByType['ResourceManager']) => void;
  readonly labels: ToolkitRendererLabels;
}

export function ResourceManagerWidget({
  widget,
  collapsed,
  onHeaderClick,
  onConfigChange,
  labels,
}: Props): ReactElement {
  const { config } = widget;
  const headerAria = (collapsed ? labels.expandAriaTemplate : labels.collapseAriaTemplate).replace(
    '{name}',
    config.shared ? labels.resourceManager.sharedHeading : labels.resourceManager.heading
  );

  const handleResourceChange = (idx: number, next: number): void => {
    const resources = config.resources.map((r, i) => (i === idx ? { ...r, value: next } : r));
    onConfigChange({ ...config, resources });
  };

  return (
    <WidgetShell
      icon="📦"
      title={config.shared ? labels.resourceManager.sharedHeading : labels.resourceManager.heading}
      typeLabel="ResourceManager"
      collapsed={collapsed}
      onHeaderClick={onHeaderClick}
      headerAriaLabel={headerAria}
      entityAccent="kb"
    >
      <div data-slot="widget-resource-manager" className="flex flex-col gap-1.5">
        {config.resources.length === 0 ? (
          <p className="text-xs text-muted-foreground italic">Nessuna risorsa</p>
        ) : (
          config.resources.map((r, i) => (
            <div
              key={r.label}
              className={`flex items-center gap-2 rounded-md border px-2 py-1.5 ${
                r.danger ? 'border-entity-event/40 bg-entity-event/5' : 'border-transparent'
              }`}
            >
              <span
                className={`flex-1 text-sm font-semibold ${
                  r.danger ? 'text-entity-event' : 'text-foreground'
                }`}
              >
                {r.label}
              </span>
              <span className="text-xs font-medium text-muted-foreground">/{r.max}</span>
              <Stepper
                value={r.value}
                onChange={next => handleResourceChange(i, next)}
                min={0}
                max={r.max}
                incrementAriaLabel={labels.resourceManager.incrementAriaTemplate.replace(
                  '{label}',
                  r.label
                )}
                decrementAriaLabel={labels.resourceManager.decrementAriaTemplate.replace(
                  '{label}',
                  r.label
                )}
                variant={r.danger ? 'event' : 'kb'}
              />
            </div>
          ))
        )}
      </div>
    </WidgetShell>
  );
}
