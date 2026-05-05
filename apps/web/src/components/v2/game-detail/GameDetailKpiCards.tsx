/**
 * GameDetailKpiCards - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (KpiCard).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Pure presentational grid of 4 KPI cards: rating / complexity / players / play time.
 * Each card renders an icon, label, and tabular-nums value with optional unit.
 *
 * AC: T A V (no animation, no V change beyond responsive grid)
 */

'use client';

import type { ReactElement, ReactNode } from 'react';

import clsx from 'clsx';

export interface GameDetailKpiCard {
  readonly key: string;
  readonly label: string;
  readonly value: string;
  readonly unit?: string;
  readonly icon?: ReactNode;
  readonly accent?: 'rating' | 'complexity' | 'players' | 'time';
}

export interface GameDetailKpiCardsProps {
  readonly cards: ReadonlyArray<GameDetailKpiCard>;
  readonly className?: string;
}

const ACCENT_CLASSES: Record<NonNullable<GameDetailKpiCard['accent']>, string> = {
  rating: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300',
  complexity: 'bg-violet-100 text-violet-700 dark:bg-violet-950/40 dark:text-violet-300',
  players: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300',
  time: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300',
};

export function GameDetailKpiCards(props: GameDetailKpiCardsProps): ReactElement {
  const { cards, className } = props;

  return (
    <div
      data-slot="game-detail-kpi-cards"
      role="list"
      aria-label="KPI"
      className={clsx('grid gap-2.5', 'grid-cols-2 sm:grid-cols-2 md:grid-cols-4', className)}
    >
      {cards.map(card => (
        <div
          key={card.key}
          role="listitem"
          data-slot="game-detail-kpi-card"
          data-kpi-key={card.key}
          className="flex flex-col gap-1.5 rounded-2xl border border-border bg-card p-3.5 shadow-sm"
        >
          <div className="flex items-center gap-1.5">
            {card.icon ? (
              <span
                aria-hidden="true"
                className={clsx(
                  'inline-flex h-6 w-6 items-center justify-center rounded-md text-[12px]',
                  card.accent ? ACCENT_CLASSES[card.accent] : 'bg-muted text-muted-foreground'
                )}
              >
                {card.icon}
              </span>
            ) : null}
            <span className="font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground">
              {card.label}
            </span>
          </div>
          <div className="font-display text-[28px] font-extrabold leading-none tabular-nums text-foreground">
            {card.value}
            {card.unit ? (
              <span className="ml-1 text-[13px] font-semibold text-muted-foreground">
                {card.unit}
              </span>
            ) : null}
          </div>
        </div>
      ))}
    </div>
  );
}
