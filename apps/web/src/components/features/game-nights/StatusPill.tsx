/**
 * StatusPill - v2 SP4 #1170 commit 2
 *
 * Status pill used in calendar/list views for /game-nights index.
 * Pure presentational: labels resolved by orchestrator (Commit 3).
 *
 * Mapped from `admin-mockups/design_files/sp4-game-nights-index.jsx` (StatusPill).
 */

import clsx from 'clsx';

import type { StatusKey } from '@/lib/game-nights/view-model';

export interface StatusPillLabels {
  readonly confirmed: string;
  readonly planned: string;
  readonly cancelled: string;
  readonly completed: string;
}

export interface StatusPillProps {
  readonly statusKey: StatusKey;
  readonly labels: StatusPillLabels;
  readonly className?: string;
}

const VARIANT: Record<StatusKey, string> = {
  confirmed: 'bg-entity-toolkit/12 text-entity-toolkit border-entity-toolkit/30',
  planned: 'bg-entity-agent/12 text-entity-agent border-entity-agent/30',
  cancelled: 'bg-destructive/12 text-destructive border-destructive/30',
  completed: 'bg-muted text-muted-foreground border-border',
};

const DOT: Record<StatusKey, string> = {
  confirmed: 'bg-entity-toolkit',
  planned: 'bg-entity-agent',
  cancelled: 'bg-destructive',
  completed: 'bg-muted-foreground',
};

export function StatusPill({ statusKey, labels, className }: StatusPillProps): React.JSX.Element {
  return (
    <span
      data-testid="game-nights-status-pill"
      data-status={statusKey}
      className={clsx(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5',
        'font-mono text-[10px] font-extrabold uppercase tracking-wider',
        VARIANT[statusKey],
        className
      )}
    >
      <span aria-hidden="true" className={clsx('h-1.5 w-1.5 rounded-full', DOT[statusKey])} />
      <span>{labels[statusKey]}</span>
    </span>
  );
}
