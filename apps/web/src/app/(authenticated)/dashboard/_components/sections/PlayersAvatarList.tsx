'use client';

import Link from 'next/link';

import { DashboardSection } from './DashboardSection';

export interface PlayerEntry {
  readonly name: string;
  readonly initials: string;
  /** Stable color seed (e.g. hash of name) for avatar tint. */
  readonly colorIndex: number;
}

export interface PlayersAvatarListLabels {
  readonly title: string;
  readonly viewAllLabel: string;
  readonly viewAllHref: string;
  readonly countTemplate: string;
  readonly emptyTitle: string;
  readonly emptyCta: string;
  readonly emptyCtaHref: string;
}

export interface PlayersAvatarListProps {
  readonly players: ReadonlyArray<PlayerEntry>;
  /** Total unique players derivable (mockup convention: shows "{visible} di {total}"). */
  readonly totalCount: number;
  readonly labels: PlayersAvatarListLabels;
  readonly onViewAllClick?: (sectionId: string, viewAllHref: string) => void;
  readonly onEmptyCtaClick?: (sectionId: string, ctaHref: string) => void;
}

const AVATAR_TINTS = [
  'bg-[hsl(var(--c-player)/0.18)] text-[hsl(var(--c-player))]',
  'bg-[hsl(var(--c-game)/0.18)] text-[hsl(var(--c-game))]',
  'bg-[hsl(var(--c-toolkit)/0.18)] text-[hsl(var(--c-toolkit))]',
  'bg-[hsl(var(--c-event)/0.18)] text-[hsl(var(--c-event))]',
  'bg-[hsl(var(--c-agent)/0.18)] text-[hsl(var(--c-agent))]',
];

export function PlayersAvatarList({
  players,
  totalCount,
  labels,
  onViewAllClick,
  onEmptyCtaClick,
}: PlayersAvatarListProps) {
  const visible = players.slice(0, 5);

  return (
    <DashboardSection
      sectionId="players"
      icon="👥"
      title={labels.title}
      count={totalCount}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
    >
      {visible.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-border bg-muted/30 px-3 py-6 text-center">
          <span aria-hidden="true" className="text-3xl">
            👥
          </span>
          <p className="text-sm text-muted-foreground">{labels.emptyTitle}</p>
          <Link
            href={labels.emptyCtaHref}
            onClick={() => onEmptyCtaClick?.('players', labels.emptyCtaHref)}
            className="mt-1 inline-flex items-center rounded-lg bg-foreground px-3 py-1.5 font-bold font-[Quicksand] text-xs text-background"
          >
            {labels.emptyCta}
          </Link>
        </div>
      ) : (
        <div className="flex items-center gap-2 overflow-hidden">
          <div className="flex -space-x-2">
            {visible.map(p => (
              <div
                key={p.name}
                title={p.name}
                data-slot="dashboard-player-avatar"
                className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-2 border-card font-bold font-[Quicksand] text-xs ${AVATAR_TINTS[p.colorIndex % AVATAR_TINTS.length]}`}
              >
                {p.initials}
              </div>
            ))}
          </div>
          <div className="ml-2 inline-flex items-center rounded-full bg-muted/60 px-2 py-1 font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
            {labels.countTemplate
              .replace('{visible}', String(visible.length))
              .replace('{total}', String(Math.max(totalCount, visible.length)))}
          </div>
        </div>
      )}
    </DashboardSection>
  );
}
