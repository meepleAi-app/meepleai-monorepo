'use client';

import Link from 'next/link';

import type { Game } from '@/lib/api/schemas/games.schemas';

import { DashboardSection } from './DashboardSection';
import { EmptySection } from './EmptySection';

export interface GamesCarouselLabels {
  readonly title: string;
  readonly viewAllLabel: string;
  readonly viewAllHref: string;
  readonly emptyTitle: string;
  readonly emptyCta: string;
  readonly emptyCtaHref: string;
}

export interface GamesCarouselProps {
  readonly games: ReadonlyArray<Game>;
  readonly totalCount: number;
  readonly labels: GamesCarouselLabels;
  readonly onViewAllClick?: (sectionId: string, viewAllHref: string) => void;
  readonly onEmptyCtaClick?: (sectionId: string, ctaHref: string) => void;
}

/** Deterministic gradient cover from a game id (matches mockup `grad(h,s)`). */
function coverGradient(seed: string): string {
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) % 360;
  const h2 = (h + 340) % 360;
  const h3 = (h + 30) % 360;
  return `linear-gradient(135deg, hsl(${h}, 70%, 55%), hsl(${h2}, 50%, 30%) 60%, hsl(${h3}, 60%, 40%))`;
}

export function GamesCarousel({
  games,
  totalCount,
  labels,
  onViewAllClick,
  onEmptyCtaClick,
}: GamesCarouselProps) {
  const top = games.slice(0, 3);

  return (
    <DashboardSection
      sectionId="games"
      entity="game"
      icon="🎲"
      title={labels.title}
      count={totalCount}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
    >
      {top.length === 0 ? (
        <EmptySection
          entity="game"
          icon="🎲"
          message={labels.emptyTitle}
          cta={labels.emptyCta}
          ctaHref={labels.emptyCtaHref}
          onCtaClick={() => onEmptyCtaClick?.('games', labels.emptyCtaHref)}
        />
      ) : (
        <div className="grid grid-cols-3 gap-2 sm:gap-2.5">
          {top.map(g => {
            const cover = g.imageUrl ?? g.iconUrl ?? null;
            return (
              <Link
                key={g.id}
                href={`/library/${g.id}`}
                data-slot="dashboard-game-card"
                className="flex flex-col gap-1.5 overflow-hidden rounded-[10px] border border-border bg-background transition-colors hover:border-border-strong"
              >
                <div
                  aria-hidden="true"
                  className="relative flex items-center justify-center overflow-hidden"
                  style={{
                    aspectRatio: '5 / 3',
                    background: coverGradient(g.id),
                  }}
                >
                  {cover ? (
                    // eslint-disable-next-line @next/next/no-img-element -- avoid Next/Image domain config for community-provided BGG URLs
                    <img
                      src={cover}
                      alt=""
                      loading="lazy"
                      className="absolute inset-0 h-full w-full object-cover"
                    />
                  ) : (
                    <span
                      className="text-[36px]"
                      style={{ filter: 'drop-shadow(0 2px 8px rgba(0,0,0,0.35))' }}
                    >
                      🎲
                    </span>
                  )}
                </div>
                <div className="px-2 pb-2 pt-0.5">
                  <div className="line-clamp-1 font-quicksand text-xs font-extrabold text-foreground">
                    {g.title}
                  </div>
                  <div className="font-mono text-[9px] font-semibold text-muted-foreground">
                    {g.yearPublished ?? '—'}
                  </div>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </DashboardSection>
  );
}
