'use client';

import Link from 'next/link';

import { DashboardSection } from './DashboardSection';
import { EmptySection } from './EmptySection';

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

/** Deterministic gradient from player colorIndex (matches mockup `grad(h,s)`). */
function avatarGradient(seed: number): string {
  const h = Math.abs(seed) % 360;
  const h2 = (h + 340) % 360;
  const h3 = (h + 30) % 360;
  return `linear-gradient(135deg, hsl(${h}, 60%, 55%), hsl(${h2}, 40%, 30%) 60%, hsl(${h3}, 50%, 40%))`;
}

export function PlayersAvatarList({
  players,
  totalCount,
  labels,
  onViewAllClick,
  onEmptyCtaClick,
}: PlayersAvatarListProps) {
  const visible = players.slice(0, 5);
  const totalShown = Math.max(totalCount, visible.length);

  return (
    <DashboardSection
      sectionId="players"
      entity="player"
      icon="👤"
      title={labels.title}
      count={totalShown}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
    >
      {visible.length === 0 ? (
        <EmptySection
          entity="player"
          icon="👤"
          message={labels.emptyTitle}
          cta={labels.emptyCta}
          ctaHref={labels.emptyCtaHref}
          onCtaClick={() => onEmptyCtaClick?.('players', labels.emptyCtaHref)}
        />
      ) : (
        <div className="flex flex-wrap items-center gap-2 sm:gap-3">
          {visible.map(p => (
            <Link
              key={p.name}
              href="/players"
              data-slot="dashboard-player-avatar"
              title={p.name}
              className="flex flex-col items-center gap-1 no-underline"
            >
              <div
                aria-hidden="true"
                className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border-2 border-card font-quicksand text-sm font-extrabold text-[#fff] sm:h-[52px] sm:w-[52px] sm:text-base"
                style={{
                  background: avatarGradient(p.colorIndex),
                  boxShadow: '0 2px 8px hsl(var(--c-player) / 0.25)',
                }}
              >
                {p.initials}
              </div>
              <div className="max-w-[50px] truncate font-quicksand text-[10px] font-bold text-foreground sm:max-w-[60px]">
                {p.name.split(' ')[0]}
              </div>
            </Link>
          ))}
          <div className="ml-auto flex items-center font-mono text-[10px] font-bold uppercase tracking-[0.06em] text-muted-foreground">
            {labels.countTemplate
              .replace('{visible}', String(visible.length))
              .replace('{total}', String(totalShown))}
          </div>
        </div>
      )}
    </DashboardSection>
  );
}
