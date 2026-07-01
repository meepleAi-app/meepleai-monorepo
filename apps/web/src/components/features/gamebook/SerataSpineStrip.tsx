import type { ReactElement } from 'react';

import type { GamebookCampaignSpine } from '@/lib/api/gamebook-campaigns';

/**
 * #2632 (SI-1b Phase 4): the "Serata" spine strip. Rendered above the per-session libro-game UIs
 * when a campaign is played as part of a GameNight (`GET /gamebook/campaigns/{id}/spine` returns a
 * spine; a standalone campaign returns 204 → `null` → this strip is not rendered by the parent).
 *
 * Shows the owning GameNight: title, a live/status indicator, and the "Serata" session pip
 * (completed of total games that evening). Uses the `event` entity tokens (the GameNight is an event).
 */
export interface SerataSpineStripProps {
  spine: GamebookCampaignSpine;
}

const STATUS_LABELS: Record<string, string> = {
  Published: 'programmata',
  InProgress: 'in corso',
  Completed: 'completata',
};

export function SerataSpineStrip({ spine }: SerataSpineStripProps): ReactElement {
  const total = Math.max(spine.totalSessions, 0);
  const completed = Math.min(Math.max(spine.completedSessions, 0), total);
  const statusLabel = STATUS_LABELS[spine.gameNightStatus] ?? spine.gameNightStatus.toLowerCase();

  return (
    <div
      data-testid="serata-spine-strip"
      className="flex items-center gap-3 rounded-lg border border-border bg-card px-3 py-2"
      role="group"
      aria-label={`Serata: ${spine.gameNightTitle}`}
    >
      <span aria-hidden className="text-lg">
        🗓️
      </span>

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate font-display text-sm font-semibold text-entity-event">
            {spine.gameNightTitle}
          </span>

          {spine.hasLiveSession ? (
            <span
              className="inline-flex items-center gap-1 rounded-full bg-entity-event/15 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-entity-event"
              data-testid="serata-live-badge"
            >
              <span aria-hidden className="inline-block h-1.5 w-1.5 rounded-full bg-entity-event" />
              live
            </span>
          ) : (
            <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
              {statusLabel}
            </span>
          )}
        </div>

        <div className="mt-0.5 flex items-center gap-2 text-xs text-muted-foreground">
          <span
            className="inline-flex items-center gap-1"
            data-testid="serata-session-pip"
            aria-label={`${completed} di ${total} sessioni della serata`}
          >
            {Array.from({ length: total }).map((_, i) => (
              <span
                key={i}
                aria-hidden
                className={
                  i < completed
                    ? 'inline-block h-1.5 w-1.5 rounded-full bg-entity-event'
                    : 'inline-block h-1.5 w-1.5 rounded-full bg-entity-event/25'
                }
              />
            ))}
            <span className="ml-1">
              {completed}/{total}
            </span>
          </span>
        </div>
      </div>
    </div>
  );
}
