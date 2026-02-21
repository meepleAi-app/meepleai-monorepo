/**
 * GameCarousel - Immersive 3D Carousel for Browsing Games
 *
 * A visually striking carousel component with depth perspective,
 * featuring a prominent center card with fading side cards.
 * Designed with MeepleAI's warm orange brand aesthetic.
 *
 * @module components/ui/data-display/game-carousel
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

import {
  ArrowDownAZ,
  Calendar,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Pause,
  Play,
  Star,
  TrendingUp,
} from 'lucide-react';

import { useEntityActions } from '@/hooks/use-entity-actions';
import { cn } from '@/lib/utils';

import { MeepleCard, type MeepleCardMetadata } from './meeple-card';

// ============================================================================
// Types & Interfaces
// ============================================================================

export interface CarouselGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  ratingMax?: number;
  metadata?: MeepleCardMetadata[];
  badge?: string;
  description?: string;
  /** Whether the game has an indexed knowledge base for RAG-based chat */
  hasKb?: boolean;
}

/**
 * Sort option values for carousel
 */
export type CarouselSortValue = 'rating' | 'popularity' | 'name' | 'date';

/**
 * Sort option configuration
 */
export interface CarouselSortOption {
  value: CarouselSortValue;
  label: string;
  icon: typeof Star;
}

/**
 * Available sort options
 */
export const CAROUSEL_SORT_OPTIONS: CarouselSortOption[] = [
  { value: 'rating', label: 'Rating', icon: Star },
  { value: 'popularity', label: 'Popularity', icon: TrendingUp },
  { value: 'name', label: 'Name', icon: ArrowDownAZ },
  { value: 'date', label: 'Date Added', icon: Calendar },
];

export interface GameCarouselProps {
  /** Array of games to display */
  games: CarouselGame[];
  /** Callback when a game card is clicked */
  onGameSelect?: (game: CarouselGame) => void;
  /** Title above the carousel */
  title?: string;
  /** Subtitle/description */
  subtitle?: string;
  /** Enable auto-play rotation */
  autoPlay?: boolean;
  /** Auto-play interval in ms (default: 5000) */
  autoPlayInterval?: number;
  /** Show navigation dots */
  showDots?: boolean;
  /** Enable sorting controls */
  sortable?: boolean;
  /** Default sort value */
  defaultSort?: CarouselSortValue;
  /** Current sort value (controlled) */
  sort?: CarouselSortValue;
  /** Callback when sort changes */
  onSortChange?: (sort: CarouselSortValue) => void;
  /** Enable flip on carousel cards */
  flippable?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
  /** Current user ID (for auth-gated actions) */
  userId?: string;
}

// ============================================================================
// Constants
// ============================================================================

const ANIMATION_DURATION = 500; // ms
const SWIPE_THRESHOLD = 50; // px

// ============================================================================
// Hooks
// ============================================================================

/**
 * Custom hook for touch/swipe handling
 */
function useSwipe(onSwipeLeft: () => void, onSwipeRight: () => void) {
  const touchStartX = useRef<number>(0);
  const touchEndX = useRef<number>(0);

  const handleTouchStart = useCallback((e: React.TouchEvent) => {
    touchStartX.current = e.touches[0].clientX;
  }, []);

  const handleTouchMove = useCallback((e: React.TouchEvent) => {
    touchEndX.current = e.touches[0].clientX;
  }, []);

  const handleTouchEnd = useCallback(() => {
    const diff = touchStartX.current - touchEndX.current;
    if (Math.abs(diff) > SWIPE_THRESHOLD) {
      if (diff > 0) {
        onSwipeLeft();
      } else {
        onSwipeRight();
      }
    }
  }, [onSwipeLeft, onSwipeRight]);

  return { handleTouchStart, handleTouchMove, handleTouchEnd };
}

/**
 * Custom hook for keyboard navigation
 */
function useKeyboardNavigation(
  onPrev: () => void,
  onNext: () => void,
  enabled: boolean
) {
  useEffect(() => {
    if (!enabled) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'ArrowLeft') {
        e.preventDefault();
        onPrev();
      } else if (e.key === 'ArrowRight') {
        e.preventDefault();
        onNext();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onPrev, onNext, enabled]);
}

