/**
 * GameDetailSessionsRail - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (SessionsTab + SessionListItem).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Renders recent sessions as a vertical card list (mobile) or horizontal scroll
 * rail (sm+). Empty state surfaces a CTA + helpful copy. Each session shows
 * playedAt, duration, players, and win/loss outcome.
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

export interface GameDetailSessionEntry {
  readonly id: string;
  readonly playedAt: string;
  readonly durationFormatted: string;
  readonly didWin: boolean | null;
  readonly playersLine: string;
}

export interface GameDetailSessionsRailLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly viewAll: string;
  readonly viewAllAriaLabel: string;
  readonly empty: string;
  readonly emptySubtitle: string;
  readonly newSession: string;
  readonly winLabel: string;
  readonly lossLabel: string;
}

export interface GameDetailSessionsRailProps {
  readonly sessions: ReadonlyArray<GameDetailSessionEntry>;
  readonly viewAllHref: string;
  readonly labels: GameDetailSessionsRailLabels;
  readonly onNewSession?: () => void;
  readonly className?: string;
}

function formatPlayedAt(iso: string, locale: string): string {
  try {
    const d = new Date(iso);
    if (isNaN(d.getTime())) return iso;
    return d.toLocaleDateString(locale, { day: '2-digit', month: 'short', year: 'numeric' });
  } catch {
    return iso;
  }
}

export function GameDetailSessionsRail(props: GameDetailSessionsRailProps): ReactElement {
  const { sessions, viewAllHref, labels, onNewSession, className } = props;
  const isEmpty = sessions.length === 0;

  return (
    <section
      data-slot="game-detail-sessions-rail"
      data-empty={isEmpty}
      className={clsx('flex flex-col gap-3', className)}
    >
      <header className="flex items-baseline justify-between gap-2">
        <div>
          <h3 className="font-display text-[15px] font-extrabold text-foreground">
            {labels.title}
          </h3>
          <p className="mt-0.5 text-[12px] text-muted-foreground">{labels.subtitle}</p>
        </div>
        <div className="flex items-center gap-2">
          {!isEmpty ? (
            <Link
              href={viewAllHref}
              aria-label={labels.viewAllAriaLabel}
              data-slot="game-detail-sessions-view-all"
              className="rounded-md border border-border px-3 py-1 font-display text-[11px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {labels.viewAll}
            </Link>
          ) : null}
          {onNewSession ? (
            <button
              type="button"
              onClick={onNewSession}
              data-slot="game-detail-sessions-new"
              className="rounded-md border-none bg-emerald-600 px-3 py-1 font-display text-[11px] font-extrabold text-white shadow-sm transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {labels.newSession}
            </button>
          ) : null}
        </div>
      </header>

      {isEmpty ? (
        <div
          data-slot="game-detail-sessions-empty"
          className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-border bg-muted/30 px-6 py-10 text-center"
        >
          <span aria-hidden="true" className="text-3xl">
            🎯
          </span>
          <h4 className="font-display text-[14px] font-extrabold text-foreground">
            {labels.empty}
          </h4>
          <p className="max-w-sm text-[12px] text-muted-foreground">{labels.emptySubtitle}</p>
        </div>
      ) : (
        <ul
          role="list"
          className="flex flex-col gap-2 sm:flex-row sm:overflow-x-auto sm:[scrollbar-width:none] sm:[&::-webkit-scrollbar]:hidden"
        >
          {sessions.map(session => {
            const winText =
              session.didWin === true
                ? labels.winLabel
                : session.didWin === false
                  ? labels.lossLabel
                  : null;
            return (
              <li
                key={session.id}
                data-slot="game-detail-session-card"
                className="flex min-w-[260px] flex-col gap-1.5 rounded-2xl border border-border bg-card p-3.5 shadow-sm transition-shadow hover:shadow-md focus-within:ring-2 focus-within:ring-ring sm:min-w-[280px]"
              >
                <div className="flex items-baseline gap-2">
                  <span className="font-mono text-[12px] font-bold text-foreground">
                    {formatPlayedAt(session.playedAt, 'it-IT')}
                  </span>
                  {winText ? (
                    <span
                      data-slot="game-detail-session-outcome"
                      data-outcome={session.didWin ? 'win' : 'loss'}
                      className={clsx(
                        'rounded-full px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-[0.06em]',
                        session.didWin
                          ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300'
                          : 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300'
                      )}
                    >
                      {winText}
                    </span>
                  ) : null}
                </div>
                <div className="font-mono text-[11px] text-muted-foreground">
                  {session.durationFormatted} · {session.playersLine}
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
