/**
 * ContributorsSidebar — top-5 community contributors + community stats grid.
 *
 * Wave A.3b (Issue #596). Mirrors mockup `sp3-shared-games.jsx` lines 433-522.
 * Sticky aside (top: 116) shown on desktop only; the page is responsible for
 * hiding the column on mobile via responsive grid.
 *
 * Items: rank pip + initials avatar (32px circle, hsl from `playerColor` 60% 55%)
 * + display name + sessions/wins meta line.
 * Footer: 2-col grid showing aggregate toolkit / agent counts.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

import { EntityChip } from '@/components/ui/v2/entity-chip/entity-chip';
import { EntityPip } from '@/components/ui/v2/entity-pip/entity-pip';

export interface ContributorsSidebarItem {
  readonly userId: string;
  readonly displayName: string;
  readonly avatarUrl?: string | null;
  readonly totalSessions: number;
  readonly totalWins: number;
  /** Optional HSL hue for the avatar tint (mockup uses `hsl(${color}, 60%, 55%)`). */
  readonly hue?: number;
}

export interface ContributorsSidebarLabels {
  readonly title: string;
  readonly emptyTitle: string;
  /** e.g. "${sessions} sessioni · ${wins} vittorie". */
  readonly meta: (sessions: number, wins: number) => string;
  readonly toolkitsLabel: string;
  readonly agentsLabel: string;
  readonly statsHeading: string;
  /** Aria label fragment for rank, e.g. "Posizione 1". */
  readonly rankAriaLabel: (rank: number) => string;
}

export interface ContributorsSidebarProps {
  readonly contributors: readonly ContributorsSidebarItem[];
  readonly toolkitsTotal: number;
  readonly agentsTotal: number;
  readonly labels: ContributorsSidebarLabels;
  readonly className?: string;
}

function initials(name: string): string {
  const trimmed = name.trim();
  if (trimmed.length === 0) return '·';
  const parts = trimmed.split(/\s+/);
  const [first, second] = parts;
  if (parts.length === 1 || !second) return first.slice(0, 2).toUpperCase();
  return (first.charAt(0) + second.charAt(0)).toUpperCase();
}

export function ContributorsSidebar({
  contributors,
  toolkitsTotal,
  agentsTotal,
  labels,
  className,
}: ContributorsSidebarProps): JSX.Element {
  const isEmpty = contributors.length === 0;
  return (
    <aside
      data-slot="shared-games-contributors-sidebar"
      aria-label={labels.title}
      className={clsx(
        'sticky top-[116px] flex flex-col gap-4 rounded-lg border border-border bg-card p-4',
        className
      )}
    >
      <h2 className="m-0 flex items-center gap-2 font-display text-[14px] font-bold text-foreground">
        <EntityPip entity="player" />
        {labels.title}
      </h2>

      {isEmpty ? (
        <p className="m-0 text-[12.5px] leading-[1.55] text-[hsl(var(--text-sec))]">
          {labels.emptyTitle}
        </p>
      ) : (
        <ol className="m-0 flex list-none flex-col gap-2 p-0">
          {contributors.map((c, idx) => {
            const rank = idx + 1;
            const hue = c.hue ?? 280;
            return (
              <li
                key={c.userId}
                data-slot="shared-games-contributor-item"
                data-rank={rank}
                className="flex items-center gap-3"
              >
                <span
                  aria-label={labels.rankAriaLabel(rank)}
                  className="font-mono text-[11px] font-bold tabular-nums text-[hsl(var(--text-muted))]"
                >
                  #{rank}
                </span>
                <span
                  aria-hidden="true"
                  style={{ backgroundColor: `hsl(${hue}, 60%, 55%)` }}
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full font-mono text-[11px] font-bold text-white"
                >
                  {initials(c.displayName)}
                </span>
                <span className="flex min-w-0 flex-col gap-0.5">
                  <span className="truncate font-display text-[13px] font-bold text-foreground">
                    {c.displayName}
                  </span>
                  <span className="font-mono text-[10px] text-[hsl(var(--text-muted))]">
                    {labels.meta(c.totalSessions, c.totalWins)}
                  </span>
                </span>
              </li>
            );
          })}
        </ol>
      )}

      <div className="flex flex-col gap-2 border-t border-border pt-3">
        <h3 className="m-0 font-mono text-[10px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--text-muted))]">
          {labels.statsHeading}
        </h3>
        <div className="grid grid-cols-2 gap-2">
          <EntityChip entity="toolkit" label={`${toolkitsTotal} ${labels.toolkitsLabel}`} />
          <EntityChip entity="agent" label={`${agentsTotal} ${labels.agentsLabel}`} />
        </div>
      </div>
    </aside>
  );
}
