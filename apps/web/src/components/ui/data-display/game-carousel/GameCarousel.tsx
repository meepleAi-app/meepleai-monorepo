/**
 * GameCarousel - Immersive 3D Carousel for Browsing Games
 *
 * A visually striking carousel component with depth perspective,
 * featuring a prominent center card with fading side cards.
 * Designed with MeepleAI's warm orange brand aesthetic.
 *
 * Features:
 * - 3D perspective with depth illusion
 * - Responsive: 1 card mobile, 3 tablet, 5 desktop
 * - Touch swipe, keyboard navigation, and click controls
 * - Infinite loop with smooth animations
 * - Uses MeepleCard for consistent entity display
 *
 * @example
 * ```tsx
 * <GameCarousel
 *   games={games}
 *   onGameSelect={(game) => router.push(`/games/${game.id}`)}
 * />
 * ```
 */

'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';

import { useEntityActions } from '@/hooks/useEntityActions';
import { cn } from '@/lib/utils';

import { MeepleCard } from '../meeple-card';
import { AutoPlayButton, DotsIndicator, NavButton, SortDropdown } from './components';
import { ANIMATION_DURATION } from './constants';
import { useKeyboardNavigation, useSwipe } from './hooks';
import { calculateCardPositions, type CardPosition } from './math';

import type { CarouselGame, CarouselSortValue, GameCarouselProps } from './types';

