'use client';

import { type ReactElement } from 'react';

import { codenamesKeyColor } from './codenames-palette';
import { teamCounts, type CodenamesGameState, type CodenamesTeam } from './codenames-state';

export interface CodenamesTeamTrackerProps {
  readonly board: CodenamesGameState['board'];
  readonly currentTeam: CodenamesGameState['currentTeam'];
  readonly labels: {
    redLabel: string;
    blueLabel: string;
    foundTemplate: string;
    turnLabel: string;
  };
}

export function CodenamesTeamTracker({
  board,
  currentTeam,
  labels,
}: CodenamesTeamTrackerProps): ReactElement {
  const teams: Array<{ id: CodenamesTeam; label: string }> = [
    { id: 'red', label: labels.redLabel },
    { id: 'blue', label: labels.blueLabel },
  ];
  return (
    <div data-slot="codenames-teams" className="flex gap-2">
      {teams.map(({ id, label }) => {
        const { total, found } = teamCounts(board, id);
        const isCurrent = currentTeam === id;
        return (
          <div
            key={id}
            data-team={id}
            data-current={isCurrent ? 'true' : 'false'}
            className={[
              'flex flex-1 items-center gap-2 rounded-lg border px-2 py-1.5',
              isCurrent ? 'border-border-strong bg-muted' : 'border-border bg-card',
            ].join(' ')}
          >
            <span
              aria-hidden="true"
              className="h-3 w-3 shrink-0 rounded-full"
              style={{ backgroundColor: codenamesKeyColor(id) }}
            />
            <span className="text-xs font-semibold text-foreground">{label}</span>
            {isCurrent && (
              <span className="rounded bg-background px-1 text-[10px] uppercase text-muted-foreground">
                {labels.turnLabel}
              </span>
            )}
            <span className="ml-auto tabular-nums text-sm font-bold text-foreground">
              {labels.foundTemplate
                .replace('{found}', String(found))
                .replace('{total}', String(total))}
            </span>
          </div>
        );
      })}
    </div>
  );
}
