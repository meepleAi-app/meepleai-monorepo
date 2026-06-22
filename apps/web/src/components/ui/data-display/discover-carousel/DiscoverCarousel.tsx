'use client';

import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react';

export interface DiscoverCarouselProps {
  children: ReactNode;
  ariaLabel: string;
  itemWidth?: number;
  gap?: number;
}

const DEFAULT_ITEM_WIDTH = 260;
const DEFAULT_GAP = 12;

export function DiscoverCarousel({
  children,
  ariaLabel,
  itemWidth = DEFAULT_ITEM_WIDTH,
  gap = DEFAULT_GAP,
}: DiscoverCarouselProps) {
  const trackId = useId();
  const trackRef = useRef<HTMLDivElement | null>(null);
  const step = itemWidth + gap;

  const scrollByStep = (delta: number) => {
    trackRef.current?.scrollBy({ left: delta, behavior: 'smooth' });
  };

  const scrollToEdge = (edge: 'start' | 'end') => {
    if (!trackRef.current) return;
    const left = edge === 'start' ? 0 : trackRef.current.scrollWidth;
    trackRef.current.scrollTo({ left, behavior: 'smooth' });
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    switch (event.key) {
      case 'ArrowRight':
        event.preventDefault();
        scrollByStep(step);
        break;
      case 'ArrowLeft':
        event.preventDefault();
        scrollByStep(-step);
        break;
      case 'Home':
        event.preventDefault();
        scrollToEdge('start');
        break;
      case 'End':
        event.preventDefault();
        scrollToEdge('end');
        break;
    }
  };

  return (
    <div className="discover-carousel group relative" role="region" aria-label={ariaLabel}>
      <div
        data-testid="discover-fade-left"
        aria-hidden="true"
        className="pointer-events-none absolute bottom-0 left-0 top-0 z-[1] w-12 bg-gradient-to-r from-background to-transparent"
      />
      <div
        data-testid="discover-fade-right"
        aria-hidden="true"
        className="pointer-events-none absolute bottom-0 right-0 top-0 z-[1] w-12 bg-gradient-to-l from-background to-transparent"
      />

      <button
        type="button"
        aria-label="Scorri verso sinistra"
        aria-controls={trackId}
        onClick={() => scrollByStep(-step)}
        className="absolute left-2 top-1/2 z-[2] grid h-10 w-10 -translate-y-1/2 place-items-center rounded-full border border-border bg-card opacity-0 shadow-md transition-opacity duration-200 focus-visible:opacity-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--c-game))] group-hover:opacity-100"
      >
        ‹
      </button>

      <div
        ref={trackRef}
        id={trackId}
        data-testid="discover-track"
        tabIndex={0}
        onKeyDown={handleKeyDown}
        className="flex gap-3 overflow-x-auto pb-2 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--c-game))]"
        style={{ scrollSnapType: 'x mandatory' }}
      >
        {children}
      </div>

      <button
        type="button"
        aria-label="Scorri verso destra"
        aria-controls={trackId}
        onClick={() => scrollByStep(step)}
        className="absolute right-2 top-1/2 z-[2] grid h-10 w-10 -translate-y-1/2 place-items-center rounded-full border border-border bg-card opacity-0 shadow-md transition-opacity duration-200 focus-visible:opacity-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--c-game))] group-hover:opacity-100"
      >
        ›
      </button>
    </div>
  );
}