export const GameCarousel = React.memo(function GameCarousel({
  games,
  onGameSelect,
  title,
  subtitle,
  autoPlay = false,
  autoPlayInterval = 5000,
  showDots = true,
  sortable = false,
  defaultSort = 'rating',
  sort: controlledSort,
  onSortChange,
  flippable,
  className,
  'data-testid': testId,
  userId,
}: GameCarouselProps) {
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isAnimating, setIsAnimating] = useState(false);
  const [isAutoPlaying, setIsAutoPlaying] = useState(autoPlay);
  const [isFocused, setIsFocused] = useState(false);
  const [visibleCards, setVisibleCards] = useState(5);
  const [internalSort, setInternalSort] = useState<CarouselSortValue>(defaultSort);
  const containerRef = useRef<HTMLDivElement>(null);
  const autoPlayRef = useRef<NodeJS.Timeout | null>(null);

  // Issue #4040: Generate entity actions for current visible card
  // Only generate for center card to avoid unnecessary hook calls
  // Guard: Use first game's ID as fallback to prevent empty string (broken links)
  const centerGame = games[currentIndex];
  const fallbackId =
    games.length > 0 ? centerGame?.id || games[0]?.id || 'placeholder' : 'placeholder';
  const centerEntityActions = useEntityActions({
    entity: 'game',
    id: fallbackId,
    userId,
    data: { hasKb: centerGame?.hasKb ?? false },
  });

  // Support controlled and uncontrolled sort modes
  const currentSort = controlledSort ?? internalSort;
  const handleSortChange = useCallback(
    (newSort: CarouselSortValue) => {
      setInternalSort(newSort);
      onSortChange?.(newSort);
      // Reset to first card when sort changes
      setCurrentIndex(0);
    },
    [onSortChange]
  );

  // Responsive visible cards count
  useEffect(() => {
    const updateVisibleCards = () => {
      const width = window.innerWidth;
      if (width < 640) {
        setVisibleCards(1);
      } else if (width < 1024) {
        setVisibleCards(3);
      } else {
        setVisibleCards(5);
      }
    };

    updateVisibleCards();
    window.addEventListener('resize', updateVisibleCards);
    return () => window.removeEventListener('resize', updateVisibleCards);
  }, []);

  // Navigation handlers
  const goToNext = useCallback(() => {
    if (isAnimating || games.length === 0) return;

    setIsAnimating(true);
    setCurrentIndex(prev => (prev + 1) % games.length);

    setTimeout(() => setIsAnimating(false), ANIMATION_DURATION);
  }, [isAnimating, games.length]);

  const goToPrev = useCallback(() => {
    if (isAnimating || games.length === 0) return;

    setIsAnimating(true);
    setCurrentIndex(prev => (prev - 1 + games.length) % games.length);

    setTimeout(() => setIsAnimating(false), ANIMATION_DURATION);
  }, [isAnimating, games.length]);

  const goToIndex = useCallback(
    (index: number) => {
      if (isAnimating || index === currentIndex) return;

      setIsAnimating(true);
      setCurrentIndex(index);

      setTimeout(() => setIsAnimating(false), ANIMATION_DURATION);
    },
    [isAnimating, currentIndex]
  );

  // Auto-play logic
  useEffect(() => {
    if (isAutoPlaying && !isFocused) {
      autoPlayRef.current = setInterval(goToNext, autoPlayInterval);
    }

    return () => {
      if (autoPlayRef.current) {
        clearInterval(autoPlayRef.current);
      }
    };
  }, [isAutoPlaying, isFocused, goToNext, autoPlayInterval]);

  // Pause auto-play on hover/focus
  const handleMouseEnter = useCallback(() => setIsFocused(true), []);
  const handleMouseLeave = useCallback(() => setIsFocused(false), []);
  const handleFocus = useCallback(() => setIsFocused(true), []);
  const handleBlur = useCallback(() => setIsFocused(false), []);

  // Hooks
  const { handleTouchStart, handleTouchMove, handleTouchEnd } = useSwipe(goToNext, goToPrev);
  useKeyboardNavigation(goToPrev, goToNext, isFocused);

  // Calculate card positions
  const cardPositions = calculateCardPositions(games.length, currentIndex, visibleCards);

  // Handle card click
  const handleCardClick = useCallback(
    (game: CarouselGame, position: CardPosition) => {
      if (position.offset === 0) {
        // Center card - trigger selection
        onGameSelect?.(game);
      } else {
        // Side card - bring to center
        goToIndex(position.index);
      }
    },
    [onGameSelect, goToIndex]
  );

  if (games.length === 0) {
    return (
      <div
        className={cn('flex items-center justify-center py-20', 'text-muted-foreground')}
        data-testid={testId}
      >
        <p>No games to display</p>
      </div>
    );
  }

  return (
    <section
      ref={containerRef}
      className={cn('relative w-full overflow-x-clip', 'py-8 md:py-12', className)}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      onFocus={handleFocus}
      onBlur={handleBlur}
      data-testid={testId || 'game-carousel'}
      aria-roledescription="carousel"
      aria-label={title || 'Game carousel'}
    >
      {/* Header */}
      {(title || subtitle || autoPlay || sortable) && (
        <header className="flex items-center justify-between mb-8 px-4 md:px-8">
          <div>
            {title && (
              <h2
                className={cn(
                  'font-quicksand font-bold text-2xl md:text-3xl',
                  'text-foreground',
                  'relative inline-block',
                  // Decorative underline
                  'after:absolute after:left-0 after:bottom-0 after:w-1/2',
                  'after:h-1 after:bg-primary after:rounded-full',
                  'after:transform after:translate-y-2'
                )}
              >
                {title}
              </h2>
            )}
            {subtitle && (
              <p className="mt-3 text-muted-foreground text-sm md:text-base">{subtitle}</p>
            )}
          </div>
          <div className="flex items-center gap-2">
            {sortable && <SortDropdown value={currentSort} onChange={handleSortChange} />}
            {autoPlay && (
              <AutoPlayButton
                isPlaying={isAutoPlaying}
                onClick={() => setIsAutoPlaying(prev => !prev)}
              />
            )}
          </div>
        </header>
      )}

      {/* Carousel Stage */}
      <div
        className={cn(
          'relative',
          'h-[350px] sm:h-[390px] md:h-[430px] lg:h-[470px]',
          // Perspective for 3D effect
          'perspective-[1200px]'
        )}
        onTouchStart={handleTouchStart}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
      >
        {/* Cards Container */}
        <div className="absolute inset-0 flex items-center justify-center">
          {cardPositions.map(position => {
            const game = games[position.index];
            if (!game || !position.visible) return null;

            const isCenter = position.offset === 0;

            return (
              <div
                key={game.id}
                className={cn(
                  'absolute',
                  'transition-all ease-out',
                  isAnimating ? 'duration-500' : 'duration-300',
                  // Card sizing - responsive
                  'w-[260px] sm:w-[280px] md:w-[300px] lg:w-[340px]'
                )}
                style={{
                  transform: `
                    translateX(${position.translateX}%)
                    scale(${position.scale})
                    rotateY(${position.offset * -5}deg)
                  `,
                  opacity: position.opacity,
                  zIndex: position.zIndex,
                  filter: `blur(${position.blur}px)`,
                }}
                aria-hidden={!isCenter}
              >
                <div
                  className={cn(
                    'transform transition-transform duration-300',
                    isCenter && 'hover:scale-[1.02]',
                    !isCenter && 'cursor-pointer'
                  )}
                >
                  <MeepleCard
                    entity="game"
                    variant="grid"
                    title={game.title}
                    subtitle={game.subtitle}
                    imageUrl={game.imageUrl}
                    rating={game.rating}
                    ratingMax={game.ratingMax || 10}
                    metadata={game.metadata}
                    badge={game.badge}
                    onClick={() => handleCardClick(game, position)}
                    flippable={flippable && !!game.description}
                    flipData={
                      flippable && game.description ? { description: game.description } : undefined
                    }
                    flipTrigger="button"
                    className={cn(
                      // Enhanced shadow & glow for center card — v2 (Issue #4612)
                      isCenter && '[box-shadow:var(--shadow-warm-2xl)]'
                    )}
                    data-testid={`carousel-card-${position.index}`}
                    // Issue #4040: Quick actions + Info button (only on center card)
                    entityQuickActions={isCenter ? centerEntityActions.quickActions : undefined}
                    showInfoButton={isCenter}
                    infoHref={isCenter ? `/games/${game.id}` : undefined}
                    infoTooltip="Vai al dettaglio"
                  />
                </div>
              </div>
            );
          })}
        </div>

        {/* Gradient Fades on Edges */}
        <div
          className={cn(
            'absolute left-0 top-0 bottom-0 w-16 md:w-24',
            'bg-gradient-to-r from-background to-transparent',
            'pointer-events-none z-20'
          )}
          aria-hidden="true"
        />
        <div
          className={cn(
            'absolute right-0 top-0 bottom-0 w-16 md:w-24',
            'bg-gradient-to-l from-background to-transparent',
            'pointer-events-none z-20'
          )}
          aria-hidden="true"
        />

        {/* Navigation Arrows */}
        <NavButton direction="prev" onClick={goToPrev} disabled={isAnimating} />
        <NavButton direction="next" onClick={goToNext} disabled={isAnimating} />
      </div>

      {/* Dots Indicator */}
      {showDots && games.length > 1 && (
        <DotsIndicator total={games.length} current={currentIndex} onDotClick={goToIndex} />
      )}

      {/* Screen Reader Announcements */}
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {`Showing ${games[currentIndex]?.title}, ${currentIndex + 1} of ${games.length}`}
      </div>
    </section>
  );
});