// ============================================================================
// Subcomponents
// ============================================================================

/**
 * Navigation arrow button
 */
function NavButton({
  direction,
  onClick,
  disabled,
}: {
  direction: 'prev' | 'next';
  onClick: () => void;
  disabled?: boolean;
}) {
  const Icon = direction === 'prev' ? ChevronLeft : ChevronRight;

  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'absolute top-1/2 -translate-y-1/2 z-30',
        'w-12 h-12 md:w-14 md:h-14 rounded-full',
        'flex items-center justify-center',
        'bg-card/95 backdrop-blur-sm border border-border/50',
        'shadow-lg hover:shadow-xl',
        'text-foreground hover:text-primary',
        'transition-all duration-300 ease-out',
        'hover:scale-110 active:scale-95',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        'disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:scale-100',
        // Glow effect on hover
        'hover:border-primary/50',
        'dark:hover:shadow-[0_0_20px_hsl(25_95%_45%/0.3)]',
        direction === 'prev' ? 'left-2 md:left-4' : 'right-2 md:right-4'
      )}
      aria-label={direction === 'prev' ? 'Previous game' : 'Next game'}
    >
      <Icon className="w-6 h-6 md:w-7 md:h-7" />
    </button>
  );
}

/**
 * Navigation dots indicator
 */
function DotsIndicator({
  total,
  current,
  onDotClick,
}: {
  total: number;
  current: number;
  onDotClick: (index: number) => void;
}) {
  // Show max 7 dots, with ellipsis for larger sets
  const maxDots = 7;
  const showEllipsis = total > maxDots;

  const getDots = () => {
    if (!showEllipsis) {
      return Array.from({ length: total }, (_, i) => i);
    }

    // Smart dot display: always show first, last, and around current
    const dots: (number | 'ellipsis')[] = [];
    const around = 1;

    for (let i = 0; i < total; i++) {
      if (
        i === 0 ||
        i === total - 1 ||
        (i >= current - around && i <= current + around)
      ) {
        dots.push(i);
      } else if (dots[dots.length - 1] !== 'ellipsis') {
        dots.push('ellipsis');
      }
    }
    return dots;
  };

  return (
    <div
      className="flex items-center justify-center gap-2 mt-6"
      role="tablist"
      aria-label="Carousel navigation"
    >
      {getDots().map((dot, idx) =>
        dot === 'ellipsis' ? (
          <span
            key={`ellipsis-${idx}`}
            className="w-2 h-2 text-muted-foreground"
          >
            ···
          </span>
        ) : (
          <button
            key={dot}
            onClick={() => onDotClick(dot)}
            role="tab"
            aria-selected={dot === current}
            aria-label={`Go to game ${dot + 1}`}
            className={cn(
              'rounded-full transition-all duration-300',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              dot === current
                ? 'w-8 h-2.5 bg-primary'
                : 'w-2.5 h-2.5 bg-muted-foreground/30 hover:bg-muted-foreground/60'
            )}
          />
        )
      )}
    </div>
  );
}

/**
 * Auto-play control button
 */
function AutoPlayButton({
  isPlaying,
  onClick,
}: {
  isPlaying: boolean;
  onClick: () => void;
}) {
  const Icon = isPlaying ? Pause : Play;

  return (
    <button
      onClick={onClick}
      className={cn(
        'w-10 h-10 rounded-full',
        'flex items-center justify-center',
        'bg-muted/80 hover:bg-muted',
        'text-muted-foreground hover:text-foreground',
        'transition-all duration-200',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
      )}
      aria-label={isPlaying ? 'Pause auto-play' : 'Start auto-play'}
    >
      <Icon className="w-4 h-4" />
    </button>
  );
}

/**
 * Sort dropdown component
 * Issue #3587: GC-002 — Sorting Controls
 */
