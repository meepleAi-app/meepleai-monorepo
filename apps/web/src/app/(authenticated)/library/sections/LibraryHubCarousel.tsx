'use client';

import type { ReactNode } from 'react';

import Link from 'next/link';

type CarouselEntity = 'game' | 'session' | 'player' | 'chat' | 'wishlist';

interface LibraryHubCarouselProps {
  title: string;
  count?: number;
  seeAllHref?: string;
  seeAllLabel?: string;
  entity: CarouselEntity;
  children: ReactNode;
}

const ENTITY_ACCENTS: Record<CarouselEntity, string> = {
  game: 'hsl(25 95% 45%)',
  session: 'hsl(240 60% 55%)',
  player: 'hsl(262 83% 58%)',
  chat: 'hsl(220 80% 55%)',
  wishlist: 'hsl(38 92% 50%)',
};

export function LibraryHubCarousel({
  title,
  count,
  seeAllHref,
  seeAllLabel = 'Vedi tutto',
  entity,
  children,
}: LibraryHubCarouselProps) {
  const accent = ENTITY_ACCENTS[entity];

  return (
    <section className="relative mb-9">
      <div className="mb-4 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <span
            data-testid="carousel-accent-bar"
            data-entity={entity}
            aria-hidden
            className="inline-block h-[22px] w-1 rounded-sm"
            style={{ background: accent }}
          />
          <h3 className="font-quicksand text-[1.2rem] font-extrabold tracking-tight">{title}</h3>
          {count !== undefined && (
            <span
              data-testid="carousel-count"
              className="rounded-full bg-[rgba(160,120,60,0.1)] px-2.5 py-1 font-quicksand text-[0.72rem] font-extrabold text-[var(--nh-text-secondary)]"
            >
              {count}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            aria-label="Scroll left"
            className="flex h-9 w-9 items-center justify-center rounded-[10px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] text-sm text-[var(--nh-text-secondary)] transition-all hover:bg-white hover:shadow-[var(--shadow-warm-sm)] hover:text-[var(--nh-text-primary)]"
          >
            ‹
          </button>
          <button
            type="button"
            aria-label="Scroll right"
            className="flex h-9 w-9 items-center justify-center rounded-[10px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] text-sm text-[var(--nh-text-secondary)] transition-all hover:bg-white hover:shadow-[var(--shadow-warm-sm)] hover:text-[var(--nh-text-primary)]"
          >
            ›
          </button>
          {seeAllHref ? (
            <Link
              href={seeAllHref}
              className="rounded-lg px-3 py-1.5 font-nunito text-[0.82rem] font-extrabold text-[hsl(25_95%_40%)] transition-colors hover:bg-[hsla(25,95%,45%,0.08)]"
            >
              {seeAllLabel} →
            </Link>
          ) : null}
        </div>
      </div>
      <div className="-mx-1 flex snap-x snap-mandatory gap-4 overflow-x-auto pb-4 pt-1 [scrollbar-color:rgba(160,120,60,0.25)_transparent] [scrollbar-width:thin]">
        {children}
      </div>
    </section>
  );
}
