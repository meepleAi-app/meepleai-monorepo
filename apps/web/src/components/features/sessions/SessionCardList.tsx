/**
 * SessionCardList — Wave D.1 v2 component (Issue #735).
 *
 * Mapped from `admin-mockups/design_files/sp4-sessions-index.jsx` (SessionCardList).
 *
 * Pure component: renders a single session row in list view.
 * Wraps MeepleCard entity="session" variant="list" inside a button for click handling.
 *
 * Layout (mockup-derived):
 *   - Left accent bar (session entity color) via MeepleCard entity prop
 *   - Cover placeholder: session entity emoji 🎯 on entity-colored bg
 *   - Title: gameName + date · duration meta
 *   - OutcomeBadge inline
 *   - ScoringInline (full display)
 *   - ConnectionChipStripFooter: game chip + player count + chat count (max 3)
 *
 * Abandoned sessions: reduced opacity (0.7) per mockup.
 * In-progress sessions: mai-pulse class on wrapper for live dot animation.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';


import { ConnectionChipStripFooter } from '@/components/features/sessions/ConnectionChipStripFooter';
import { OutcomeBadge } from '@/components/features/sessions/OutcomeBadge';
import type { OutcomeBadgeLabels } from '@/components/features/sessions/OutcomeBadge';
import { ScoringInline } from '@/components/features/sessions/ScoringInline';
import type { SessionListItem } from '@/lib/sessions/sessions-filters';

export interface SessionCardListLabels {
  readonly outcomeWon: string;
  readonly outcomeLost: string;
  readonly outcomeTie: string;
  readonly statusLive: string;
  readonly statusPaused: string;
  readonly statusAbandoned: string;
  readonly playerCountTemplate: string;
  readonly chatCountTemplate: string;
  readonly turnTemplate: string;
  readonly winnerLabel: string;
  readonly openSessionAriaTemplate: string;
}

export interface SessionCardListProps {
  readonly item: SessionListItem;
  readonly onClick: (item: SessionListItem) => void;
  readonly labels: SessionCardListLabels;
  readonly className?: string;
}

export function SessionCardList({
  item,
  onClick,
  labels,
  className,
}: SessionCardListProps): ReactElement {
  const isAbandoned = item.status === 'abandoned';
  const isInProgress = item.status === 'inprogress';
  const isPaused = item.status === 'paused';

  const ariaLabel = labels.openSessionAriaTemplate.replace('{gameName}', item.gameName);
  const turnLabel = item.turn ? labels.turnTemplate.replace('{turn}', item.turn) : undefined;

  const outcomeBadgeLabels: OutcomeBadgeLabels = {
    outcomeWon: labels.outcomeWon,
    outcomeLost: labels.outcomeLost,
    outcomeTie: labels.outcomeTie,
    statusLive: labels.statusLive,
    statusPaused: labels.statusPaused,
    statusAbandoned: labels.statusAbandoned,
  };

  const chips = [
    { entity: 'game' as const, label: item.gameName.slice(0, 10) || 'gioco' },
    { entity: 'player' as const, count: item.playerCount },
    ...(item.hasChat
      ? [{ entity: 'chat' as const, count: item.chatCount ?? 0 }]
      : [{ entity: 'chat' as const, empty: true, count: 0 }]),
  ];

  return (
    <button
      type="button"
      onClick={() => onClick(item)}
      aria-label={ariaLabel}
      data-slot="session-card-list"
      data-item-id={item.id}
      className={clsx(
        'group relative w-full text-left',
        'flex items-stretch gap-0 overflow-hidden rounded-xl border border-border bg-card',
        'border-l-[3px] border-l-entity-session',
        'cursor-pointer transition-shadow hover:shadow-md',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        isAbandoned && 'opacity-70',
        isInProgress && 'mai-pulse',
        className
      )}
    >
      {/* Cover placeholder */}
      <div
        aria-hidden="true"
        className="flex w-[72px] shrink-0 items-center justify-center bg-entity-session/12 text-3xl"
      >
        <span className="drop-shadow-sm">🎯</span>
      </div>

      {/* Body */}
      <div className="flex flex-1 flex-col gap-2 p-3 sm:flex-row sm:items-center sm:gap-4 sm:p-4">
        {/* Left: title + meta + scoring */}
        <div className="flex min-w-0 flex-1 flex-col gap-1.5">
          {/* Title row */}
          <div className="flex flex-wrap items-center gap-1.5">
            <h3 className="truncate font-semibold leading-tight text-foreground text-sm sm:text-base">
              {item.gameName}
            </h3>
            <span className="font-mono text-[10.5px] font-semibold text-muted-foreground">
              · {item.date}
            </span>
            <OutcomeBadge
              status={item.status}
              outcome={item.outcome}
              paused={isPaused}
              labels={outcomeBadgeLabels}
            />
            {(isInProgress || isPaused) && turnLabel && (
              <span className="rounded-full bg-entity-session/10 px-1.5 py-px font-mono text-[9.5px] font-extrabold uppercase tracking-wider text-entity-session">
                {turnLabel}
              </span>
            )}
          </div>

          {/* Meta subtitle */}
          <div className="font-mono text-[11px] font-semibold text-muted-foreground">
            ⏱ {item.duration} ·{' '}
            {labels.playerCountTemplate.replace('{count}', String(item.playerCount))} · {item.when}
          </div>

          {/* Scoring */}
          <ScoringInline scores={item.scores} labels={{ winnerAriaLabel: labels.winnerLabel }} />
        </div>

        {/* Right: chips */}
        <div className="flex shrink-0 flex-col items-start gap-2 sm:items-end">
          <ConnectionChipStripFooter chips={chips} />
        </div>
      </div>
    </button>
  );
}
