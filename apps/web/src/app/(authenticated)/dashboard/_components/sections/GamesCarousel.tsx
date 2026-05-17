'use client';

import Link from 'next/link';

import type { Game } from '@/lib/api/schemas/games.schemas';

import { DashboardSection } from './DashboardSection';

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
      icon="🎲"
      title={labels.title}
      count={totalCount}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
    >
      {top.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-border bg-muted/30 px-3 py-6 text-center">
          <span aria-hidden="true" className="text-3xl">
            🎲
          </span>
          <p className="text-sm text-muted-foreground">{labels.emptyTitle}</p>
          <Link
            href={labels.emptyCtaHref}
            onClick={() => onEmptyCtaClick?.('games', labels.emptyCtaHref)}
            className="mt-1 inline-flex items-center rounded-lg bg-foreground px-3 py-1.5 font-bold font-[Quicksand] text-xs text-background"
          >
            {labels.emptyCta}
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-3 gap-2">
          {top.map(g => (
            <Link
              key={g.id}
              href={`/library/${g.id}`}
              data-slot="dashboard-game-card"
              className="group flex flex-col overflow-hidden rounded-lg border border-border bg-card hover:border-border-strong"
            >
              <div
                aria-hidden="true"
                className="flex h-16 items-center justify-center bg-gradient-to-br from-muted to-muted/40 text-2xl"
              >
                🎲
              </div>
              <div className="flex flex-col gap-0.5 p-2">
                <div className="line-clamp-1 font-bold font-[Quicksand] text-xs text-foreground">
                  {g.title}
                </div>
                <div className="font-mono text-[9px] text-muted-foreground">
                  {g.yearPublished ?? '—'}
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </DashboardSection>
  );
}
