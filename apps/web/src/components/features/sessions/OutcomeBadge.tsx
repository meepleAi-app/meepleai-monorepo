/**
 * OutcomeBadge — Wave D.1 v2 component (Issue #735).
 *
 * Mapped from `admin-mockups/design_files/sp4-sessions-index.jsx` (OutcomeBadge).
 *
 * Pure component: all i18n strings injected via `labels` — no `useTranslation`.
 *
 * Discriminated by `status × outcome × paused` matrix:
 *   inprogress + paused=true  → statusPaused (slate, warning)
 *   inprogress + paused=false → statusLive (session entity color, pulse dot)
 *   abandoned                 → statusAbandoned (slate muted)
 *   completed + won           → outcomeWon (emerald-700 — WCAG AA on white)
 *   completed + lost          → outcomeLost (rose-700 — WCAG AA on white)
 *   completed + tie           → outcomeTie (slate-700 — WCAG AA on white)
 *
 * WCAG AA: 700-shade text colors per Wave C.1 hotfix lesson (never 600 on white).
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { SessionListItem } from '@/lib/sessions/sessions-filters';

export interface OutcomeBadgeLabels {
  readonly outcomeWon: string;
  readonly outcomeLost: string;
  readonly outcomeTie: string;
  readonly statusLive: string;
  readonly statusPaused: string;
  readonly statusAbandoned: string;
}

export interface OutcomeBadgeProps {
  readonly status: SessionListItem['status'];
  readonly outcome: SessionListItem['outcome'];
  readonly paused?: boolean;
  readonly labels: OutcomeBadgeLabels;
  readonly className?: string;
}

const PILL_BASE =
  'inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-extrabold uppercase tracking-wider';

export function OutcomeBadge({
  status,
  outcome,
  paused = false,
  labels,
  className,
}: OutcomeBadgeProps): ReactElement | null {
  // inprogress + paused
  if (status === 'inprogress' && paused) {
    return (
      <span
        data-slot="outcome-badge"
        data-status="paused"
        className={clsx(PILL_BASE, 'bg-amber-50 text-amber-700 ring-1 ring-amber-200', className)}
      >
        {labels.statusPaused}
      </span>
    );
  }

  // inprogress + live
  if (status === 'inprogress') {
    return (
      <span
        data-slot="outcome-badge"
        data-status="live"
        className={clsx(
          PILL_BASE,
          'mai-pulse bg-entity-session/14 text-entity-session ring-1 ring-entity-session/30',
          className
        )}
      >
        <span
          aria-hidden="true"
          className="inline-block h-1.5 w-1.5 rounded-full bg-entity-session"
        />
        {labels.statusLive}
      </span>
    );
  }

  // abandoned
  if (status === 'abandoned') {
    return (
      <span
        data-slot="outcome-badge"
        data-status="abandoned"
        className={clsx(PILL_BASE, 'bg-slate-100 text-slate-700 ring-1 ring-slate-200', className)}
      >
        {labels.statusAbandoned}
      </span>
    );
  }

  // completed — outcome matrix
  if (status === 'completed') {
    if (outcome === 'won') {
      return (
        <span
          data-slot="outcome-badge"
          data-outcome="won"
          className={clsx(
            PILL_BASE,
            'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
            className
          )}
        >
          {labels.outcomeWon}
        </span>
      );
    }
    if (outcome === 'lost') {
      return (
        <span
          data-slot="outcome-badge"
          data-outcome="lost"
          className={clsx(PILL_BASE, 'bg-rose-50 text-rose-700 ring-1 ring-rose-200', className)}
        >
          {labels.outcomeLost}
        </span>
      );
    }
    // tie or null outcome on completed
    return (
      <span
        data-slot="outcome-badge"
        data-outcome="tie"
        className={clsx(PILL_BASE, 'bg-slate-100 text-slate-700 ring-1 ring-slate-200', className)}
      >
        {labels.outcomeTie}
      </span>
    );
  }

  // paused status
  if (status === 'paused') {
    return (
      <span
        data-slot="outcome-badge"
        data-status="paused"
        className={clsx(PILL_BASE, 'bg-amber-50 text-amber-700 ring-1 ring-amber-200', className)}
      >
        {labels.statusPaused}
      </span>
    );
  }

  return null;
}
