/**
 * SessionsTabPanel — content for the "Sessions" tab on /players/[id] (Issue #1113).
 *
 * MVP: shows total session count + CTA link to the existing subroute
 * /players/[id]/sessions which carries the full Wave 3 list view. Follow-up
 * issue will expand into a richer in-tab list once backend exposes a
 * per-player session feed.
 */

'use client';

import Link from 'next/link';
import type { JSX } from 'react';

import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

export interface SessionsTabPanelLabels {
  readonly title: string;
  readonly viewAll: string;
  readonly empty: string;
  readonly totalLabel: string;
}

export interface SessionsTabPanelProps {
  readonly stats: PlayerProfileFixture;
  readonly labels: SessionsTabPanelLabels;
}

export function SessionsTabPanel({ stats, labels }: SessionsTabPanelProps): JSX.Element {
  const isEmpty = stats.totalSessions === 0;
  return (
    <div
      data-slot="sessions-tab-panel"
      className="mx-auto w-full max-w-4xl px-4 sm:px-8 flex flex-col gap-4"
    >
      <h2 className="text-lg font-semibold">{labels.title}</h2>
      {isEmpty ? (
        <p className="text-muted-foreground">{labels.empty}</p>
      ) : (
        <>
          <div className="flex items-baseline gap-2">
            <span className="text-3xl font-bold tabular-nums">{stats.totalSessions}</span>
            <span className="text-muted-foreground">{labels.totalLabel}</span>
          </div>
          <Link
            href={`/players/${stats.playerId}/sessions`}
            className="text-sm font-semibold text-primary-700 hover:underline"
          >
            {labels.viewAll}
          </Link>
        </>
      )}
    </div>
  );
}
