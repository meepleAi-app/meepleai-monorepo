'use client';

import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';

import { ChevronLeft, ChevronRight } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface RowScrollerProps {
  /** Card width hint for keyboard scroll step (default: 200px). */
  readonly cardWidth?: number;
  /** Accessible region name. */
  readonly ariaLabel: string;
  /** Cards rendered inside the scroll region. */
  readonly children: ReactNode;
}

const DEFAULT_CARD_WIDTH = 200;
const KEYBOARD_STEP_MULT = 1.5;

/**
 * RowScroller — horizontal scroll region with keyboard arrows + hover arrows.
 *
 * Semantically `role="region"` (NOT a WAI-ARIA carousel — the mockup is not
 * auto-advancing). `tabIndex={0}` makes the region keyboard-focusable, and
 * ArrowLeft / ArrowRight scroll by ~1.5× card width. Hover arrows on desktop
 * are visual affordance only.
 *
 * Respects `prefers-reduced-motion`: scroll-snap stays on (snap points), but
 * smooth animation is skipped when the media query matches.
 */
export function RowScroller({
  cardWidth = DEFAULT_CARD_WIDTH,
  ariaLabel,
  children,
}: RowScrollerProps) {
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);

  const updateScrollAffordances = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    setCanScrollLeft(el.scrollLeft > 4);
    setCanScrollRight(el.scrollLeft + el.clientWidth < el.scrollWidth - 4);
  }, []);

  useEffect(() => {
    updateScrollAffordances();
    const el = scrollRef.current;
    if (!el) return;
    el.addEventListener('scroll', updateScrollAffordances, { passive: true });
    return () => el.removeEventListener('scroll', updateScrollAffordances);
  }, [updateScrollAffordances]);

  const scrollBy = useCallback((delta: number) => {
    const el = scrollRef.current;
    if (!el) return;
    const reducedMotion =
      typeof window !== 'undefined' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    el.scrollBy({
      left: delta,
      behavior: reducedMotion ? 'auto' : 'smooth',
    });
  }, []);

  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLDivElement>) => {
      if (event.key === 'ArrowRight') {
        event.preventDefault();
        scrollBy(cardWidth * KEYBOARD_STEP_MULT);
      } else if (event.key === 'ArrowLeft') {
        event.preventDefault();
        scrollBy(-cardWidth * KEYBOARD_STEP_MULT);
      } else if (event.key === 'Home') {
        event.preventDefault();
        scrollRef.current?.scrollTo({ left: 0, behavior: 'auto' });
      } else if (event.key === 'End') {
        event.preventDefault();
        const el = scrollRef.current;
        if (el) el.scrollTo({ left: el.scrollWidth, behavior: 'auto' });
      }
    },
    [cardWidth, scrollBy]
  );

  return (
    <div className="group relative">
      {/* Left arrow (hover-only on desktop) */}
      <button
        type="button"
        aria-label="Scorri a sinistra"
        tabIndex={-1}
        onClick={() => scrollBy(-cardWidth * KEYBOARD_STEP_MULT)}
        disabled={!canScrollLeft}
        className={cn(
          'pointer-events-none absolute left-1 top-1/2 z-10 hidden -translate-y-1/2 items-center justify-center rounded-full bg-card/90 p-1.5 shadow-md backdrop-blur transition-opacity sm:flex',
          'opacity-0 group-hover:pointer-events-auto group-hover:opacity-100',
          !canScrollLeft && 'pointer-events-none opacity-0 group-hover:opacity-30'
        )}
      >
        <ChevronLeft size={18} aria-hidden="true" />
      </button>

      {/* Scroll region */}
      <div
        ref={scrollRef}
        role="region"
        aria-label={ariaLabel}
        tabIndex={0}
        onKeyDown={handleKeyDown}
        data-slot="row-scroller"
        className={cn(
          'flex gap-3 overflow-x-auto scrollbar-none scroll-smooth',
          'snap-x snap-mandatory motion-reduce:scroll-auto',
          'outline-none focus-visible:ring-2 focus-visible:ring-foreground/20 focus-visible:rounded-md',
          'px-1 py-2'
        )}
        style={{ scrollPaddingInline: 8 }}
      >
        {children}
      </div>

      {/* Right arrow (hover-only on desktop) */}
      <button
        type="button"
        aria-label="Scorri a destra"
        tabIndex={-1}
        onClick={() => scrollBy(cardWidth * KEYBOARD_STEP_MULT)}
        disabled={!canScrollRight}
        className={cn(
          'pointer-events-none absolute right-1 top-1/2 z-10 hidden -translate-y-1/2 items-center justify-center rounded-full bg-card/90 p-1.5 shadow-md backdrop-blur transition-opacity sm:flex',
          'opacity-0 group-hover:pointer-events-auto group-hover:opacity-100',
          !canScrollRight && 'pointer-events-none opacity-0 group-hover:opacity-30'
        )}
      >
        <ChevronRight size={18} aria-hidden="true" />
      </button>
    </div>
  );
}