function SortDropdown({
  value,
  onChange,
}: {
  value: CarouselSortValue;
  onChange: (value: CarouselSortValue) => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Close on click outside
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen]);

  // Close on escape
  useEffect(() => {
    function handleEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    }

    if (isOpen) {
      document.addEventListener('keydown', handleEscape);
    }
    return () => document.removeEventListener('keydown', handleEscape);
  }, [isOpen]);

  const currentOption = CAROUSEL_SORT_OPTIONS.find(opt => opt.value === value) ?? CAROUSEL_SORT_OPTIONS[0];
  const CurrentIcon = currentOption.icon;

  return (
    <div ref={dropdownRef} className="relative">
      <button
        onClick={() => setIsOpen(prev => !prev)}
        className={cn(
          'flex items-center gap-2',
          'h-10 px-3 rounded-lg',
          'bg-muted/80 hover:bg-muted',
          'text-muted-foreground hover:text-foreground',
          'transition-all duration-200',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          // Mobile-friendly touch target
          'min-h-[44px] min-w-[44px]'
        )}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label={`Sort by ${currentOption.label}`}
      >
        <CurrentIcon className="w-4 h-4" />
        <span className="text-sm font-medium hidden sm:inline">{currentOption.label}</span>
        <ChevronDown
          className={cn(
            'w-4 h-4 transition-transform duration-200',
            isOpen && 'rotate-180'
          )}
        />
      </button>

      {/* Dropdown Menu */}
      {isOpen && (
        <div
          className={cn(
            'absolute right-0 mt-2 z-50',
            'w-48 rounded-lg',
            'bg-popover border border-border',
            'shadow-lg',
            // Animation
            'animate-in fade-in-0 zoom-in-95',
            'origin-top-right'
          )}
          role="listbox"
          aria-label="Sort options"
        >
          <div className="p-1">
            {CAROUSEL_SORT_OPTIONS.map(option => {
              const Icon = option.icon;
              const isSelected = option.value === value;

              return (
                <button
                  key={option.value}
                  onClick={() => {
                    onChange(option.value);
                    setIsOpen(false);
                  }}
                  role="option"
                  aria-selected={isSelected}
                  className={cn(
                    'w-full flex items-center gap-3',
                    'px-3 py-2 rounded-md',
                    'text-sm',
                    'transition-colors duration-150',
                    // Mobile-friendly touch target
                    'min-h-[44px]',
                    isSelected
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset'
                  )}
                >
                  <Icon className="w-4 h-4" />
                  <span className="flex-1 text-left">{option.label}</span>
                  {isSelected && (
                    <span className="text-xs text-primary" aria-hidden="true">✓</span>
                  )}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Card Position Calculator
// ============================================================================

interface CardPosition {
  index: number;
  offset: number; // -2, -1, 0, 1, 2 from center
  scale: number;
  opacity: number;
  zIndex: number;
  translateX: number;
  blur: number;
  visible: boolean;
}

function calculateCardPositions(
  totalCards: number,
  currentIndex: number,
  visibleCards: number
): CardPosition[] {
  const positions: CardPosition[] = [];
  const halfVisible = Math.floor(visibleCards / 2);

  for (let i = 0; i < totalCards; i++) {
    // Calculate circular offset from center
    let offset = i - currentIndex;

    // Handle infinite loop wrapping
    if (offset > totalCards / 2) offset -= totalCards;
    if (offset < -totalCards / 2) offset += totalCards;

    const absOffset = Math.abs(offset);
    const visible = absOffset <= halfVisible;

    // Calculate visual properties based on offset
    // v2: Center card 1.1x, side cards 0.85x (Issue #4612)
    const scale = visible ? (absOffset === 0 ? 1.1 : 0.85) : 0.5;
    const opacity = visible ? (absOffset === 0 ? 1 : 0.6) : 0;
    const zIndex = visible ? 10 - absOffset : 0;
    const blur = absOffset * 2;

    // Responsive spacing - percentage based
    const baseSpacing = 35; // % offset per card
    const translateX = offset * baseSpacing;

    positions.push({
      index: i,
      offset,
      scale,
      opacity,
      zIndex,
      translateX,
      blur,
      visible,
    });
  }

  return positions;
}

// ============================================================================
// Main Component
// ============================================================================

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
  // eslint-disable-next-line security/detect-object-injection -- currentIndex is controlled by useState, always valid array index
  const centerGame = games[currentIndex];
  const fallbackId = games.length > 0 ? (centerGame?.id || games[0]?.id || 'placeholder') : 'placeholder';
  const centerEntityActions = useEntityActions({
    entity: 'game',
    id: fallbackId,
    userId,
    data: { hasKb: centerGame?.hasKb ?? false },
  });

  // Support controlled and uncontrolled sort modes
  const currentSort = controlledSort ?? internalSort;
  const handleSortChange = useCallback((newSort: CarouselSortValue) => {
    setInternalSort(newSort);
    onSortChange?.(newSort);
    // Reset to first card when sort changes
    setCurrentIndex(0);
  }, [onSortChange]);

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
  const { handleTouchStart, handleTouchMove, handleTouchEnd } = useSwipe(
    goToNext,
    goToPrev
  );
  useKeyboardNavigation(goToPrev, goToNext, isFocused);

  // Calculate card positions
  const cardPositions = calculateCardPositions(
    games.length,
    currentIndex,
    visibleCards
  );

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
        className={cn(
          'flex items-center justify-center py-20',
          'text-muted-foreground'
        )}
        data-testid={testId}
      >
        <p>No games to display</p>
      </div>
    );
  }

  return (
    <section
      ref={containerRef}
      className={cn(
        'relative w-full overflow-x-clip',
        'py-8 md:py-12',
        className
      )}
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
              <p className="mt-3 text-muted-foreground text-sm md:text-base">
                {subtitle}
              </p>
            )}
          </div>
          <div className="flex items-center gap-2">
            {sortable && (
              <SortDropdown
                value={currentSort}
                onChange={handleSortChange}
              />
            )}
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
                    flipData={flippable && game.description ? { description: game.description } : undefined}
                    flipTrigger="button"
                    className={cn(
                      // Enhanced shadow & glow for center card — v2 (Issue #4612)
                      isCenter && '[box-shadow:var(--shadow-warm-2xl)]',
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
        <DotsIndicator
          total={games.length}
          current={currentIndex}
          onDotClick={goToIndex}
        />
      )}

      {/* Screen Reader Announcements */}
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {/* eslint-disable-next-line security/detect-object-injection -- currentIndex is controlled by useState, always a valid array index */}
        {`Showing ${games[currentIndex]?.title}, ${currentIndex + 1} of ${games.length}`}
      </div>
    </section>
  );
});

// ============================================================================
// Skeleton Loader
// ============================================================================

export function GameCarouselSkeleton({
  className,
}: {
  className?: string;
}) {
  return (
    <div
      className={cn('relative w-full overflow-hidden py-8 md:py-12', className)}
      data-testid="game-carousel-skeleton"
    >
      {/* Header skeleton */}
      <div className="flex items-center justify-between mb-8 px-4 md:px-8">
        <div>
          <div className="h-8 w-48 bg-muted rounded animate-pulse" />
          <div className="h-4 w-32 bg-muted rounded mt-3 animate-pulse" />
        </div>
      </div>

      {/* Cards skeleton */}
      <div className="relative h-[360px] md:h-[400px]">
        <div className="absolute inset-0 flex items-center justify-center gap-4">
          {/* Side cards */}
          <div className="w-[240px] h-[300px] bg-muted rounded-2xl animate-pulse opacity-50 scale-85" />
          {/* Center card */}
          <div className="w-[300px] h-[340px] bg-muted rounded-2xl animate-pulse" />
          {/* Side cards */}
          <div className="w-[240px] h-[300px] bg-muted rounded-2xl animate-pulse opacity-50 scale-85" />
        </div>
      </div>

      {/* Dots skeleton */}
      <div className="flex items-center justify-center gap-2 mt-6">
        <div className="w-8 h-2.5 bg-primary rounded-full" />
        <div className="w-2.5 h-2.5 bg-muted rounded-full animate-pulse" />
        <div className="w-2.5 h-2.5 bg-muted rounded-full animate-pulse" />
        <div className="w-2.5 h-2.5 bg-muted rounded-full animate-pulse" />
        <div className="w-2.5 h-2.5 bg-muted rounded-full animate-pulse" />
      </div>
    </div>
  );
}

