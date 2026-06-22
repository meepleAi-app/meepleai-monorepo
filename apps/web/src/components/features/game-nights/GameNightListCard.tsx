/**
 * GameNightListCard - v2 SP4 #1170 commit 2
 *
 * List-view card for a single game night. CTA branches by status + role.
 * Pure presentational: orchestrator resolves player roster + game title.
 *
 * Mapped from `admin-mockups/design_files/sp4-game-nights-index.jsx`
 * (GameNightListCard).
 */

import clsx from 'clsx';

import type { GameNightVM } from '@/lib/game-nights/view-model';

import { PlayerAvatars, type AvatarPlayer } from './PlayerAvatars';
import { StatusPill, type StatusPillLabels } from './StatusPill';

export type GameNightListCardAction = 'edit' | 'viewSummary' | 'reschedule' | 'accept' | 'maybe';

export interface GameNightListCardCtaLabels {
  readonly edit: string;
  readonly viewSummary: string;
  readonly reschedule: string;
  readonly accept: string;
  readonly maybe: string;
}

export interface GameNightListCardLabels {
  readonly status: StatusPillLabels;
  readonly organizingBadge: string;
  readonly participants: (count: number) => string;
  readonly cta: GameNightListCardCtaLabels;
  readonly monthAbbrev: string;
}

export interface GameNightListCardProps {
  readonly vm: GameNightVM;
  readonly isMobile?: boolean;
  readonly labels: GameNightListCardLabels;
  readonly onAction?: (id: string, action: GameNightListCardAction) => void;
  readonly gameTitle?: string;
  readonly players?: readonly AvatarPlayer[];
}

export function GameNightListCard({
  vm,
  isMobile = false,
  labels,
  onAction,
  gameTitle,
  players,
}: GameNightListCardProps): React.JSX.Element {
  const isCancelled = vm.statusKey === 'cancelled';
  const playerList = players ?? [];
  const participantCount = playerList.length > 0 ? playerList.length : vm.playerIds.length;

  const dispatch = (action: GameNightListCardAction): void => {
    onAction?.(vm.id, action);
  };

  return (
    <article
      data-testid="game-nights-list-card"
      data-status={vm.statusKey}
      data-role={vm.role}
      className={clsx(
        'flex flex-col gap-2.5 rounded-lg border border-l-4 border-border bg-card p-3.5 transition-all md:flex-row md:gap-3.5 md:p-4',
        isCancelled ? 'border-l-destructive opacity-[0.78]' : 'border-l-entity-event'
      )}
    >
      <div className="flex flex-row items-center justify-center gap-2 md:w-[78px] md:flex-col md:gap-0 md:rounded-md md:border md:border-entity-event/22 md:bg-entity-event/8 md:px-2.5 md:py-1.5">
        <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-event md:text-[9px]">
          {labels.monthAbbrev}
        </span>
        <span className="font-display text-[22px] font-extrabold leading-none tabular-nums text-entity-event md:text-3xl">
          {vm.day}
        </span>
        <span className="font-mono text-[10px] font-bold text-muted-foreground md:mt-0.5">
          {vm.timeLabel}
        </span>
        {vm.durationLabel && (
          <span className="ml-auto font-mono text-[9px] text-muted-foreground md:ml-0 md:mt-0.5">
            {vm.durationLabel}
          </span>
        )}
      </div>

      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <div className="flex flex-wrap items-start gap-2">
          <h3
            className={clsx(
              'min-w-0 flex-1 font-display text-sm font-extrabold leading-snug text-foreground md:text-base',
              isCancelled && 'line-through'
            )}
          >
            {vm.title}
          </h3>
          <StatusPill statusKey={vm.statusKey} labels={labels.status} />
        </div>

        <div className="flex flex-wrap items-center gap-1.5 font-mono text-[11px] text-muted-foreground">
          {vm.location && (
            <>
              <span aria-hidden="true">📍</span>
              <span className="font-semibold">{vm.location}</span>
            </>
          )}
          {gameTitle && (
            <>
              <span aria-hidden="true" className="opacity-40">
                ·
              </span>
              <span className="inline-flex items-center gap-1 whitespace-nowrap rounded-full border border-entity-game/22 bg-entity-game/12 px-2 py-0.5 font-display text-[10px] font-extrabold text-entity-game">
                {gameTitle}
              </span>
            </>
          )}
          {vm.role === 'organizer' && (
            <span className="rounded-full border border-entity-player/22 bg-entity-player/12 px-1.5 py-0.5 font-display text-[10px] font-extrabold text-entity-player">
              {labels.organizingBadge}
            </span>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2.5 border-t border-border pt-1.5">
          <PlayerAvatars players={playerList} max={isMobile ? 4 : 5} />
          <span className="font-mono text-[11px] font-bold text-muted-foreground">
            {labels.participants(participantCount)}
          </span>
          <div className="flex-1" />
          <CardCta vm={vm} labels={labels.cta} onAction={dispatch} />
        </div>
      </div>
    </article>
  );
}

interface CardCtaProps {
  readonly vm: GameNightVM;
  readonly labels: GameNightListCardCtaLabels;
  readonly onAction: (action: GameNightListCardAction) => void;
}

function CardCta({ vm, labels, onAction }: CardCtaProps): React.JSX.Element {
  if (vm.statusKey === 'completed') {
    return (
      <button
        type="button"
        onClick={() => onAction('viewSummary')}
        className="rounded-md border border-border-strong px-3 py-1.5 font-display text-xs font-extrabold text-foreground"
      >
        {labels.viewSummary}
      </button>
    );
  }
  if (vm.statusKey === 'cancelled') {
    return (
      <button
        type="button"
        onClick={() => onAction('reschedule')}
        className="rounded-md border border-border px-3 py-1.5 font-display text-xs font-extrabold text-muted-foreground"
      >
        {labels.reschedule}
      </button>
    );
  }
  // confirmed | planned
  if (vm.role === 'organizer') {
    return (
      <button
        type="button"
        onClick={() => onAction('edit')}
        className="rounded-md bg-entity-event px-3 py-1.5 font-display text-xs font-extrabold text-white"
      >
        {labels.edit}
      </button>
    );
  }
  return (
    <div className="flex gap-1.5">
      <button
        type="button"
        onClick={() => onAction('accept')}
        className="rounded-md bg-entity-toolkit px-3 py-1.5 font-display text-xs font-extrabold text-white"
      >
        {labels.accept}
      </button>
      <button
        type="button"
        onClick={() => onAction('maybe')}
        className="rounded-md border border-border px-3 py-1.5 font-display text-xs font-extrabold text-muted-foreground"
      >
        {labels.maybe}
      </button>
    </div>
  );
}
