/**
 * SessionCardGrid — Wave D.1 v2 component (Issue #735).
 *
 * Mapped from `admin-mockups/design_files/sp4-sessions-index.jsx` (SessionCardGrid).
 *
 * Pure component: renders a single session card in grid view.
 * Wraps content inside a button for click handling.
 *
 * Layout (mockup-derived):
 *   - Top accent bar (session entity color, 3px absolute)
 *   - Cover area: entity-colored bg, 🎯 emoji, OutcomeBadge top-right
 *   - Body: gameName title, date · duration · playerCount meta
 *   - ScoringInline compact (max 3 visible + overflow)
 *   - Footer: ConnectionChipStripFooter (game/player/chat)
 *
 * Abandoned sessions: reduced opacity (0.7).
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';


import { ConnectionChipStripFooter } from '@/components/features/sessions/ConnectionChipStripFooter';
import { OutcomeBadge } from '@/components/features/sessions/OutcomeBadge';
import type { OutcomeBadgeLabels } from '@/components/features/sessions/OutcomeBadge';
import { ScoringInline } from '@/components/features/sessions/ScoringInline';
import type { SessionListItem } from '@/lib/sessions/sessions-filters';

export interface SessionCardGridLabels {
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
  /** e.g. "+{count} altri" for score overflow */
  readonly scoreOverflowTemplate?: string;
}

export interface SessionCardGridProps {
  readonly item: SessionListItem;
  readonly onClick: (item: SessionListItem) => void;
  readonly labels: SessionCardGridLabels;
  readonly className?: string;
}

export function SessionCardGrid({
  item,
  onClick,
  labels,
  className,
}: SessionCardGridProps): ReactElement {
  const isAbandoned = item.status === 'abandoned';
  const isPaused = item.status === 'paused';

  const ariaLabel = labels.openSessionAriaTemplate.replace('{gameName}', item.gameName);

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
      data-slot="session-card-grid"
      data-item-id={item.id}
      className={clsx(
        'group relative flex w-full flex-col overflow-hidden rounded-xl border border-border bg-card text-left',
        'cursor-pointer transition-shadow hover:shadow-md',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        isAbandoned && 'opacity-70',
        className
      )}
    >
      {/* Top accent bar */}
      <div
        aria-hidden="true"
        className="absolute left-0 right-0 top-0 z-10 h-[3px] bg-entity-session"
      />

      {/* Cover area */}
      <div
        aria-hidden="true"
        className="relative flex h-[110px] items-center justify-center bg-entity-session/12 text-[44px]"
      >
        <span className="drop-shadow-md">🎯</span>

        {/* OutcomeBadge top-right */}
        <div className="absolute right-2 top-4">
          <OutcomeBadge
            status={item.status}
            outcome={item.outcome}
            paused={isPaused}
            labels={outcomeBadgeLabels}
          />
        </div>
      </div>

      {/* Body */}
      <div className="flex flex-1 flex-col gap-2 p-3">
        {/* Title + meta */}
        <div>
          <h3 className="truncate text-sm font-bold leading-tight text-foreground">
            {item.gameName}
          </h3>
          <div className="mt-0.5 font-mono text-[10.5px] font-semibold text-muted-foreground">
            {item.date} · ⏱ {item.duration} · 👥 {item.playerCount}
          </div>
        </div>

        {/* Scoring compact */}
        <div className="rounded-md bg-muted/40 px-2 py-1.5">
          <ScoringInline
            scores={item.scores}
            compact
            labels={{
              winnerAriaLabel: labels.winnerLabel,
              overflowTemplate: labels.scoreOverflowTemplate,
            }}
          />
        </div>

        {/* Spacer + footer chips */}
        <div className="mt-auto border-t border-border/40 pt-2">
          <ConnectionChipStripFooter chips={chips} />
        </div>
      </div>
    </button>
  );
}
