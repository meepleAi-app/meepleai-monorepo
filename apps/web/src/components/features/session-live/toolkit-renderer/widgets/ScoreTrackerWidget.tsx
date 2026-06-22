'use client';

import type { ReactElement } from 'react';

import type { ParsedWidget, WidgetConfigByType } from '@/lib/session-live/widget-state';

import { Stepper } from '../internals/Stepper';
import { WidgetShell } from '../internals/WidgetShell';

import type { ToolkitRendererLabels } from '../labels';

interface Props {
  readonly widget: Extract<ParsedWidget, { type: 'ScoreTracker' }>;
  readonly players?: ReadonlyArray<{ id: string; name: string }>;
  readonly collapsed: boolean;
  readonly onHeaderClick: () => void;
  readonly onConfigChange: (next: WidgetConfigByType['ScoreTracker']) => void;
  readonly labels: ToolkitRendererLabels;
}

export function ScoreTrackerWidget({
  widget,
  players = [],
  collapsed,
  onHeaderClick,
  onConfigChange,
  labels,
}: Props): ReactElement {
  const { config } = widget;
  const headerAria = (collapsed ? labels.expandAriaTemplate : labels.collapseAriaTemplate).replace(
    '{name}',
    labels.scoreTracker.heading
  );

  const handleScoreChange = (playerId: string, next: number): void => {
    onConfigChange({
      ...config,
      scores: { ...config.scores, [playerId]: next },
    });
  };

  const visiblePlayers = players.length > 0 ? players : [];

  return (
    <WidgetShell
      icon="🧮"
      title={labels.scoreTracker.heading}
      typeLabel="ScoreTracker"
      collapsed={collapsed}
      onHeaderClick={onHeaderClick}
      headerAriaLabel={headerAria}
      entityAccent="session"
    >
      <div data-slot="widget-score-tracker" className="flex flex-col gap-2">
        {visiblePlayers.length === 0 ? (
          <p className="text-xs text-muted-foreground italic">Nessun giocatore</p>
        ) : (
          visiblePlayers.slice(0, 6).map(p => {
            const score = config.scores[p.id] ?? 0;
            return (
              <div key={p.id} className="flex items-center gap-2">
                <span className="flex-1 truncate text-sm font-semibold text-foreground">
                  {p.name}
                </span>
                <Stepper
                  value={score}
                  onChange={next => handleScoreChange(p.id, next)}
                  incrementAriaLabel={labels.scoreTracker.incrementAriaTemplate.replace(
                    '{name}',
                    p.name
                  )}
                  decrementAriaLabel={labels.scoreTracker.decrementAriaTemplate.replace(
                    '{name}',
                    p.name
                  )}
                  wide
                  variant="session"
                />
              </div>
            );
          })
        )}
      </div>
    </WidgetShell>
  );
}
